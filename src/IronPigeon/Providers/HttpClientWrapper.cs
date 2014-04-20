namespace IronPigeon.Providers {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net.Http;
	using System.Text;
	using System.Threading.Tasks;

	/// <summary>
	/// A simple MEF part that wraps an <see cref="HttpMessageHandler"/> in a new <see cref="HttpClient"/>
	/// for all importers.
	/// </summary>
	public class HttpClientWrapper {
		/// <summary>
		/// The default timeout.
		/// </summary>
		public static readonly TimeSpan DefaultTimeoutInitValue = TimeSpan.FromSeconds(10);

		/// <summary>
		/// Initializes a new instance of the <see cref="HttpClientWrapper"/> class.
		/// </summary>
		public HttpClientWrapper() {
			this.DefaultTimeout = DefaultTimeoutInitValue;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="HttpClientWrapper"/> class.
		/// </summary>
		/// <param name="messageHandler">The message handler.</param>
		public HttpClientWrapper(HttpMessageHandler messageHandler) {
			this.MessageHandler = messageHandler;
		}

		/// <summary>
		/// Gets or sets the default timeout for HttpClient instances produced by this part.
		/// </summary>
		public TimeSpan DefaultTimeout { get; set; }

		/// <summary>
		/// Gets a new instance of <see cref="HttpClient"/> that wraps an optionally custom <see cref="HttpMessageHandler"/>.
		/// </summary>
		public HttpClient Client {
			get {
				return new HttpClient(this.MessageHandler ?? new HttpClientHandler()) {
					Timeout = this.DefaultTimeout
				};
			}
		}

		/// <summary>
		/// Gets or sets a custom <see cref="HttpMessageHandler"/>.
		/// </summary>
		public HttpMessageHandler MessageHandler { get; set; }
	}
}
