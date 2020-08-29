// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft;
    using PCLCrypto;

    /// <summary>
    /// The personal contact information for receiving one's own messages.
    /// </summary>
    [DataContract]
    public class OwnEndpoint
    {
        /// <summary>
        /// The signing key material.
        /// </summary>
        private byte[] signingKeyMaterial;

        /// <summary>
        /// The signing key.
        /// </summary>
        private ICryptographicKey? signingKey;

        /// <summary>
        /// The encryption key material.
        /// </summary>
        private byte[] encryptionKeyMaterial;

        /// <summary>
        /// The encryption key.
        /// </summary>
        private ICryptographicKey? encryptionKey;

        /// <summary>
        /// Backing field for the <see cref="PublicEndpoint"/> property.
        /// </summary>
        private Endpoint publicEndpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="OwnEndpoint"/> class.
        /// </summary>
        /// <param name="privateKeyFormat">The private key format used.</param>
        /// <param name="signingKeyPrivateMaterial">The key material for the private key this personality uses for signing messages.</param>
        /// <param name="encryptionKeyPrivateMaterial">The key material for the private key used to decrypt messages.</param>
        /// <param name="publicEndpoint">The public information associated with this endpoint.</param>
        /// <param name="inboxOwnerCode">The secret that proves ownership of the inbox at the <see cref="Endpoint.MessageReceivingEndpoint"/>.</param>
        public OwnEndpoint(CryptographicPrivateKeyBlobType privateKeyFormat, byte[] signingKeyPrivateMaterial, byte[] encryptionKeyPrivateMaterial, Endpoint publicEndpoint, string? inboxOwnerCode)
        {
            this.PrivateKeyFormat = privateKeyFormat;
            this.signingKeyMaterial = signingKeyPrivateMaterial ?? throw new ArgumentNullException(nameof(signingKeyPrivateMaterial));
            this.encryptionKeyMaterial = encryptionKeyPrivateMaterial ?? throw new ArgumentNullException(nameof(encryptionKeyPrivateMaterial));
            this.publicEndpoint = publicEndpoint ?? throw new ArgumentNullException(nameof(publicEndpoint));
            this.InboxOwnerCode = inboxOwnerCode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OwnEndpoint" /> class.
        /// </summary>
        /// <param name="signingKey">The signing key.</param>
        /// <param name="encryptionKey">The encryption key.</param>
        /// <param name="creationDateUtc">The date this endpoint was originally created in UTC time.</param>
        /// <param name="messageReceivingEndpoint">The location where messages can be delivered to this endpoint.</param>
        /// <param name="inboxOwnerCode">The secret that proves ownership of the inbox at the <see cref="Endpoint.MessageReceivingEndpoint" />.</param>
        public OwnEndpoint(ICryptographicKey signingKey, ICryptographicKey encryptionKey, DateTime creationDateUtc, Uri messageReceivingEndpoint, string? inboxOwnerCode = null)
        {
            Requires.NotNull(signingKey, nameof(signingKey));
            Requires.NotNull(encryptionKey, nameof(encryptionKey));

            this.publicEndpoint = new Endpoint(
                creationDateUtc,
                messageReceivingEndpoint,
                signingKey.ExportPublicKey(CryptoSettings.PublicKeyFormat),
                encryptionKey.ExportPublicKey(CryptoSettings.PublicKeyFormat),
                Array.Empty<string>());

            // We could preserve the key instances, but that could make
            // our behavior a little less repeatable if we had problems
            // with key serialization.
            ////this.signingKey = signingKey;
            ////this.encryptionKey = encryptionKey;

            // Since this is a new endpoint we can choose a more modern format for the private keys.
            this.PrivateKeyFormat = CryptographicPrivateKeyBlobType.Pkcs8RawPrivateKeyInfo;
            this.signingKeyMaterial = signingKey.Export(this.PrivateKeyFormat);
            this.encryptionKeyMaterial = encryptionKey.Export(this.PrivateKeyFormat);
            this.InboxOwnerCode = inboxOwnerCode;
        }

        /// <summary>
        /// Gets the public information associated with this endpoint.
        /// </summary>
        [DataMember]
        public Endpoint PublicEndpoint
        {
            get => this.publicEndpoint;
        }

        /// <summary>
        /// Gets the private key format used.
        /// </summary>
        /// <remarks>
        /// The default is required for backward compat.
        /// </remarks>
        [DataMember]
        public CryptographicPrivateKeyBlobType PrivateKeyFormat { get; } = CryptographicPrivateKeyBlobType.Capi1PrivateKey;

        /// <summary>
        /// Gets the key material for the private key this personality uses for signing messages.
        /// </summary>
        [DataMember]
        public byte[] SigningKeyPrivateMaterial
        {
            get
            {
                return this.signingKeyMaterial;
            }
        }

        /// <summary>
        /// Gets the key material for the private key used to decrypt messages.
        /// </summary>
        [DataMember]
        public byte[] EncryptionKeyPrivateMaterial
        {
            get
            {
                return this.encryptionKeyMaterial;
            }
        }

        /// <summary>
        /// Gets the encryption key.
        /// </summary>
        public ICryptographicKey EncryptionKey
        {
            get
            {
                if (this.encryptionKey is null)
                {
                    this.encryptionKey = CryptoSettings.EncryptionAlgorithm.ImportKeyPair(
                        this.EncryptionKeyPrivateMaterial,
                        this.PrivateKeyFormat);
                }

                return this.encryptionKey;
            }
        }

        /// <summary>
        /// Gets the signing key.
        /// </summary>
        public ICryptographicKey SigningKey
        {
            get
            {
                if (this.signingKey is null)
                {
                    this.signingKey = CryptoSettings.SigningAlgorithm.ImportKeyPair(
                        this.SigningKeyPrivateMaterial,
                        this.PrivateKeyFormat);
                }

                return this.signingKey;
            }
        }

        /// <summary>
        /// Gets or sets the secret that proves ownership of the inbox at the <see cref="Endpoint.MessageReceivingEndpoint"/>.
        /// </summary>
        [DataMember]
        public string? InboxOwnerCode { get; set; }

        /// <summary>
        /// Loads endpoint information including private data from the specified stream.
        /// </summary>
        /// <param name="stream">A stream, previously serialized to using <see cref="SaveAsync"/>.</param>
        /// <returns>A task whose result is the deserialized instance of <see cref="OwnEndpoint"/>.</returns>
        public static async Task<OwnEndpoint> OpenAsync(Stream stream)
        {
            Requires.NotNull(stream, nameof(stream));

            var ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(false);   // relies on the input stream containing only the endpoint.
            ms.Position = 0;
            using (var reader = new BinaryReader(ms))
            {
                return reader.DeserializeDataContract<OwnEndpoint>();
            }
        }

        /// <summary>
        /// Creates a signed address book entry that describes the public information in this endpoint.
        /// </summary>
        /// <param name="cryptoServices">The crypto services to use for signing the address book entry.</param>
        /// <returns>The address book entry.</returns>
        public AddressBookEntry CreateAddressBookEntry(CryptoSettings cryptoServices)
        {
            Requires.NotNull(cryptoServices, nameof(cryptoServices));

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.SerializeDataContract(this.PublicEndpoint);
            writer.Flush();
            var serializedEndpoint = ms.ToArray();
            var signature = WinRTCrypto.CryptographicEngine.Sign(this.SigningKey, serializedEndpoint);
            var entry = new AddressBookEntry(serializedEndpoint, CryptoSettings.SigningAlgorithm.Algorithm.GetHashAlgorithm().ToString().ToUpperInvariant(), signature);
            return entry;
        }

        /// <summary>
        /// Saves the receiving endpoint including private data to the specified stream.
        /// </summary>
        /// <param name="target">The stream to write to.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task whose completion signals the save is complete.</returns>
        public Task SaveAsync(Stream target, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(target, nameof(target));

            var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms))
            {
                writer.SerializeDataContract(this);
                ms.Position = 0;
                return ms.CopyToAsync(target, 4096, cancellationToken);
            }
        }
    }
}
