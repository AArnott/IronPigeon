namespace IronPigeon.Relay {
	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.Linq;
	using System.Web;
	using Validation;
	using Microsoft.WindowsAzure;

	public class AzureStorageConfig {
		public static void RegisterConfiguration() {
			CloudStorageAccount.SetConfigurationSettingPublisher(ConfigSetter);
		}

		private static void ConfigSetter(string configName, Func<string, bool> configSetter) {
			var connectionString = ConfigurationManager.ConnectionStrings[configName];
			if (connectionString != null) {
				configSetter(connectionString.ConnectionString);
				return;
			}

			var appSetting = ConfigurationManager.AppSettings[configName];
			if (!string.IsNullOrEmpty(appSetting)) {
				configSetter(appSetting);
				return;
			}

			Requires.Fail("Configuration name not found.");
		}
	}
}