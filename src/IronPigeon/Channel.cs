// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Mime;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using IronPigeon.Relay;
    using MessagePack;
    using Microsoft;
    using Microsoft.VisualStudio.Threading;
    using PCLCrypto;

    /// <summary>
    /// A channel for sending or receiving secure messages.
    /// </summary>
    public class Channel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Channel"/> class.
        /// </summary>
        /// <param name="httpClient">The means to make outbound requests.</param>
        /// <param name="endpoint">The endpoint of the owner who will be operating this channel.</param>
        /// <param name="cloudBlobStorage">The cloud blob storage.</param>
        /// <param name="cryptoSettings">Crypto settings to use when creating messages.</param>
        public Channel(HttpClient httpClient, OwnEndpoint endpoint, ICloudBlobStorageProvider cloudBlobStorage, CryptoSettings cryptoSettings)
        {
            this.HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this.Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            this.CloudBlobStorage = cloudBlobStorage ?? throw new ArgumentNullException(nameof(cloudBlobStorage));
            this.CryptoSettings = cryptoSettings ?? throw new ArgumentNullException(nameof(cryptoSettings));

            // Allow for long-polling. Non-long-poll requests should set shorter timeouts via the CancellationToken.
            // https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.timeout?view=netcore-3.1#remarks
            this.HttpClient.Timeout = Timeout.InfiniteTimeSpan;

            this.RelayServer = new RelayServer(httpClient, endpoint);
        }

        /// <summary>
        /// Gets the provider of blob storage.
        /// </summary>
        public ICloudBlobStorageProvider CloudBlobStorage { get; }

        /// <summary>
        /// Gets an <see cref="HttpClient"/> configured with an infinite timeout to allow for long-polling.
        /// </summary>
        /// <remarks>
        /// Non-long-poll requests should set a shorter timeout via the <see cref="CancellationToken"/> passed to those requests set to <see cref="HttpTimeout"/>
        /// as <see href="https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.timeout?view=netcore-3.1#remarks">documented here</see>.
        /// </remarks>
        public HttpClient HttpClient { get; }

        /// <summary>
        /// Gets access to the message relay server.
        /// </summary>
        public RelayServer RelayServer { get; }

        /// <summary>
        /// Gets or sets the timeout for typical HTTP requests.
        /// </summary>
        /// <value>The default value is 100 seconds.</value>
        public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(100);

        /// <summary>
        /// Gets the endpoint used to receive messages.
        /// </summary>
        /// <value>
        /// The endpoint.
        /// </value>
        public OwnEndpoint Endpoint { get; }

        /// <summary>
        /// Gets or sets the trace source to which messages are logged.
        /// </summary>
        public TraceSource? TraceSource { get; set; }

        /// <summary>
        /// Gets or sets the crypto configuration to use when creating messages.
        /// </summary>
        public CryptoSettings CryptoSettings { get; set; }

        /// <summary>
        /// Downloads messages from the server.
        /// </summary>
        /// <param name="longPoll"><c>true</c> to asynchronously wait for messages if there are none immediately available for download.</param>
        /// <param name="cancellationToken">A token whose cancellation signals lost interest in the result of this method.</param>
        /// <returns>A collection of all messages that were waiting at the time this method was invoked.</returns>
        /// <exception cref="HttpRequestException">Thrown when a connection to the server could not be established, or was terminated.</exception>
        public async IAsyncEnumerable<InboxItem> ReceiveInboxItemsAsync(bool longPoll = false, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (IncomingInboxItem incomingInboxItem in this.RelayServer.DownloadIncomingItemsAsync(longPoll, cancellationToken).ConfigureAwait(false))
            {
                InboxItem inboxItem;
                try
                {
                    InboxItemEnvelope envelope = MessagePackSerializer.Deserialize<InboxItemEnvelope>(incomingInboxItem.Envelope, Utilities.MessagePackSerializerOptions, cancellationToken);
                    inboxItem = this.OpenEnvelope(envelope, cancellationToken);
                }
                catch (MessagePackSerializationException)
                {
                    // Bad message. Throw it out.
                    await this.RelayServer.DeleteInboxItemAsync(incomingInboxItem, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                catch (InvalidMessageException)
                {
                    // Bad message. Throw it out.
                    await this.RelayServer.DeleteInboxItemAsync(incomingInboxItem, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                inboxItem.RelayServerItem = incomingInboxItem;
                yield return inboxItem;
            }
        }

        /// <summary>
        /// Sends some payload to a set of recipients.
        /// </summary>
        /// <param name="payload">The payload to transmit.</param>
        /// <param name="contentType">The type of content contained by the <paramref name="payload"/>.</param>
        /// <param name="recipients">The recipients to receive the message.</param>
        /// <param name="expiresUtc">The date after which the message may be destroyed.</param>
        /// <param name="payloadProgress">Progress in terms of payload bytes uploaded.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task representing the asynchronous operation.</returns>
        public async Task<IReadOnlyCollection<NotificationPostedReceipt>> PostAsync(Stream payload, ContentType contentType, IReadOnlyCollection<Endpoint> recipients, DateTime expiresUtc, IProgress<(long BytesTransferred, long? ExpectedLength)>? payloadProgress = null, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(payload, nameof(payload));
            Requires.NotNull(contentType, nameof(contentType));
            Requires.Argument(expiresUtc.Kind == DateTimeKind.Utc, nameof(expiresUtc), Strings.UTCTimeRequired);
            Requires.NotNullOrEmpty(recipients, nameof(recipients));

            PayloadReference? payloadReference = await this.UploadPayloadAsync(payload, contentType, expiresUtc, payloadProgress, cancellationToken).ConfigureAwait(false);
            return await this.PostPayloadReferenceAsync(payloadReference, recipients, expiresUtc, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Uploads a message to the cloud, encrypting and hashing it along the way.
        /// </summary>
        /// <param name="payload">The stream containing the payload to be shared.</param>
        /// <param name="contentType">The type of content contained by the <paramref name="payload"/>.</param>
        /// <param name="expiresUtc">The date after which the message may be destroyed.</param>
        /// <param name="progress">Receives progress in terms of number of bytes uploaded.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A reference to the uploaded payload including the decryption key and content hash.</returns>
        public async Task<PayloadReference> UploadPayloadAsync(Stream payload, ContentType contentType, DateTime expiresUtc, IProgress<(long BytesTransferred, long? ExpectedLength)>? progress = null, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(payload, nameof(payload));
            Requires.Argument(expiresUtc.Kind == DateTimeKind.Utc, nameof(expiresUtc), Strings.UTCTimeRequired);
            Verify.Operation(this.CloudBlobStorage is object, "{0} must not be null", nameof(this.CloudBlobStorage));

            cancellationToken.ThrowIfCancellationRequested();

            CryptoSettings cryptoSettings = this.CryptoSettings; // snap the mutable property into a local
            ISymmetricKeyAlgorithmProvider encryptionAlgorithm = WinRTCrypto.SymmetricKeyAlgorithmProvider.OpenAlgorithm(cryptoSettings.SymmetricEncryptionAlgorithm);
            byte[] key = WinRTCrypto.CryptographicBuffer.GenerateRandom(cryptoSettings.SymmetricKeySize / 8);
            byte[] iv = WinRTCrypto.CryptographicBuffer.GenerateRandom(encryptionAlgorithm.BlockLength);
            SymmetricEncryptionInputs encryptionInputs = new SymmetricEncryptionInputs(cryptoSettings.SymmetricEncryptionAlgorithm, key, iv);

            using CryptographicHash hasher = WinRTCrypto.HashAlgorithmProvider.OpenAlgorithm(cryptoSettings.HashAlgorithm).CreateHash();
            using ICryptographicKey encryptionKey = encryptionInputs.CreateKey();
            using ICryptoTransform encryptor = WinRTCrypto.CryptographicEngine.CreateEncryptor(encryptionKey, iv);
            using CryptoStream hashingEncryptingStream = CryptoStream.ReadFrom(payload, encryptor, hasher);

            Uri blobUri = await this.CloudBlobStorage.UploadMessageAsync(hashingEncryptingStream, expiresUtc, progress.Adapt(payload.CanSeek ? (long?)payload.Length : null), cancellationToken: cancellationToken).ConfigureAwait(false);
            byte[] hash = hasher.GetValueAndReset();
            return new PayloadReference(blobUri, contentType, hash, cryptoSettings.HashAlgorithm.GetHashAlgorithmName(), encryptionInputs, expiresUtc);
        }

        /// <summary>
        /// Shares the reference to a message payload with the specified set of recipients.
        /// </summary>
        /// <param name="payloadReference">The payload reference to share.</param>
        /// <param name="recipients">The set of recipients that should be notified of the message.</param>
        /// <param name="expiresUtc">An expiration after which the relay server may delete the notification. If unset, the <see cref="PayloadReference.ExpiresUtc"/> from the <paramref name="payloadReference"/> is used, if that is set.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task representing the asynchronous operation.</returns>
        protected virtual async Task<IReadOnlyCollection<NotificationPostedReceipt>> PostPayloadReferenceAsync(PayloadReference payloadReference, IReadOnlyCollection<Endpoint> recipients, DateTime? expiresUtc, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(payloadReference, nameof(payloadReference));
            Requires.NotNullOrEmpty(recipients, nameof(recipients));

            // Each recipient requires cryptography (CPU intensive) to be performed, so don't block the calling thread.
            await TaskScheduler.Default;

            // We'll do the CPU intensive cryptography one at a time, but once it 'yields' during network I/O, proceed to the next envelope right away.
            return await Task.WhenAll(recipients.Select(recipient => this.PostPayloadReferenceAsync(payloadReference, recipient, expiresUtc, cancellationToken))).ConfigureAwait(false);
        }

        /// <summary>
        /// Shares the reference to a message payload with the specified recipient.
        /// </summary>
        /// <param name="payloadReference">The payload reference to share.</param>
        /// <param name="recipient">The recipient that should be notified of the message.</param>
        /// <param name="expiresUtc">An expiration after which the relay server may delete the notification. If unset, the <see cref="PayloadReference.ExpiresUtc"/> from the <paramref name="payloadReference"/> is used, if that is set.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task representing the asynchronous operation.</returns>
        protected virtual async Task<NotificationPostedReceipt> PostPayloadReferenceAsync(PayloadReference payloadReference, Endpoint recipient, DateTime? expiresUtc, CancellationToken cancellationToken)
        {
            Requires.NotNull(payloadReference, nameof(payloadReference));
            Requires.NotNull(recipient, nameof(recipient));
            if (expiresUtc is null)
            {
                expiresUtc = payloadReference.ExpiresUtc;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Create the inbox item and serialize it.
            InboxItem? inboxItem = new InboxItem(DateTime.UtcNow, this.Endpoint.PublicEndpoint, recipient, payloadReference);
            byte[] serializedInboxItem = MessagePackSerializer.Serialize(inboxItem, Utilities.MessagePackSerializerOptions, cancellationToken);

            // Sign the serialized inbox item using the author's signing key.
            using ICryptographicKey signingKey = this.Endpoint.SigningKeyInputs.CreateKey();
            byte[] signature = WinRTCrypto.CryptographicEngine.Sign(signingKey, serializedInboxItem);
            SignedInboxItem signedInboxItem = new SignedInboxItem(serializedInboxItem, signature);
            byte[] serializedSignedInboxItem = MessagePackSerializer.Serialize(signedInboxItem, Utilities.MessagePackSerializerOptions, cancellationToken);

            // Generate a new symmetric key and encrypt the signed, serialized inbox.
            CryptoSettings cryptoSettings = this.CryptoSettings; // snap the mutable property to a local.
            ISymmetricKeyAlgorithmProvider algorithm = WinRTCrypto.SymmetricKeyAlgorithmProvider.OpenAlgorithm(cryptoSettings.SymmetricEncryptionAlgorithm);
            byte[] symmetricKeyMaterial = WinRTCrypto.CryptographicBuffer.GenerateRandom(cryptoSettings.SymmetricKeySize / 8);
            using ICryptographicKey symmetricKey = algorithm.CreateSymmetricKey(symmetricKeyMaterial);
            byte[] iv = WinRTCrypto.CryptographicBuffer.GenerateRandom(algorithm.BlockLength);
            byte[] encryptedSignedInboxItem = WinRTCrypto.CryptographicEngine.Encrypt(symmetricKey, serializedSignedInboxItem, iv);

            // Encrypt the symmetric key using the recipient's asymmetric key, and prepare the decryption instructions.
            using ICryptographicKey recipientEncryptionKey = recipient.EncryptionKeyInputs.CreateKey();
            byte[] encryptedKeyMaterial = WinRTCrypto.CryptographicEngine.Encrypt(recipientEncryptionKey, symmetricKeyMaterial);
            var decryptionInstructions = new SymmetricEncryptionInputs(cryptoSettings.SymmetricEncryptionAlgorithm, encryptedKeyMaterial, iv);

            // Wrap up the encrypted, signed inbox item in an envelope that includes the decryption instructions.
            InboxItemEnvelope envelope = new InboxItemEnvelope(decryptionInstructions, encryptedSignedInboxItem);
            byte[] serializedEnvelope = MessagePackSerializer.Serialize(envelope, Utilities.MessagePackSerializerOptions, cancellationToken);

            NotificationPostedReceipt receipt = await this.RelayServer.PostInboxItemAsync(recipient, serializedEnvelope, expiresUtc, cancellationToken).ConfigureAwait(false);
            return receipt;
        }

        /// <summary>
        /// Opens an envelope, decrypts its contents and verifies its authenticity.
        /// </summary>
        /// <param name="envelope">The received envelope.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The inbox item found in the envelope.</returns>
        private InboxItem OpenEnvelope(InboxItemEnvelope envelope, CancellationToken cancellationToken)
        {
            Requires.NotNull(envelope, nameof(envelope));

            // First we need to decrypt the SignedInboxItem using the key in the envelope.
            cancellationToken.ThrowIfCancellationRequested();
            using ICryptographicKey ownKey = this.Endpoint.DecryptionKeyInputs.CreateKey();
            try
            {
                SymmetricEncryptionInputs signedInboxItemDecryptingInstructions = envelope.DecryptionKey.WithKeyMaterial(
                    WinRTCrypto.CryptographicEngine.Decrypt(ownKey, envelope.DecryptionKey.KeyMaterial.AsOrCreateArray()));

                using ICryptographicKey signedInboxItemDecryptingKey = signedInboxItemDecryptingInstructions.CreateKey();
                byte[] serializedSignedInboxItem = WinRTCrypto.CryptographicEngine.Decrypt(signedInboxItemDecryptingKey, envelope.SerializedInboxItem.AsOrCreateArray(), signedInboxItemDecryptingInstructions.IV.AsOrCreateArray());

                SignedInboxItem signedInboxItem = MessagePackSerializer.Deserialize<SignedInboxItem>(serializedSignedInboxItem, Utilities.MessagePackSerializerOptions, cancellationToken);

                // Extract the InboxItem
                cancellationToken.ThrowIfCancellationRequested();
                InboxItem inboxItem = MessagePackSerializer.Deserialize<InboxItem>(signedInboxItem.SerializedInboxItem, Utilities.MessagePackSerializerOptions, cancellationToken);

                // Verify that the signature on the inbox item matches its alleged author.
                using ICryptographicKey authorSigningKey = inboxItem.Author.AuthenticatingKeyInputs.CreateKey();
                if (!WinRTCrypto.CryptographicEngine.VerifySignature(authorSigningKey, signedInboxItem.SerializedInboxItem.AsOrCreateArray(), signedInboxItem.Signature.AsOrCreateArray()))
                {
                    throw new InvalidMessageException("The signature of the InboxItem does not match its alleged author.");
                }

                // Verify that we are the intended recipient.
                if (!inboxItem.Recipient.Equals(this.Endpoint.PublicEndpoint))
                {
                    throw new InvalidMessageException("This InboxItem was not intended for this recipient.");
                }

                return inboxItem;
            }
            catch (Exception ex) when (Utilities.IsCorruptionException(ex))
            {
                throw new InvalidMessageException("The inbox item failed to deserialize, likely due to corruption or tampering.", ex);
            }
        }
    }
}
