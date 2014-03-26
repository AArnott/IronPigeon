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
	using PCLCrypto;
	using Validation;
	using TaskEx = System.Threading.Tasks.Task;

	/// <summary>
	/// Creates and services <see cref="OwnEndpoint"/> instances.
	/// </summary>
	[Export]
	[Shared]
	public class OwnEndpointServices {
		private Channel channel;

		/// <summary>
		/// Gets the cryptographic services provider.
		/// </summary>
		public CryptoSettings CryptoProvider { get; set; }

		/// <summary>
		/// Gets or sets the channel.
		/// </summary>
		[Import]
		public Channel Channel {
			get { return this.channel; }
			set {
				this.channel = value;

				if (value != null) {
					this.CryptoProvider = value.CryptoServices;
				}
			}
		}

		/// <summary>
		/// Gets or sets the cloud blob storage provider.
		/// </summary>
		[Import]
		public ICloudBlobStorageProvider CloudBlobStorage { get; set; }

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
			var endpoint = await TaskEx.Run(() => this.CreateEndpointWithKeys(cancellationToken), cancellationToken);

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

			var fullLocationWithFragment = new Uri(
				location,
				"#" + this.CryptoProvider.CreateWebSafeBase64Thumbprint(endpoint.PublicEndpoint.SigningKeyPublicMaterial));
			return fullLocationWithFragment;
		}

		/// <summary>
		/// Generates a new receiving endpoint.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>
		/// The newly generated endpoint.
		/// </returns>
		/// <remarks>
		/// Depending on the length of the keys set in the provider and the amount of buffered entropy in the operating system,
		/// this method can take an extended period (several seconds) to complete.
		/// </remarks>
		private OwnEndpoint CreateEndpointWithKeys(CancellationToken cancellationToken) {
			byte[] privateSigningKey, publicSigningKey;

			cancellationToken.ThrowIfCancellationRequested();
			var encryptionAlgorithm = WinRTCrypto.AsymmetricKeyAlgorithmProvider.OpenAlgorithm(CryptoSettings.EncryptionAlgorithm);
			var encryptionKey = encryptionAlgorithm.CreateKeyPair(this.CryptoProvider.AsymmetricKeySize);
			cancellationToken.ThrowIfCancellationRequested();
			var signingAlgorithm = WinRTCrypto.AsymmetricKeyAlgorithmProvider.OpenAlgorithm(CryptoSettings.SigningAlgorithm);
			var signingKey = signingAlgorithm.CreateKeyPair(this.CryptoProvider.AsymmetricKeySize);

			var contact = new Endpoint() {
				EncryptionKeyPublicMaterial = encryptionKey.ExportPublicKey(CryptoProviderExtensions.PublicKeyFormat),
				SigningKeyPublicMaterial = signingKey.ExportPublicKey(CryptoProviderExtensions.PublicKeyFormat),
			};

			var ownContact = new OwnEndpoint(
				contact,
				signingKey.Export(CryptographicPrivateKeyBlobType.Capi1PrivateKey),
				encryptionKey.Export(CryptographicPrivateKeyBlobType.Capi1PrivateKey));
			return ownContact;
		}
	}
}
