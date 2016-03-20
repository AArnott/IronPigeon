// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Json;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Validation;

    /// <summary>
    /// Shortens URLs using the goo.gl URL shortener service.
    /// </summary>
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "StyleCop sees goo.gl as two mispelled words.")]
    public class GoogleUrlShortener : IUrlShortener
    {
        /// <summary>
        /// The URL to the goog.gl shortening service.
        /// </summary>
        [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "StyleCop sees goo.gl as two mispelled words.")]
        private static readonly Uri ShorteningService = new Uri("https://www.googleapis.com/urlshortener/v1/url");

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleUrlShortener"/> class.
        /// </summary>
        public GoogleUrlShortener()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleUrlShortener"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client.</param>
        public GoogleUrlShortener(HttpClient httpClient)
        {
            this.HttpClient = httpClient;
        }

        /// <summary>
        /// Gets or sets the HTTP client to use for outbound HTTP requests.
        /// </summary>
        public HttpClient HttpClient { get; set; }

        /// <summary>
        /// Shortens the specified long URL.
        /// </summary>
        /// <param name="longUrl">The long URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The short URL.
        /// </returns>
        public async Task<Uri> ShortenAsync(Uri longUrl, CancellationToken cancellationToken)
        {
            Requires.NotNull(longUrl, "longUrl");

            var requestSerializer = new DataContractJsonSerializer(typeof(ShortenRequest));
            var request = new ShortenRequest() { LongUrl = longUrl.AbsoluteUri };
            var requestStream = new MemoryStream();
            requestSerializer.WriteObject(requestStream, request);
            requestStream.Position = 0;
            var requestContent = new StreamContent(requestStream);
            requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var postResponse = await this.HttpClient.PostAsync(ShorteningService, requestContent, cancellationToken);

            postResponse.EnsureSuccessStatusCode();
            var responseStream = await postResponse.Content.ReadAsStreamAsync();
            var responseSerializer = new DataContractJsonSerializer(typeof(ShortenResponse));
            var response = (ShortenResponse)responseSerializer.ReadObject(responseStream);
            return new Uri(response.ShortUrl, UriKind.Absolute);
        }

        /// <summary>
        /// The request message to send to Google.
        /// </summary>
        [DataContract]
        private class ShortenRequest
        {
            /// <summary>
            /// Gets or sets the long URL to be shortened.
            /// </summary>
            [DataMember(Name = "longUrl")]
            public string LongUrl { get; set; }
        }

        /// <summary>
        /// The response message received from Google.
        /// </summary>
        [DataContract]
        private class ShortenResponse
        {
            /// <summary>
            /// Gets or sets the kind.
            /// </summary>
            /// <value>
            /// The kind.
            /// </value>
            [DataMember(Name = "kind")]
            public string Kind { get; set; }

            /// <summary>
            /// Gets or sets the short URL.
            /// </summary>
            /// <value>
            /// The short URL.
            /// </value>
            [DataMember(Name = "id")]
            public string ShortUrl { get; set; }

            /// <summary>
            /// Gets or sets the normalized long URL.
            /// </summary>
            /// <value>
            /// The long URL.
            /// </value>
            [DataMember(Name = "longUrl")]
            public string LongUrl { get; set; }
        }
    }
}
