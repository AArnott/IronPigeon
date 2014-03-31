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
	using Microsoft.WindowsAzure.Storage;
	using Microsoft.WindowsAzure.Storage.Blob;
	using NUnit.Framework;

	[TestFixture]
	public class AzureBlobStorageTests : CloudBlobStorageProviderTestBase {
		private string testContainerName;
		private CloudBlobContainer container;
		private AzureBlobStorage provider;

		[SetUp]
		public void Initialize() {
			this.testContainerName = "unittests" + Guid.NewGuid().ToString();
			var account = CloudStorageAccount.DevelopmentStorageAccount;
			var blobClient = account.CreateCloudBlobClient();
			this.Provider = this.provider = new AzureBlobStorage(account, this.testContainerName);
			this.provider.CreateContainerIfNotExistAsync().GetAwaiter().GetResult();
			this.container = blobClient.GetContainerReference(this.testContainerName);
		}

		[TearDown]
		public void TearDown() {
			if (this.container != null) {
				this.container.Delete();
			}
		}

		[Test, Ignore]
		public void CreateWithContainerAsync() {
			// The SetUp method already called the method, so this tests the results of it.
			var permissions = this.container.GetPermissions();
			Assert.That(permissions.PublicAccess, Is.EqualTo(BlobContainerPublicAccessType.Blob));
		}

		[Test, Ignore]
		public void PurgeBlobsExpiringBeforeAsync() {
			this.UploadMessageHelperAsync().GetAwaiter().GetResult();
			this.provider.PurgeBlobsExpiringBeforeAsync(DateTime.UtcNow.AddDays(7)).GetAwaiter().GetResult();
			Assert.That(this.container.ListBlobs().Count(), Is.EqualTo(0));
		}
	}
}
