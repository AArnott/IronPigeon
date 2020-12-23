// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using MessagePack;
    using Microsoft;

    /// <summary>
    /// Retrieves contacts from some online store.
    /// </summary>
    /// <remarks>
    /// This class does not describe a method for publishing to an address book because
    /// each address book may have different authentication requirements.
    /// Derived types are expected to be thread-safe.
    /// </remarks>
    public abstract class OnlineAddressBook : AddressBook
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OnlineAddressBook" /> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client.</param>
        protected OnlineAddressBook(HttpClient httpClient)
        {
            this.HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// Gets the HTTP client to use for outbound HTTP requests.
        /// </summary>
        public HttpClient HttpClient { get; }

        /// <summary>
        /// Downloads an address book entry from the specified URL.  No signature validation is performed.
        /// </summary>
        /// <param name="entryLocation">The location to download from.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task whose result is the downloaded address book entry.</returns>
        /// <exception cref="BadAddressBookEntryException">Thrown when deserialization of the downloaded address book entry fails.</exception>
        protected async Task<AddressBookEntry?> DownloadAddressBookEntryAsync(Uri entryLocation, CancellationToken cancellationToken)
        {
            Requires.NotNull(entryLocation, nameof(entryLocation));

            using var request = new HttpRequestMessage(HttpMethod.Get, entryLocation);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(AddressBookEntry.ContentType.MediaType!));
            HttpResponseMessage? response = await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using (Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    AddressBookEntry entry = await MessagePackSerializer.DeserializeAsync<AddressBookEntry>(stream, Utilities.MessagePackSerializerOptions, cancellationToken).ConfigureAwait(false);
                    return entry;
                }
                catch (MessagePackSerializationException ex)
                {
                    throw new BadAddressBookEntryException(ex.Message, ex);
                }
            }
        }

        /// <summary>
        /// Downloads the endpoint described by the address book entry found at the given URL and verifies the signature.
        /// </summary>
        /// <param name="entryLocation">The entry location.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task whose result is the endpoint described and signed by the address book entry.</returns>
        /// <exception cref="BadAddressBookEntryException">Thrown when deserialization or signature verification of the address book entry fails.</exception>
        protected async Task<Endpoint?> DownloadEndpointAsync(Uri entryLocation, CancellationToken cancellationToken)
        {
            Requires.NotNull(entryLocation, nameof(entryLocation));

            AddressBookEntry? entry = await this.DownloadAddressBookEntryAsync(entryLocation, cancellationToken).ConfigureAwait(false);
            if (entry is null)
            {
                return null;
            }

            Endpoint endpoint = entry.ExtractEndpoint();

            if (!string.IsNullOrEmpty(entryLocation.Fragment))
            {
                var expectedThumbprint = entryLocation.Fragment.Substring(1);
                if (!endpoint.IsThumbprintMatch(expectedThumbprint))
                {
                    throw new BadAddressBookEntryException("Fragment thumbprint mismatch.");
                }
            }

            return endpoint;
        }
    }
}
