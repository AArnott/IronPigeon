namespace IronPigeon.Relay {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Web;
	using System.Web.Mvc;

	public class PrivacyController : Controller {
		/// <summary>
		/// GET: /Privacy/
		/// </summary>
		/// <returns>The web result.</returns>
		public ActionResult Index() {
			return this.View();
		}
	}
}
