namespace IronPigeon.Tests {
	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Text;
	using System.Threading.Tasks;
	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.StorageClient;
	using NUnit.Framework;

	[TestFixture]
	public class AzureBlobStorageTests : CloudBlobStorageProviderTestBase {
		private string testContainerName;
		private CloudBlobContainer container;

		[SetUp]
		public void Initialize() {
			this.testContainerName = "unittests" + Guid.NewGuid().ToString();
			CloudStorageAccount.SetConfigurationSettingPublisher(ConfigSetter);
			var account = CloudStorageAccount.FromConfigurationSetting("StorageConnectionString");
			var blobClient = account.CreateCloudBlobClient();
			this.container = blobClient.GetContainerReference(this.testContainerName);
			this.Provider = AzureBlobStorage.CreateWithContainerAsync(account, this.testContainerName).Result;
		}

		[TearDown]
		public void TearDown() {
			this.container.Delete();
		}

		[Test]
		public void CreateWithContainerAsync() {
			// The SetUp method already called the method, so this tests the results of it.
			var permissions = this.container.GetPermissions();
			Assert.That(permissions.PublicAccess, Is.EqualTo(BlobContainerPublicAccessType.Blob));
		}

		private static void ConfigSetter(string configName, Func<string, bool> configSetter) {
			string value = ConfigurationManager.AppSettings[configName];
			if (String.IsNullOrEmpty(value)) {
				value = ConfigurationManager.ConnectionStrings[configName].ConnectionString;
			}

			configSetter(value);
		}
	}
}
