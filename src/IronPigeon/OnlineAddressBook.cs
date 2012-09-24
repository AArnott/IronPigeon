namespace IronPigeon {
	using System;
	using System.Collections.Generic;
#if NET40
	using System.ComponentModel.Composition;
#else
	using System.Composition;
#endif
	using System.IO;
	using System.Linq;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Runtime.Serialization;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Validation;

	/// <summary>
	/// Retrieves contacts from some online store.
	/// </summary>
	/// <remarks>
	/// This class does not describe a method for publishing to an address book because
	/// each address book may have different authentication requirements.
	/// Derived types are expected to be thread-safe.
	/// </remarks>
	public abstract class OnlineAddressBook : AddressBook {
		/// <summary>
		/// Initializes a new instance of the <see cref="OnlineAddressBook"/> class.
		/// </summary>
		protected OnlineAddressBook() {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="OnlineAddressBook" /> class.
		/// </summary>
		/// <param name="httpClient">The HTTP client.</param>
		protected OnlineAddressBook(HttpClient httpClient) {
			this.HttpClient = httpClient;
		}

		/// <summary>
		/// Gets or sets the HTTP client to use for outbound HTTP requests.
		/// </summary>
		[Import]
		public HttpClient HttpClient { get; set; }

		/// <summary>
		/// Downloads an address book entry from the specified URL.  No signature validation is performed.
		/// </summary>
		/// <param name="entryLocation">The location to download from.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>A task whose result is the downloaded address book entry.</returns>
		/// <exception cref="BadAddressBookEntryException">Thrown when deserialization of the downloaded address book entry fails.</exception>
		protected async Task<AddressBookEntry> DownloadAddressBookEntryAsync(Uri entryLocation, CancellationToken cancellationToken) {
			Requires.NotNull(entryLocation, "entryLocation");

			var request = new HttpRequestMessage(HttpMethod.Get, entryLocation);
			request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(AddressBookEntry.ContentType));
			var response = await this.HttpClient.SendAsync(request, cancellationToken);
			if (!response.IsSuccessStatusCode) {
				return null;
			}

			using (var stream = await response.Content.ReadAsStreamAsync()) {
				var reader = new StreamReader(stream);
				try {
					var entry = await Utilities.DeserializeDataContractFromBase64Async<AddressBookEntry>(reader);
					return entry;
				} catch (SerializationException ex) {
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
		protected async Task<Endpoint> DownloadEndpointAsync(Uri entryLocation, CancellationToken cancellationToken) {
			Requires.NotNull(entryLocation, "entryLocation");
			Verify.Operation(this.CryptoServices != null, Strings.CryptoServicesRequired);

			var entry = await this.DownloadAddressBookEntryAsync(entryLocation, cancellationToken);
			if (entry == null) {
				return null;
			}

			var endpoint = entry.ExtractEndpoint(this.CryptoServices);

			if (!string.IsNullOrEmpty(entryLocation.Fragment)) {
				if (this.CryptoServices.CreateWebSafeBase64Thumbprint(endpoint.SigningKeyPublicMaterial) != entryLocation.Fragment.Substring(1)) {
					throw new BadAddressBookEntryException("Fragment thumbprint mismatch.");
				}
			}

			return endpoint;
		}
	}
}
