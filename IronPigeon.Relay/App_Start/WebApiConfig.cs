namespace IronPigeon.Relay {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Web.Http;

	/// <summary>
	/// Registers WebAPI routes.
	/// </summary>
	public static class WebApiConfig {
		/// <summary>
		/// Registers WebAPI routes.
		/// </summary>
		/// <param name="config">The config.</param>
		public static void Register(HttpConfiguration config) {
			config.Routes.MapHttpRoute(
				name: "DefaultApi",
				routeTemplate: "api/{controller}/{id}",
				defaults: new { id = RouteParameter.Optional });
		}
	}
}
