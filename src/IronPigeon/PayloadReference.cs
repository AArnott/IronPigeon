// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Net.Mime;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft;
    using PCLCrypto;

    /// <summary>
    /// Describes where some encrypted payload is found and how to authenticate and decrypt it.
    /// </summary>
    [DataContract]
    public class PayloadReference
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PayloadReference" /> class.
        /// </summary>
        /// <param name="location">The URL where the payload of the message may be downloaded.</param>
        /// <param name="contentType">The Content-Type of the payload.</param>
        /// <param name="hash">The hash of the encrypted bytes of the payload.</param>
        /// <param name="hashAlgorithmName">Name of the hash algorithm.</param>
        /// <param name="decryptionInputs">The material to reconstruct the symmetric key to decrypt the referenced message.</param>
        /// <param name="expiresUtc">The time beyond which the payload is expected to be deleted.</param>
        /// <param name="origin">The location from which this instance was downloaded.</param>
        public PayloadReference(Uri location, ContentType contentType, ReadOnlyMemory<byte> hash, string hashAlgorithmName, SymmetricEncryptionInputs decryptionInputs, DateTime? expiresUtc, Uri? origin = null)
        {
            Requires.NotNullOrEmpty(hashAlgorithmName, nameof(hashAlgorithmName));
            Requires.Argument(hash.Length > 0, nameof(hash), "Cannot be empty.");
            Requires.Argument(expiresUtc is null || expiresUtc.Value.Kind == DateTimeKind.Utc, nameof(expiresUtc), Strings.UTCTimeRequired);

            this.Location = location ?? throw new ArgumentNullException(nameof(location));
            this.ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
            this.Hash = hash;
            this.HashAlgorithmName = hashAlgorithmName;
            this.DecryptionInputs = decryptionInputs ?? throw new ArgumentNullException(nameof(decryptionInputs));
            this.ExpiresUtc = expiresUtc;
            this.Origin = origin;
        }

        /// <inheritdoc cref="PayloadReference(Uri, ContentType, ReadOnlyMemory{byte}, string, SymmetricEncryptionInputs, DateTime?, Uri?)"/>
        [MessagePack.SerializationConstructor]
        public PayloadReference(Uri location, ContentType contentType, ReadOnlyMemory<byte> hash, string hashAlgorithmName, SymmetricEncryptionInputs decryptionInputs, DateTime? expiresUtc)
            : this(location, contentType, hash, hashAlgorithmName, decryptionInputs, expiresUtc, origin: null)
        {
        }

        /// <summary>
        /// Gets the location from which the payload can be downloaded.
        /// </summary>
        [DataMember]
        public Uri Location { get; }

        /// <summary>
        /// Gets the content-type of the payload.
        /// </summary>
        [DataMember]
        public ContentType ContentType { get; }

        /// <summary>
        /// Gets the hash of the message's encrypted bytes.
        /// </summary>
        /// <remarks>
        /// This value can be used by the recipient to verify that the actual message, when downloaded,
        /// has not be altered from the author's original version.
        /// </remarks>
        [DataMember]
        public ReadOnlyMemory<byte> Hash { get; }

        /// <summary>
        /// Gets the name of the hash algorithm used to compute <see cref="Hash"/> from the raw content downloaded from <see cref="Location"/>.
        /// </summary>
        [DataMember]
        public string HashAlgorithmName { get; }

        /// <summary>
        /// Gets the hash algorithm to use for the payload.
        /// </summary>
        [IgnoreDataMember]
        public HashAlgorithm HashAlgorithm => ParseAlgorithmName(this.HashAlgorithmName);

        /// <summary>
        /// Gets the material to reconstruct the symmetric key to decrypt the referenced message.
        /// </summary>
        /// <value>The symmetric key data, or <c>null</c> if this information is not disclosed.</value>
        /// <remarks>
        /// This may be <c>null</c> when a user means to refer to some message in a conversation with
        /// another user, without inadvertently disclosing the decryption key for that message in case
        /// permission for that message had not been granted to the other user.
        /// </remarks>
        [DataMember]
        public SymmetricEncryptionInputs? DecryptionInputs { get; }

        /// <summary>
        /// Gets the time when the payload referred to is expected to be deleted.
        /// </summary>
        /// <value>
        /// The expiration date, in UTC.
        /// </value>
        [DataMember]
        public DateTime? ExpiresUtc { get; }

        /// <summary>
        /// Gets the location from which this instance was downloaded.
        /// </summary>
        [IgnoreDataMember]
        internal Uri? Origin { get; }

        /// <summary>
        /// Downloads the message payload referred to by the specified <see cref="PayloadReference"/>.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use to download the payload.</param>
        /// <param name="receivingStream">The stream to write the payload to.</param>
        /// <param name="progress">Receives progress updates on how much of the stream has been downloaded.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidMessageException">Thrown if the payload content has been changed since this reference was created.</exception>
        /// <exception cref="EndOfStreamException">Thrown if the download stream ended before it was expected to. When thrown, the hash of the content thus far was not verified.</exception>
        /// <remarks>
        /// The stream is decrypted as it is downloaded.
        /// At the conclusion of the download the hash of the stream's content is compared to the hash predicted by this reference
        /// and an <see cref="InvalidMessageException"/> is thrown if the hash does not match.
        /// </remarks>
        public async Task DownloadPayloadAsync(HttpClient httpClient, Stream receivingStream, IProgress<(long BytesTransferred, long? ExpectedLength)>? progress = null, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(httpClient, nameof(httpClient));
            Requires.NotNull(receivingStream, nameof(receivingStream));
            Verify.Operation(this.DecryptionInputs is object, Strings.PayloadDecryptionKeyMissing);

            using HttpResponseMessage responseMessage = await httpClient.GetAsync(this.Location, cancellationToken).ConfigureAwait(false);
            using Stream downloadingStream = await responseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false);

            using ICryptographicKey decryptingKey = this.DecryptionInputs.CreateKey();
            using CryptographicHash hasher = WinRTCrypto.HashAlgorithmProvider.OpenAlgorithm(this.HashAlgorithm).CreateHash();

            try
            {
                using ICryptoTransform decryptor = WinRTCrypto.CryptographicEngine.CreateDecryptor(this.DecryptionInputs.CreateKey(), this.DecryptionInputs.IV.AsOrCreateArray());
                using CryptoStream hashingDecryptingStream = CryptoStream.ReadFrom(downloadingStream, hasher, decryptor);
                await hashingDecryptingStream.ConcurrentCopyToAsync(receivingStream, progress, responseMessage.Content.Headers.ContentLength, cancellationToken).ConfigureAwait(false);
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                throw new InvalidMessageException("Error while decrypting the stream.", ex);
            }

            // Now that the content has been entirely downloaded, verify that the hash is what was expected.
            byte[] actualContentHash = hasher.GetValueAndReset();
            if (!Utilities.AreEquivalent(actualContentHash, this.Hash.Span))
            {
                throw new InvalidMessageException("The content hash for the payload does not match the expected value. Corruption or tampering has occurred.");
            }
        }

        private static HashAlgorithm ParseAlgorithmName(string name) => (HashAlgorithm)Enum.Parse(typeof(HashAlgorithm), name, ignoreCase: true);
    }
}
