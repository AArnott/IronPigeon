// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Providers
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Runtime.Serialization.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using IronPigeon.Relay;
    using Microsoft;

    /// <summary>
    /// A blob storage provider that stores blobs to the message relay service via its well-known blob API.
    /// </summary>
    public class RelayCloudBlobStorageProvider : ICloudBlobStorageProvider, IEndpointInboxFactory
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RelayCloudBlobStorageProvider"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client.</param>
        public RelayCloudBlobStorageProvider(HttpClient httpClient)
        {
            this.HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// Gets or sets the URL to post blobs to.
        /// </summary>
        public Uri? BlobPostUrl { get; set; }

        /// <summary>
        /// Gets or sets the base URL (without the trailing /create) of the inbox service.
        /// </summary>
        public Uri? InboxServiceUrl { get; set; }

        /// <summary>
        /// Gets the HTTP client to use for outbound HTTP requests.
        /// </summary>
        public HttpClient HttpClient { get; }

        /// <inheritdoc/>
        public async Task<Uri> UploadMessageAsync(Stream content, DateTime expirationUtc, IProgress<long>? bytesCopiedProgress = null, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(content, nameof(content));
            Verify.Operation(this.BlobPostUrl is object, Strings.PropertyMustBeSetFirst, nameof(this.BlobPostUrl));

            using var httpContent = new StreamContent(content.ReadStreamWithProgress(bytesCopiedProgress));
            if (content.CanSeek)
            {
                httpContent.Headers.ContentLength = content.Length;
            }

            int lifetime = expirationUtc == DateTime.MaxValue ? int.MaxValue : (int)(expirationUtc - DateTime.UtcNow).TotalMinutes;
            HttpResponseMessage? response = await this.HttpClient.PostAsync(this.BlobPostUrl.AbsoluteUri + "?lifetimeInMinutes=" + lifetime, httpContent, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var serializer = new DataContractJsonSerializer(typeof(string));
            var location = (string)serializer.ReadObject(await response.Content.ReadAsStreamAsync().ConfigureAwait(false));
            return new Uri(location, UriKind.Absolute);
        }

        /// <summary>
        /// Creates an inbox at a message relay service.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The result of the inbox creation request from the server.
        /// </returns>
        public async Task<InboxCreationResponse> CreateInboxAsync(CancellationToken cancellationToken = default)
        {
            Verify.Operation(this.InboxServiceUrl is object, $"{nameof(this.InboxServiceUrl)} must be set first.");
            Verify.Operation(this.InboxServiceUrl is object, Strings.PropertyMustBeSetFirst, nameof(this.InboxServiceUrl));

            var registerUrl = new Uri(this.InboxServiceUrl, "create");

            HttpResponseMessage? responseMessage =
                await this.HttpClient.PostAsync(registerUrl, null, cancellationToken).ConfigureAwait(false);
            responseMessage.EnsureSuccessStatusCode();
            using (Stream? responseStream = await responseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                var deserializer = new DataContractJsonSerializer(typeof(InboxCreationResponse));
                var creationResponse = (InboxCreationResponse)deserializer.ReadObject(responseStream);
                return creationResponse;
            }
        }
    }
}
