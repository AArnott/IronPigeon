﻿// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Providers
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft;

    /// <summary>
    /// An address book whose identifiers are URLs to the online address book entries.
    /// </summary>
    public class DirectEntryAddressBook : OnlineAddressBook
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DirectEntryAddressBook" /> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client.</param>
        public DirectEntryAddressBook(HttpClient httpClient)
            : base(httpClient)
        {
        }

        /// <summary>
        /// Retrieves a contact with some user supplied identifier.
        /// </summary>
        /// <param name="identifier">The user-supplied identifier for the contact.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>
        /// A task whose result is the contact, or null if no match is found.
        /// </returns>
        public override async Task<Endpoint?> LookupAsync(string identifier, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(identifier, nameof(identifier));

            if (!Uri.TryCreate(identifier, UriKind.Absolute, out Uri? entryLocation))
            {
                return null;
            }

            try
            {
                Endpoint? endpoint = await this.DownloadEndpointAsync(entryLocation, cancellationToken).ConfigureAwait(false);
                return endpoint;
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (BadAddressBookEntryException)
            {
                return null;
            }
        }
    }
}
