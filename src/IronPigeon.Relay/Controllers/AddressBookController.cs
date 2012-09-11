namespace IronPigeon.Relay.Controllers {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Web;
	using System.Web.Mvc;
	using Microsoft;

	/// <summary>
	/// This controller serves URLs that may appear to the user, but represent the downloadable address book entry
	/// for IronPigeon communication.
	/// </summary>
#if !DEBUG
	[RequireHttps]
#endif
	public class AddressBookController : Controller {
		/// <summary>
		/// Returns the address book entry, or an HTML page for browsers.
		/// GET: /AddressBook/?blob={uri}
		/// </summary>
		/// <param name="blob">The blob address to redirect a programmatic client to.</param>
		/// <returns>The HTTP response.</returns>
		public ActionResult Index(string blob) {
			Requires.NotNullOrEmpty(blob, "blob");

			var blobUri = new Uri(blob, UriKind.Absolute);
			if (!this.Request.AcceptTypes.Contains(AddressBookEntry.ContentType) && this.Request.AcceptTypes.Contains("text/html")) {
				// This looks like a browser rather than an IronPigeon client.
				// Return an HTML page that describes what IronPigeon is.
				return this.View();
			}

			return this.Redirect(blobUri.AbsoluteUri);
		}
	}
}
