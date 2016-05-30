// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Json;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Validation;

    /// <summary>
    /// Discovers an address book entry by searching for the URL to it on the user's Twitter bio.
    /// </summary>
    public class TwitterAddressBook : OnlineAddressBook
    {
        /// <summary>
        /// The unformatted string that serves as the template for the URL that downloads user information from Twitter.
        /// </summary>
        private const string TwitterUriFormattingString = "https://api.twitter.com/1/users/show.json?screen_name={0}";

        /// <summary>
        /// A regular expression pattern that matches on URLs that are likely to point to an address book entry
        /// and includes as a URL #fragment the thumbprint of the public signing key.
        /// </summary>
        [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "StyleCop sees #fragment as a mispelled word 'ragment'")]
        private static readonly Regex AddressBookEntryWithThumbprintFragmentRegex = new Regex(@"\b(http|https|ftp)\://[a-zA-Z0-9\-\.]+(:[a-zA-Z0-9]*)?/?([a-zA-Z0-9\-\._\?\,\'/\\\+&amp;%\$\=~])*#([a-zA-Z0-9\-_]{27,43})(\b|$)", RegexOptions.IgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="TwitterAddressBook" /> class.
        /// </summary>
        public TwitterAddressBook()
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
        /// <exception cref="IronPigeon.BadAddressBookEntryException">Thrown when a validation error occurs while reading the address book entry.</exception>
        public override async Task<Endpoint> LookupAsync(string identifier, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNullOrEmpty(identifier, "identifier");
            if (!identifier.StartsWith("@"))
            {
                return null;
            }

            try
            {
                var entryLocation = await this.DiscoverAddressBookEntryUrlAsync(identifier.Substring(1), cancellationToken).ConfigureAwait(false);
                if (entryLocation == null)
                {
                    return null;
                }

                var endpoint = await this.DownloadEndpointAsync(entryLocation, cancellationToken).ConfigureAwait(false);
                return endpoint;
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        /// <summary>
        /// Searches for the URL to an IronPigeon address book entry in the specified Twitter account.
        /// </summary>
        /// <param name="twitterUsername">The Twitter account username.  It should <em>not</em> begin with an @ character.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task whose result is either the discovered URL, or <c>null</c> if none was found.</returns>
        private async Task<Uri> DiscoverAddressBookEntryUrlAsync(string twitterUsername, CancellationToken cancellationToken)
        {
            Requires.NotNullOrEmpty(twitterUsername, "identifier");

            Uri twitterUserProfileLocation = new Uri(string.Format(CultureInfo.InvariantCulture, TwitterUriFormattingString, Uri.EscapeDataString(twitterUsername)));
            using (var userProfileStream = await this.HttpClient.GetBufferedStreamAsync(twitterUserProfileLocation, cancellationToken).ConfigureAwait(false))
            {
                var jsonSerializer = new DataContractJsonSerializer(typeof(TwitterUserInfo));
                var userInfo = (TwitterUserInfo)jsonSerializer.ReadObject(userProfileStream);

                // TODO: add support for discovering the magic link from the TwitterUserInfo.WebSite property as well.
                var match = AddressBookEntryWithThumbprintFragmentRegex.Match(userInfo.Description);
                Uri addressBookEntryUrl;
                if (match.Success && Uri.TryCreate(match.Value, UriKind.Absolute, out addressBookEntryUrl))
                {
                    return addressBookEntryUrl;
                }
            }

            return null;
        }

        /// <summary>
        /// The structure of some of the data returned from a Twitter account query.
        /// </summary>
        [DataContract(Name = "user")]
        private class TwitterUserInfo
        {
            /// <summary>
            /// Gets or sets the Twitter account's Bio field.
            /// </summary>
            [DataMember(Name = "description")]
            public string Description { get; set; }

            /// <summary>
            /// Gets or sets the Twitter account's web site URL.
            /// </summary>
            [DataMember(Name = "url")]
            public string WebSite { get; set; }
        }
    }
}
