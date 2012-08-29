namespace IronPigeon.Providers {
	using System;
	using System.Collections.Generic;
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
	using Microsoft;

	/// <summary>
	/// Discovers an address book entry by searching for the URL to it on the user's Twitter bio.
	/// </summary>
	public class TwitterAddressBook : OnlineAddressBook {
		/// <summary>
		/// The unformatted string that serves as the template for the URL that downloads user information from Twitter.
		/// </summary>
		private const string TwitterUriFormattingString = "https://api.twitter.com/1/users/show.json?screen_name={0}";

		/// <summary>
		/// A regular expression pattern that matches on URLs that are likely to point to an address book entry
		/// and includes as a URL #fragment the thumbprint of the public signing key.
		/// </summary>
		private static readonly Regex AddressBookEntryWithThumbprintFragmentRegex = new Regex(@"\b(http|https|ftp)\://[a-zA-Z0-9\-\.]+(:[a-zA-Z0-9]*)?/?([a-zA-Z0-9\-\._\?\,\'/\\\+&amp;%\$\=~])*#([a-zA-Z0-9\-_]{27,43})(\b|$)", RegexOptions.IgnoreCase);

		/// <summary>
		/// Initializes a new instance of the <see cref="TwitterAddressBook" /> class.
		/// </summary>
		public TwitterAddressBook() {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TwitterAddressBook" /> class.
		/// </summary>
		/// <param name="cryptoProvider">The crypto provider.</param>
		public TwitterAddressBook(ICryptoProvider cryptoProvider) {
			this.CryptoServices = cryptoProvider;
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
		public override async Task<Endpoint> LookupAsync(string identifier, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNullOrEmpty(identifier, "identifier");
			if (!identifier.StartsWith("@")) {
				return null;
			}

			try {
				var entryLocation = await this.DiscoverAddressBookEntryUrlAsync(identifier.Substring(1), cancellationToken);
				if (entryLocation == null) {
					return null;
				}

				var endpoint = await this.DownloadEndpointAsync(entryLocation, cancellationToken);
				return endpoint;
			} catch (HttpRequestException) {
				return null;
			}
		}

		private async Task<Uri> DiscoverAddressBookEntryUrlAsync(string identifier, CancellationToken cancellationToken) {
			Uri twitterUserProfileLocation = new Uri(string.Format(CultureInfo.InvariantCulture, TwitterUriFormattingString, Uri.EscapeDataString(identifier)));
			using (var userProfileStream = await this.HttpClient.GetBufferedStreamAsync(twitterUserProfileLocation, cancellationToken)) {
				var jsonSerializer = new DataContractJsonSerializer(typeof(TwitterUserInfo));
				var userInfo = (TwitterUserInfo)jsonSerializer.ReadObject(userProfileStream);

				// TODO: add support for discovering the magic link from the TwitterUserInfo.WebSite property as well.
				var match = AddressBookEntryWithThumbprintFragmentRegex.Match(userInfo.Description);
				Uri addressBookEntryUrl;
				if (match.Success && Uri.TryCreate(match.Value, UriKind.Absolute, out addressBookEntryUrl)) {
					return addressBookEntryUrl;
				}
			}

			return null;
		}

		[DataContract(Name = "user")]
		private class TwitterUserInfo {
			[DataMember(Name = "description")]
			public string Description { get; set; }

			[DataMember(Name = "url")]
			public string WebSite { get; set; }
		}
	}
}
