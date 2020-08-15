// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using IronPigeon.Relay;
    using Microsoft;
    using PCLCrypto;
    using TaskEx = System.Threading.Tasks.Task;

    /// <summary>
    /// Creates and services <see cref="OwnEndpoint"/> instances.
    /// </summary>
    public class OwnEndpointServices
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OwnEndpointServices"/> class.
        /// </summary>
        public OwnEndpointServices()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OwnEndpointServices"/> class.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="cloudBlobStorage">The cloud blob storage provider.</param>
        /// <param name="httpClient">The HTTP client.</param>
        /// <param name="endpointInboxFactory">The endpoint inbox factory.</param>
        public OwnEndpointServices(Channel channel, ICloudBlobStorageProvider cloudBlobStorage, HttpClient httpClient, IEndpointInboxFactory endpointInboxFactory)
        {
            this.Channel = channel;
            this.CloudBlobStorage = cloudBlobStorage;
            this.HttpClient = httpClient;
            this.EndpointInboxFactory = endpointInboxFactory;
        }

        /// <summary>
        /// Gets the cryptographic services provider.
        /// </summary>
        public CryptoSettings CryptoProvider
        {
            get { return this.Channel.CryptoServices; }
        }

        /// <summary>
        /// Gets or sets the channel.
        /// </summary>
        public Channel? Channel { get; set; }

        /// <summary>
        /// Gets or sets the cloud blob storage provider.
        /// </summary>
        public ICloudBlobStorageProvider? CloudBlobStorage { get; set; }

        /// <summary>
        /// Gets or sets the HTTP client.
        /// </summary>
        public HttpClient? HttpClient { get; set; }

        /// <summary>
        /// Gets or sets the service that creates new inboxes on a message relay.
        /// </summary>
        public IEndpointInboxFactory? EndpointInboxFactory { get; set; }

        /// <summary>
        /// Generates a new receiving endpoint.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task whose result is the newly generated endpoint.</returns>
        /// <remarks>
        /// Depending on the length of the keys set in the provider and the amount of buffered entropy in the operating system,
        /// this method can take an extended period (several seconds) to complete.
        /// This method merely moves all the work to a threadpool thread.
        /// </remarks>
        public async Task<OwnEndpoint> CreateAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            // Create new key pairs.
            OwnEndpoint? endpoint = await TaskEx.Run(() => this.CreateEndpointWithKeys(cancellationToken), cancellationToken).ConfigureAwait(false);

            // Set up the inbox on a message relay.
            InboxCreationResponse? inboxResponse = await this.EndpointInboxFactory.CreateInboxAsync(cancellationToken).ConfigureAwait(false);
            endpoint.PublicEndpoint.MessageReceivingEndpoint = new Uri(inboxResponse.MessageReceivingEndpoint, UriKind.Absolute);
            endpoint.InboxOwnerCode = inboxResponse.InboxOwnerCode;

            return endpoint;
        }

        /// <summary>
        /// Saves the information required to send this channel messages to the blob store,
        /// and returns the URL to share with senders.
        /// </summary>
        /// <param name="endpoint">The endpoint for which an address book entry should be created and published.</param>
        /// <param name="cancellationToken">A cancellation token to abort the publish.</param>
        /// <returns>A task whose result is the absolute URI to the address book entry.</returns>
        public async Task<Uri> PublishAddressBookEntryAsync(OwnEndpoint endpoint, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(endpoint, nameof(endpoint));

            AddressBookEntry? abe = endpoint.CreateAddressBookEntry(this.CryptoProvider);
            using var abeWriter = new StringWriter();
            await Utilities.SerializeDataContractAsBase64Async(abeWriter, abe).ConfigureAwait(false);
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(abeWriter.ToString()));
            Uri? location = await this.CloudBlobStorage.UploadMessageAsync(ms, DateTime.MaxValue, AddressBookEntry.ContentType, cancellationToken: cancellationToken).ConfigureAwait(false);

            var fullLocationWithFragment = new Uri(
                location,
                "#" + this.CryptoProvider.CreateWebSafeBase64Thumbprint(endpoint.PublicEndpoint.SigningKeyPublicMaterial));
            return fullLocationWithFragment;
        }

        /// <summary>
        /// Generates a new receiving endpoint.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The newly generated endpoint.
        /// </returns>
        /// <remarks>
        /// Depending on the length of the keys set in the provider and the amount of buffered entropy in the operating system,
        /// this method can take an extended period (several seconds) to complete.
        /// </remarks>
        private OwnEndpoint CreateEndpointWithKeys(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ICryptographicKey? encryptionKey = CryptoSettings.EncryptionAlgorithm.CreateKeyPair(this.CryptoProvider.AsymmetricKeySize);
            cancellationToken.ThrowIfCancellationRequested();
            ICryptographicKey? signingKey = CryptoSettings.SigningAlgorithm.CreateKeyPair(this.CryptoProvider.AsymmetricKeySize);

            var ownContact = new OwnEndpoint(signingKey, encryptionKey);
            return ownContact;
        }
    }
}
