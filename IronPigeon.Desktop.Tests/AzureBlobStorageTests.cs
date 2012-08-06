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
	public class AzureBlobStorageTests {
		private readonly string TestContainerName = "unittests" + Guid.NewGuid().ToString();
		private AzureBlobStorage blobStorage;
		private CloudBlobContainer container;

		[SetUp]
		public void Initialize() {
			CloudStorageAccount.SetConfigurationSettingPublisher(ConfigSetter);
			var account = CloudStorageAccount.FromConfigurationSetting("StorageConnectionString");
			var blobClient = account.CreateCloudBlobClient();
			this.container = blobClient.GetContainerReference(TestContainerName);
			this.blobStorage = AzureBlobStorage.CreateWithContainerAsync(account, TestContainerName).Result;
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
		public void UploadMessageAsync() {
			var body = new MemoryStream(Valid.MessageContent);
			var uri = this.blobStorage.UploadMessageAsync(body, Valid.ExpirationUtc).Result;
			var client = new HttpClient();
			var downloadedBody = client.GetByteArrayAsync(uri).Result;
			Assert.That(downloadedBody, Is.EqualTo(Valid.MessageContent));
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
