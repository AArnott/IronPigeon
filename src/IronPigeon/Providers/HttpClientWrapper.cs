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
	public class HttpClientWrapper {
		/// <summary>
		/// Gets a new instance of <see cref="HttpClient"/> that wraps an optionally custom <see cref="HttpMessageHandler"/>.
		/// </summary>
		[Export]
		public HttpClient Client {
			get {
				return new HttpClient(this.MessageHandler ?? new HttpClientHandler()) {
					Timeout = TimeSpan.FromSeconds(5)
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
