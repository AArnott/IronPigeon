namespace IronPigeon.Providers {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Runtime.Serialization.Json;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using IronPigeon.Relay;
	using Validation;

	/// <summary>
	/// A blob storage provider that stores blobs to the message relay service via its well-known blob API.
	/// </summary>
	public class RelayCloudBlobStorageProvider : ICloudBlobStorageProvider, IEndpointInboxFactory {
		/// <summary>
		/// Initializes a new instance of the <see cref="RelayCloudBlobStorageProvider" /> class.
		/// </summary>
		public RelayCloudBlobStorageProvider() {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="RelayCloudBlobStorageProvider" /> class.
		/// </summary>
		/// <param name="postUrl">The URL to post blobs to.</param>
		public RelayCloudBlobStorageProvider(Uri postUrl) {
			Requires.NotNull(postUrl, "postUrl");
			this.BlobPostUrl = postUrl;
		}

		/// <summary>
		/// Gets or sets the URL to post blobs to.
		/// </summary>
		public Uri BlobPostUrl { get; set; }

		/// <summary>
		/// Gets or sets the base URL (without the trailing /create) of the inbox service.
		/// </summary>
		public Uri InboxServiceUrl { get; set; }

		/// <summary>
		/// Gets or sets the HTTP client to use for outbound HTTP requests.
		/// </summary>
		public HttpClient HttpClient { get; set; }

		/// <inheritdoc/>
		public async Task<Uri> UploadMessageAsync(Stream content, DateTime expirationUtc, string contentType = null, string contentEncoding = null, IProgress<int> bytesCopiedProgress = null, CancellationToken cancellationToken = default(CancellationToken)) {
			var httpContent = new StreamContent(content.ReadStreamWithProgress(bytesCopiedProgress));
			if (contentType != null) {
				httpContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
			}

			if (contentEncoding != null) {
				httpContent.Headers.ContentEncoding.Add(contentEncoding);
			}

			int lifetime = expirationUtc == DateTime.MaxValue ? int.MaxValue : (int)(expirationUtc - DateTime.UtcNow).TotalMinutes;
			var response = await this.HttpClient.PostAsync(this.BlobPostUrl.AbsoluteUri + "?lifetimeInMinutes=" + lifetime, httpContent);
			response.EnsureSuccessStatusCode();

			var serializer = new DataContractJsonSerializer(typeof(string));
			var location = (string)serializer.ReadObject(await response.Content.ReadAsStreamAsync());
			return new Uri(location, UriKind.Absolute);
		}

		/// <summary>
		/// Creates an inbox at a message relay service.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>
		/// The result of the inbox creation request from the server.
		/// </returns>
		public async Task<InboxCreationResponse> CreateInboxAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			var registerUrl = new Uri(this.InboxServiceUrl, "create");

			var responseMessage =
				await this.HttpClient.PostAsync(registerUrl, null, cancellationToken);
			responseMessage.EnsureSuccessStatusCode();
			using (var responseStream = await responseMessage.Content.ReadAsStreamAsync()) {
				var deserializer = new DataContractJsonSerializer(typeof(InboxCreationResponse));
				var creationResponse = (InboxCreationResponse)deserializer.ReadObject(responseStream);
				return creationResponse;
			}
		}
	}
}
