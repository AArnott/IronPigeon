// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft;

    /// <summary>
    /// An address book that resolves email addresses and email hashes
    /// via the message relay service.
    /// </summary>
    public class RelayServiceAddressBook : OnlineAddressBook
    {
        /// <summary>
        /// A regular expression that matches 64-character hex sequences (the type and length of a Microsoft account friend's hashed email).
        /// </summary>
        private static readonly Regex HashedEmailRegEx = new Regex(@"^[a-f0-9]{64}$");

        /// <summary>
        /// A regular expression that matches email addresses.
        /// </summary>
        private static readonly Regex EmailRegEx = new Regex(@"^[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+(?:[A-Z]{2}|com|org|net|edu|gov|mil|biz|info|mobi|name|aero|asia|jobs|museum)$");

        /// <summary>
        /// Gets or sets the URL that can be used to look up certificates for Dart users.
        /// </summary>
        public Uri? AddressBookLookupUrl { get; set; }

        /// <summary>
        /// Retrieves a contact with some user supplied identifier.
        /// </summary>
        /// <param name="identifier">The user-supplied identifier for the contact.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>
        /// A task whose result is the contact, or null if no match is found.
        /// </returns>
        public override async Task<Endpoint?> LookupAsync(string identifier, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return null;
            }

            Verify.Operation(this.AddressBookLookupUrl != null, "{0} not initialized", nameof(this.AddressBookLookupUrl));

            var queryString = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (HashedEmailRegEx.IsMatch(identifier))
            {
                queryString["emailHash"] = identifier;
            }
            else if (EmailRegEx.IsMatch(identifier))
            {
                queryString["email"] = identifier;
            }
            else
            {
                return null;
            }

            var builder = new UriBuilder(this.AddressBookLookupUrl);
            builder.Query = queryString.UrlEncode();
            return await this.DownloadEndpointAsync(builder.Uri, cancellationToken).ConfigureAwait(false);
        }
    }
}
