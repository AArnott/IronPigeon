namespace IronPigeon.Providers {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net.Http;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	using Microsoft;

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
		/// <param name="cryptoProvider">The crypto provider.</param>
		public DirectEntryAddressBook(ICryptoProvider cryptoProvider) {
			this.CryptoServices = cryptoProvider;
		}

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
			}
		}
	}
}
