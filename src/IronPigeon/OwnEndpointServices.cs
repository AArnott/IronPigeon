namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Composition;
	using System.IO;
	using System.Linq;
	using System.Net.Http;
	using System.Runtime.Serialization.Json;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using IronPigeon.Relay;
	using Validation;
	using TaskEx = System.Threading.Tasks.Task;

	/// <summary>
	/// Creates and services <see cref="OwnEndpoint"/> instances.
	/// </summary>
	[Export]
	[Shared]
	public class OwnEndpointServices {
		/// <summary>
		/// Gets or sets the crypto provider.
		/// </summary>
		[Import]
		public ICryptoProvider CryptoProvider { get; set; }

		/// <summary>
		/// Gets or sets the cloud blob storage provider.
		/// </summary>
		[Import]
		public ICloudBlobStorageProvider CloudBlobStorage { get; set; }

		/// <summary>
		/// Gets or sets the URL shortener.
		/// </summary>
		[Import(AllowDefault = true)]
		public IUrlShortener UrlShortener { get; set; }

		/// <summary>
		/// Gets or sets the HTTP client.
		/// </summary>
		[Import]
		public HttpClient HttpClient { get; set; }

		/// <summary>
		/// Gets or sets the service that creates new inboxes on a message relay.
		/// </summary>
		[Import]
		public IEndpointInboxFactory EndpointInboxFactory { get; set; }

		/// <summary>
		/// Generates a new receiving endpoint.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>A task whose result is the newly generated endpoint.</returns>
		/// <remarks>
		/// Depending on the length of the keys set in the provider and the amount of buffered entropy in the operating system,
		/// this method can take an extended period (several seconds) to complete.
		/// This method merely moves all the work to a threadpool thread.
		/// </remarks>
		public async Task<OwnEndpoint> CreateAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			// Create new key pairs.
			var endpoint = await TaskEx.Run(() => this.CreateEndpointWithKeys(), cancellationToken);

			// Set up the inbox on a message relay.
			var inboxResponse = await this.EndpointInboxFactory.CreateInboxAsync(cancellationToken);
			endpoint.PublicEndpoint.MessageReceivingEndpoint = new Uri(inboxResponse.MessageReceivingEndpoint, UriKind.Absolute);
			endpoint.InboxOwnerCode = inboxResponse.InboxOwnerCode;

			return endpoint;
		}

		/// <summary>
		/// Saves the information required to send this channel messages to the blob store,
		/// and returns the URL to share with senders.
		/// </summary>
		/// <param name="endpoint">The endpoint for which an address book entry should be created and published.</param>
		/// <param name="cancellationToken">A cancellation token to abort the publish.</param>
		/// <returns>A task whose result is the absolute URI to the address book entry.</returns>
		public async Task<Uri> PublishAddressBookEntryAsync(OwnEndpoint endpoint, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(endpoint, "endpoint");

			var abe = endpoint.CreateAddressBookEntry(this.CryptoProvider);
			var abeWriter = new StringWriter();
			await Utilities.SerializeDataContractAsBase64Async(abeWriter, abe);
			var ms = new MemoryStream(Encoding.UTF8.GetBytes(abeWriter.ToString()));
			var location = await this.CloudBlobStorage.UploadMessageAsync(ms, DateTime.MaxValue, AddressBookEntry.ContentType, cancellationToken: cancellationToken);
			if (this.UrlShortener != null) {
				location = await this.UrlShortener.ShortenAsync(location);
			}

			var fullLocationWithFragment = new Uri(
				location,
				"#" + this.CryptoProvider.CreateWebSafeBase64Thumbprint(endpoint.PublicEndpoint.SigningKeyPublicMaterial));
			return fullLocationWithFragment;
		}

		/// <summary>
		/// Generates a new receiving endpoint.
		/// </summary>
		/// <returns>The newly generated endpoint.</returns>
		/// <remarks>
		/// Depending on the length of the keys set in the provider and the amount of buffered entropy in the operating system,
		/// this method can take an extended period (several seconds) to complete.
		/// </remarks>
		private OwnEndpoint CreateEndpointWithKeys() {
			byte[] privateEncryptionKey, publicEncryptionKey;
			byte[] privateSigningKey, publicSigningKey;

			this.CryptoProvider.GenerateEncryptionKeyPair(out privateEncryptionKey, out publicEncryptionKey);
			this.CryptoProvider.GenerateSigningKeyPair(out privateSigningKey, out publicSigningKey);

			var contact = new Endpoint() {
				EncryptionKeyPublicMaterial = publicEncryptionKey,
				SigningKeyPublicMaterial = publicSigningKey,
				HashAlgorithmName = this.CryptoProvider.HashAlgorithmName,
			};

			var ownContact = new OwnEndpoint(contact, privateSigningKey, privateEncryptionKey);
			return ownContact;
		}
	}
}
