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

	using Microsoft;

	/// <summary>
	/// A blob storage provider that stores blobs to the message relay service via its well-known blob API.
	/// </summary>
	public class RelayCloudBlobStorageProvider : ICloudBlobStorageProvider {
		/// <summary>
		/// The handler to use for outbound HTTP requests.
		/// </summary>
		private HttpMessageHandler httpMessageHandler = new HttpClientHandler();

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
			this.PostUrl = postUrl;
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

		/// <summary>
		/// Gets or sets the URL to post blobs to.
		/// </summary>
		public Uri PostUrl { get; set; }

		/// <summary>
		/// Gets the HTTP client to use for outbound HTTP requests.
		/// </summary>
		protected HttpClient HttpClient { get; private set; }

		/// <summary>
		/// Uploads a blob to public cloud storage.
		/// </summary>
		/// <param name="content">The blob's content.</param>
		/// <param name="expirationUtc">The date after which this blob should be deleted.</param>
		/// <param name="contentType">The content type of the blob.</param>
		/// <param name="contentEncoding">The content encoding of the blob.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>
		/// A task whose result is the URL by which the blob's content may be accessed.
		/// </returns>
		public async Task<Uri> UploadMessageAsync(Stream content, DateTime expirationUtc, string contentType = null, string contentEncoding = null, CancellationToken cancellationToken = default(CancellationToken)) {
			var httpContent = new StreamContent(content);
			if (contentType != null) {
				httpContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
			}

			if (contentEncoding != null) {
				httpContent.Headers.ContentEncoding.Add(contentEncoding);
			}

			int lifetime = expirationUtc == DateTime.MaxValue ? int.MaxValue : (int)(expirationUtc - DateTime.UtcNow).TotalMinutes;
			var response = await this.HttpClient.PostAsync(this.PostUrl.AbsoluteUri + "?lifetimeInMinutes=" + lifetime, httpContent);
			response.EnsureSuccessStatusCode();

			var serializer = new DataContractJsonSerializer(typeof(string));
			var location = (string)serializer.ReadObject(await response.Content.ReadAsStreamAsync());
			return new Uri(location, UriKind.Absolute);
		}
	}
}
