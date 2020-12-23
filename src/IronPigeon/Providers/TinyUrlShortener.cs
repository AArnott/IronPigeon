// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft;

    /// <summary>
    /// A URL shortener that uses tinyurl.com.
    /// </summary>
    public class TinyUrlShortener : IUrlShortener
    {
        /// <summary>
        /// The template for the URL of the service that takes a long URL and returns a short one.
        /// </summary>
        protected const string ShorteningService = "http://tinyurl.com/api-create.php?url={0}";

        /// <summary>
        /// Initializes a new instance of the <see cref="TinyUrlShortener"/> class.
        /// </summary>
        public TinyUrlShortener()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TinyUrlShortener"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client.</param>
        public TinyUrlShortener(HttpClient httpClient)
        {
            this.HttpClient = httpClient;
        }

        /// <summary>
        /// Gets or sets the HTTP client to use for outbound HTTP requests.
        /// </summary>
        public HttpClient? HttpClient { get; set; }

        /// <summary>
        /// Shortens the asynchronous.
        /// </summary>
        /// <param name="longUrl">The long URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task whose result is the shortened URL.</returns>
        public async Task<Uri> ShortenAsync(Uri longUrl, CancellationToken cancellationToken)
        {
            Requires.NotNull(longUrl, nameof(longUrl));
            Verify.Operation(this.HttpClient is object, Strings.PropertyMustBeSetFirst, nameof(this.HttpClient));

            if (longUrl.Host == "tinyurl.com")
            {
                // already shortened.
                return longUrl;
            }

            Uri shorteningRequestUrl = new Uri(string.Format(
                CultureInfo.InvariantCulture, ShorteningService, Uri.EscapeDataString(longUrl.AbsoluteUri)));
            HttpResponseMessage? response = await this.HttpClient.GetAsync(shorteningRequestUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string responseAsString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new Uri(responseAsString, UriKind.Absolute);
        }
    }
}
