﻿namespace IronPigeon.Relay {
	using System.Web;
	using System.Web.Mvc;

	public class FilterConfig {
		public static void RegisterGlobalFilters(GlobalFilterCollection filters) {
			filters.Add(new HandleErrorAttribute());
		}
	}
}