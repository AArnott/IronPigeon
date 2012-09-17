namespace IronPigeon.Relay {
	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Web;
	using IronPigeon.Providers;
	using IronPigeon.Relay.Controllers;
	using IronPigeon.Relay.Models;
	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.StorageClient;
	using Validation;
#if !NET40
	using TaskEx = System.Threading.Tasks.Task;
#endif

	public class AzureStorageConfig {
		/// <summary>
		/// The key to the Azure account configuration information.
		/// </summary>
		internal const string DefaultCloudConfigurationName = "StorageConnectionString";

		internal static readonly TimeSpan PurgeExpiredBlobsInterval = TimeSpan.FromHours(4);

		public static void RegisterConfiguration() {
			CloudStorageAccount.SetConfigurationSettingPublisher(ConfigSetter);

			var storage = CloudStorageAccount.FromConfigurationSetting(DefaultCloudConfigurationName);
			var initialization = TaskEx.WhenAll(
				BlobController.OneTimeInitializeAsync(storage),
				InboxController.OneTimeInitializeAsync(storage),
				WindowsPushNotificationClientController.OneTimeInitializeAsync(storage));
			initialization.Wait();
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