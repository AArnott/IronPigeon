namespace IronPigeon.Tests.Providers {
	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Text;
	using System.Threading.Tasks;
	using IronPigeon.Providers;
	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.StorageClient;
	using NUnit.Framework;

	[TestFixture]
	public class AzureBlobStorageTests : CloudBlobStorageProviderTestBase {
		private string testContainerName;
		private CloudBlobContainer container;
		private AzureBlobStorage provider;

		[SetUp]
		public void Initialize() {
			this.testContainerName = "unittests" + Guid.NewGuid().ToString();
			CloudStorageAccount.SetConfigurationSettingPublisher(ConfigSetter);
			var account = CloudStorageAccount.FromConfigurationSetting("StorageConnectionString");
			var blobClient = account.CreateCloudBlobClient();
			this.Provider = this.provider = new AzureBlobStorage(account, this.testContainerName);
			this.provider.CreateContainerIfNotExistAsync().GetAwaiter().GetResult();
			this.container = blobClient.GetContainerReference(this.testContainerName);
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

		[Test]
		public void PurgeBlobsExpiringBeforeAsync() {
			this.UploadMessageHelperAsync().GetAwaiter().GetResult();
			this.provider.PurgeBlobsExpiringBeforeAsync(DateTime.UtcNow.AddDays(7)).GetAwaiter().GetResult();
			Assert.That(this.container.ListBlobs().Count(), Is.EqualTo(0));
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
