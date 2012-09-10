namespace IronPigeon.Providers {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net.Http;
	using System.Text;
	using System.Threading.Tasks;
	using Microsoft;
	using System.Runtime.Serialization.Json;
	using System.Runtime.Serialization;
	using System.IO;
	using System.Net.Http.Headers;

	/// <summary>
	/// Shortens URLs using the goo.gl URL shortener service.
	/// </summary>
	public class GoogleUrlShortener : IUrlShortener {
		private static readonly Uri ShorteningService = new Uri("https://www.googleapis.com/urlshortener/v1/url");

		private HttpMessageHandler httpMessageHandler = new HttpClientHandler();

		/// <summary>
		/// Initializes a new instance of the <see cref="GoogleUrlShortener"/> class.
		/// </summary>
		public GoogleUrlShortener() {
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
		/// Gets the HTTP client to use for outbound HTTP requests.
		/// </summary>
		protected HttpClient HttpClient { get; private set; }

		public async Task<Uri> ShortenAsync(Uri longUrl) {
			Requires.NotNull(longUrl, "longUrl");

			var requestSerializer = new DataContractJsonSerializer(typeof(ShortenRequest));
			var request = new ShortenRequest() { LongUrl = longUrl.AbsoluteUri };
			var requestStream = new MemoryStream();
			requestSerializer.WriteObject(requestStream, request);
			requestStream.Position = 0;
			var requestContent = new StreamContent(requestStream);
			requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

			var postResponse = await this.HttpClient.PostAsync(ShorteningService, requestContent);

			postResponse.EnsureSuccessStatusCode();
			var responseStream = await postResponse.Content.ReadAsStreamAsync();
			var responseSerializer = new DataContractJsonSerializer(typeof(ShortenResponse));
			var response = (ShortenResponse)responseSerializer.ReadObject(responseStream);
			return new Uri(response.ShortUrl, UriKind.Absolute);
		}

		[DataContract]
		private class ShortenRequest {
			[DataMember(Name = "longUrl")]
			public string LongUrl { get; set; }
		}

		[DataContract]
		private class ShortenResponse {
			[DataMember(Name = "kind")]
			public string Kind { get; set; }

			[DataMember(Name = "id")]
			public string ShortUrl { get; set; }

			[DataMember(Name = "longUrl")]
			public string LongUrl { get; set; }
		}
	}
}
