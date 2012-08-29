namespace IronPigeon.Relay.Controllers {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Web;
	using System.Web.Mvc;
	using Microsoft;

	public class AddressBookController : Controller {
		// GET: /AddressBook/?blob={uri}
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
