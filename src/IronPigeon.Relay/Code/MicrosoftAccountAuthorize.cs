namespace IronPigeon.Relay {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Web;
	using System.Web.Mvc;

	/// <summary>
	/// Verifies that the bearer token 
	/// </summary>
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
	public class MicrosoftAccountAuthorizeAttribute : AuthorizeAttribute {
		protected override bool AuthorizeCore(HttpContextBase httpContext) {
			// It's VERY IMPORTANT to also verify that the access token we were given
			// was issued to the Dart app.  Otherwise, any arbitrary (web or Win8) app
			// that logs a user in by their Microsoft account will have an access token
			// they can then send to this service to replace their address book entry.
			//// TODO: code here

			return true;
		}
	}
}