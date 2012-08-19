namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net.Http;
	using System.Runtime.Serialization;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft;

	public abstract class OnlineAddressBook : AddressBook {
		private HttpMessageHandler httpMessageHandler = new HttpClientHandler();

		/// <summary>
		/// Initializes a new instance of the <see cref="OnlineAddressBook"/> class.
		/// </summary>
		protected OnlineAddressBook() {
			this.HttpClient = new HttpClient(this.httpMessageHandler);
		}

		/// <summary>
		/// Gets or sets the message handler to use for outbound HTTP requests.
		/// </summary>
		public HttpMessageHandler HttpMessageHandler {
			get {
				return this.httpMessageHandler;
			}

			set {
				Requires.NotNull(value, "value");
				this.httpMessageHandler = value;
				this.HttpClient = new HttpClient(value);
			}
		}

		protected HttpClient HttpClient { get; private set; }

		protected async Task<AddressBookEntry> DownloadAddressBookEntryAsync(Uri entryLocation, CancellationToken cancellationToken) {
			Requires.NotNull(entryLocation, "entryLocation");

			using (var stream = await this.HttpClient.GetBufferedStreamAsync(entryLocation, cancellationToken)) {
				var reader = new StreamReader(stream);
				try {
					var entry = await Utilities.DeserializeDataContractFromBase64Async<AddressBookEntry>(reader);
					return entry;
				} catch (SerializationException ex) {
					throw new BadAddressBookEntryException(ex.Message, ex);
				}
			}
		}

		protected async Task<Endpoint> DownloadEndpointAsync(Uri entryLocation, CancellationToken cancellationToken) {
			Requires.NotNull(entryLocation, "entryLocation");
			Verify.Operation(this.CryptoServices != null, Strings.CryptoServicesRequired);

			var entry = await this.DownloadAddressBookEntryAsync(entryLocation, cancellationToken);
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
