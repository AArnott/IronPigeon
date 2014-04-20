namespace IronPigeon.Providers {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net.Http;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	using Validation;

	/// <summary>
	/// An address book whose identifiers are URLs to the online address book entries.
	/// </summary>
	public class DirectEntryAddressBook : OnlineAddressBook {
		/// <summary>
		/// Initializes a new instance of the <see cref="DirectEntryAddressBook" /> class.
		/// </summary>
		public DirectEntryAddressBook() {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DirectEntryAddressBook" /> class.
		/// </summary>
		/// <param name="httpClient">The HTTP client.</param>
		public DirectEntryAddressBook(HttpClient httpClient)
			: base(httpClient) {
		}

		/// <summary>
		/// Retrieves a contact with some user supplied identifier.
		/// </summary>
		/// <param name="identifier">The user-supplied identifier for the contact.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>
		/// A task whose result is the contact, or null if no match is found.
		/// </returns>
		public override async Task<Endpoint> LookupAsync(string identifier, CancellationToken cancellationToken = default(CancellationToken)) {
			Uri entryLocation;
			if (!Uri.TryCreate(identifier, UriKind.Absolute, out entryLocation)) {
				return null;
			}

			try {
				var endpoint = await this.DownloadEndpointAsync(entryLocation, cancellationToken);
				return endpoint;
			} catch (HttpRequestException) {
				return null;
			} catch (BadAddressBookEntryException) {
				return null;
			}
		}
	}
}
