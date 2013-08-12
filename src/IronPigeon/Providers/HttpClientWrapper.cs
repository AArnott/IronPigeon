namespace IronPigeon.Providers {
	using System;
	using System.Collections.Generic;
	using System.Composition;
	using System.Linq;
	using System.Net.Http;
	using System.Text;
	using System.Threading.Tasks;

	/// <summary>
	/// A simple MEF part that wraps an <see cref="HttpMessageHandler"/> in a new <see cref="HttpClient"/>
	/// for all importers.
	/// </summary>
	[Shared]
	[Export]
	public class HttpClientWrapper {
		/// <summary>
		/// Initializes a new instance of the <see cref="HttpClientWrapper"/> class.
		/// </summary>
		public HttpClientWrapper() {
			this.DefaultTimeout = TimeSpan.FromSeconds(10);
		}

		/// <summary>
		/// Gets or sets the default timeout for HttpClient instances produced by this part.
		/// </summary>
		public TimeSpan DefaultTimeout { get; set; }

		/// <summary>
		/// Gets a new instance of <see cref="HttpClient"/> that wraps an optionally custom <see cref="HttpMessageHandler"/>.
		/// </summary>
		[Export]
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
		[Import(AllowDefault = true)]
		public HttpMessageHandler MessageHandler { get; set; }
	}
}
