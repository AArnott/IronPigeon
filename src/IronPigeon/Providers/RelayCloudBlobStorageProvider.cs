// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Providers
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using IronPigeon.Relay;
    using Microsoft;
    using Newtonsoft.Json;

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
        /// Gets or sets the URL to POST to in order to create a new inbox.
        /// </summary>
        public Uri? InboxFactoryUrl { get; set; }

        /// <summary>
        /// Gets the HTTP client to use for outbound HTTP requests.
        /// </summary>
        public HttpClient HttpClient { get; }

        /// <inheritdoc/>
        public async Task<Uri> UploadMessageAsync(Stream content, DateTime expirationUtc, MediaTypeHeaderValue? contentType, IProgress<long>? bytesCopiedProgress = null, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(content, nameof(content));
            Verify.Operation(this.BlobPostUrl is object, Strings.PropertyMustBeSetFirst, nameof(this.BlobPostUrl));

            using var httpContent = new StreamContent(content.ReadStreamWithProgress(bytesCopiedProgress))
            {
                Headers =
                {
                    ContentType = contentType,
                    ContentLength = content.CanSeek ? (long?)content.Length : null,
                },
            };

            int lifetime = expirationUtc == DateTime.MaxValue ? int.MaxValue : (int)(expirationUtc - DateTime.UtcNow).TotalMinutes;
            HttpResponseMessage? response = await this.HttpClient.PostAsync(this.BlobPostUrl.OriginalString + "?lifetimeInMinutes=" + lifetime, httpContent, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            return response.Headers.Location;
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
            Verify.Operation(this.InboxFactoryUrl is object, Strings.PropertyMustBeSetFirst, nameof(this.InboxFactoryUrl));

            HttpResponseMessage responseMessage =
                await this.HttpClient.PostAsync(this.InboxFactoryUrl, null, cancellationToken).ConfigureAwait(false);
            responseMessage.EnsureSuccessStatusCode();
            string json = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
            InboxCreationResponse creationResponse = JsonConvert.DeserializeObject<InboxCreationResponse>(json);
            return creationResponse;
        }
    }
}
