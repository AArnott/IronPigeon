// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using IronPigeon.Relay;
    using MessagePack;
    using Microsoft;
    using Microsoft.VisualStudio.Threading;
    using PCLCrypto;

    /// <summary>
    /// The personal contact information for receiving one's own messages.
    /// </summary>
    [DataContract]
    public class OwnEndpoint
    {
        private static readonly TimeSpan MaxAddressBookEntryLifetime = TimeSpan.FromDays(365 * 20);

        /// <summary>
        /// Backing field for the <see cref="PublicEndpoint"/> property.
        /// </summary>
        private Endpoint? publicEndpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="OwnEndpoint" /> class.
        /// </summary>
        /// <param name="messageReceivingEndpoint">The location where messages can be delivered to this endpoint.</param>
        /// <param name="signingKeyInputs">Instructions for verifying signed messages directed to this endpoint.</param>
        /// <param name="decryptionKeyInputs">Instructions for decrypting messages directed to this endpoint.</param>
        /// <param name="inboxOwnerCode">The secret that proves ownership of the inbox at the <paramref name="messageReceivingEndpoint"/>.</param>
        public OwnEndpoint(Uri messageReceivingEndpoint, AsymmetricKeyInputs signingKeyInputs, AsymmetricKeyInputs decryptionKeyInputs, string? inboxOwnerCode = null)
        {
            this.MessageReceivingEndpoint = messageReceivingEndpoint ?? throw new ArgumentNullException(nameof(messageReceivingEndpoint));
            this.SigningKeyInputs = signingKeyInputs ?? throw new ArgumentNullException(nameof(signingKeyInputs));
            this.DecryptionKeyInputs = decryptionKeyInputs ?? throw new ArgumentNullException(nameof(decryptionKeyInputs));
            this.InboxOwnerCode = inboxOwnerCode;

            Requires.Argument(signingKeyInputs.HasPrivateKey, nameof(signingKeyInputs), Strings.PrivateKeyDataRequired);
            Requires.Argument(decryptionKeyInputs.HasPrivateKey, nameof(decryptionKeyInputs), Strings.PrivateKeyDataRequired);
        }

        /// <summary>
        /// Gets the URL where notification messages to this recipient may be posted.
        /// </summary>
        [DataMember]
        public Uri MessageReceivingEndpoint { get; }

        /// <summary>
        /// Gets instructions for signing messages sent from this endpoint.
        /// </summary>
        [DataMember]
        public AsymmetricKeyInputs SigningKeyInputs { get; }

        /// <summary>
        /// Gets instructions for decrypting messages directed to this endpoint.
        /// </summary>
        [DataMember]
        public AsymmetricKeyInputs DecryptionKeyInputs { get; }

        /// <summary>
        /// Gets or sets the secret that proves ownership of the inbox at the <see cref="MessageReceivingEndpoint"/>.
        /// </summary>
        [DataMember]
        public string? InboxOwnerCode { get; set; }

        /// <summary>
        /// Gets a shareable public endpoint that refers to this one.
        /// </summary>
        [IgnoreDataMember]
        public Endpoint PublicEndpoint => this.publicEndpoint ?? (this.publicEndpoint = new Endpoint(this.MessageReceivingEndpoint, this.SigningKeyInputs.PublicKey, this.DecryptionKeyInputs.PublicKey));

        /// <summary>
        /// Creates a new <see cref="OwnEndpoint"/> with new asymmetric keys and a fresh inbox.
        /// </summary>
        /// <param name="cryptoSettings">Crypto settings to use when deriving asymmetric keys.</param>
        /// <param name="inboxFactory">The factory to use in creating the inbox.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task whose result is the newly generated endpoint.</returns>
        public static async Task<OwnEndpoint> CreateAsync(CryptoSettings cryptoSettings, IEndpointInboxFactory inboxFactory, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(cryptoSettings, nameof(cryptoSettings));
            Requires.NotNull(inboxFactory, nameof(inboxFactory));

            // Create the online inbox and the asymmetric keys in parallel.
            Task<InboxCreationResponse> inboxResponseTask = inboxFactory.CreateInboxAsync(cancellationToken);
            Task<(AsymmetricKeyInputs EncryptionInputs, AsymmetricKeyInputs SigningInputs)> keyGenerator = CreateAsync(cryptoSettings, cancellationToken);

            InboxCreationResponse inboxResponse = await inboxResponseTask.ConfigureAwait(false);
            (AsymmetricKeyInputs EncryptionInputs, AsymmetricKeyInputs SigningInputs) keys = await keyGenerator.ConfigureAwait(false);

            var ownContact = new OwnEndpoint(inboxResponse.MessageReceivingEndpoint, keys.SigningInputs, keys.EncryptionInputs, inboxResponse.InboxOwnerCode);
            return ownContact;
        }

        /// <summary>
        /// Saves the public information regarding this endpoint to a blob store,
        /// and returns the URL to share with others so they can send messages to this endpoint.
        /// </summary>
        /// <param name="cloudBlobStorage">The cloud blob storage to use for uploading the address book entry.</param>
        /// <param name="cancellationToken">A cancellation token to abort the publish.</param>
        /// <returns>A task whose result is the absolute URI to the address book entry.</returns>
        public async Task<Uri> PublishAddressBookEntryAsync(ICloudBlobStorageProvider cloudBlobStorage, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(cloudBlobStorage, nameof(cloudBlobStorage));

            var abe = new AddressBookEntry(this);
            byte[] serializedAddressBookEntry = MessagePackSerializer.Serialize(abe, Utilities.MessagePackSerializerOptions, cancellationToken);
            using var serializedAbeStream = new MemoryStream(serializedAddressBookEntry);
            Uri location = await cloudBlobStorage.UploadMessageAsync(serializedAbeStream, DateTime.UtcNow + MaxAddressBookEntryLifetime, contentType: AddressBookEntry.ContentType, cancellationToken: cancellationToken).ConfigureAwait(false);

            // Append a thumbprint (we use the signature) as a fragment to the URI so that those we share it with can detect if the hosted endpoint changes.
            var locationBuilder = new UriBuilder(location);
            locationBuilder.Fragment = "#" + Utilities.ToBase64WebSafe(abe.Signature.AsOrCreateArray());
            return locationBuilder.Uri;
        }

        /// <summary>
        /// Creates a pair of asymmetric keys for signing and encryption.
        /// </summary>
        /// <param name="cryptoSettings">Crypto settings to use when deriving asymmetric keys.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The newly created endpoint.</returns>
        /// <remarks>
        /// Depending on the length of the keys set in the provider and the amount of buffered entropy in the operating system,
        /// this method can take an extended period (several seconds) to complete.
        /// This method merely moves all the work to a threadpool thread.
        /// </remarks>
        private static async Task<(AsymmetricKeyInputs EncryptionInputs, AsymmetricKeyInputs SigningInputs)> CreateAsync(CryptoSettings cryptoSettings, CancellationToken cancellationToken)
        {
            Requires.NotNull(cryptoSettings, nameof(cryptoSettings));

            await TaskScheduler.Default;

            cancellationToken.ThrowIfCancellationRequested();
            IAsymmetricKeyAlgorithmProvider asymmetricEncryption = WinRTCrypto.AsymmetricKeyAlgorithmProvider.OpenAlgorithm(cryptoSettings.AsymmetricEncryptionAlgorithm);
            using ICryptographicKey? encryptionKey = asymmetricEncryption.CreateKeyPair(cryptoSettings.AsymmetricKeySize);
            AsymmetricKeyInputs encryptionKeyInputs = new AsymmetricKeyInputs(cryptoSettings.AsymmetricEncryptionAlgorithm, encryptionKey, includePrivateKey: true);

            cancellationToken.ThrowIfCancellationRequested();
            IAsymmetricKeyAlgorithmProvider asymmetricSigning = WinRTCrypto.AsymmetricKeyAlgorithmProvider.OpenAlgorithm(cryptoSettings.SigningAlgorithm);
            using ICryptographicKey signingKey = asymmetricSigning.CreateKeyPair(cryptoSettings.AsymmetricKeySize);
            AsymmetricKeyInputs signingKeyInputs = new AsymmetricKeyInputs(cryptoSettings.SigningAlgorithm, signingKey, includePrivateKey: true);

            return (encryptionKeyInputs, signingKeyInputs);
        }
    }
}
