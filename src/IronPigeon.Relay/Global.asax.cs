namespace IronPigeon.Relay {
	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.IO;
	using System.Linq;
	using System.Web;
	using System.Web.Http;
	using System.Web.Mvc;
	using System.Web.Routing;
	using PushSharp;
	using PushSharp.Apple;

	// Note: For instructions on enabling IIS6 or IIS7 classic mode, 
	// visit http://go.microsoft.com/?LinkId=9394801
	public class MvcApplication : HttpApplication {
		internal static PushBroker PushBroker { get; private set; }

		internal static bool IsApplePushRegistered { get; private set; }

		protected void Application_Start() {
			AreaRegistration.RegisterAllAreas();

			WebApiConfig.Register(GlobalConfiguration.Configuration);
			FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
			RouteConfig.RegisterRoutes(RouteTable.Routes);
			AzureStorageConfig.RegisterConfiguration();

			PushBroker = new PushBroker();

			if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["AppleAPNSCertFile"])) {
				byte[] appleCert = File.ReadAllBytes(ConfigurationManager.AppSettings["AppleAPNSCertFile"]);
				PushBroker.RegisterAppleService(new ApplePushChannelSettings(appleCert, ConfigurationManager.AppSettings["AppleAPNSCertPassword"]));
				IsApplePushRegistered = true;
			}
		}

		protected void Application_End() {
			PushBroker.StopAllServices();
			PushBroker = null;
		}
	}
}