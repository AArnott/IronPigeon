namespace IronPigeon.Relay.Tests.Controllers {
	using System;
	using System.IO;
	using System.Net;
	using System.Net.Http;
	using System.Threading.Tasks;
	using System.Web;
	using System.Web.Mvc;
	using IronPigeon.Relay.Controllers;
	using Microsoft;
	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.StorageClient;

	using Moq;
	using NUnit.Framework;

	[TestFixture]
	public class InboxControllerTest {
		private const string CloudConfigurationName = "StorageConnectionString";

		private readonly HttpClient httpClient = new HttpClient();

		private CloudBlobContainer container;

		private InboxController controller;

		[SetUp]
		public void SetUp() {
			AzureStorageConfig.RegisterConfiguration();

			var testContainerName = "unittests" + Guid.NewGuid().ToString();
			var account = CloudStorageAccount.FromConfigurationSetting(CloudConfigurationName);
			var client = account.CreateCloudBlobClient();
			this.container = client.GetContainerReference(testContainerName);
			this.controller = new InboxController(this.container.Name, CloudConfigurationName);
		}

		[TearDown]
		public void TearDown() {
			try {
				this.container.Delete();
			} catch (StorageClientException ex) {
				bool handled = false;
				var webException = ex.InnerException as WebException;
				if (webException != null) {
					var httpResponse = (HttpWebResponse)webException.Response;
					if (httpResponse.StatusCode == HttpStatusCode.NotFound) {
						handled = true; // it's legit that some tests never created the container to begin with.
					}
				}

				if (!handled) {
					throw;
				}
			}
		}

		[Test]
		public void GetInboxItemsAsyncAction() {
			var data = this.GetInboxItemsAsyncHelper("emptyThumbprint").Result;
			Assert.That(data.Items, Is.Empty);
		}

		[Test]
		public void PostNotificationActionRejectsNonPositiveLifetime() {
			Assert.Throws<ArgumentOutOfRangeException>(() => this.controller.PostNotification("thumbprint", 0).GetAwaiter().GetResult());
			Assert.Throws<ArgumentOutOfRangeException>(() => this.controller.PostNotification("thumbprint", -1).GetAwaiter().GetResult());
		}

		[Test]
		public void PostNotificationAction() {
			var inputStream = new MemoryStream(new byte[] { 0x1, 0x3, 0x2 });

			var request = new Mock<HttpRequestBase>();
			request.SetupGet(r => r.InputStream).Returns(inputStream);
			request.SetupGet(r => r.HttpMethod).Returns("POST");

			var httpContext = new Mock<HttpContextBase>();
			httpContext.SetupGet(c => c.Request).Returns(request.Object);

			var controllerContext = new Mock<ControllerContext>();
			controllerContext.SetupGet(cc => cc.HttpContext).Returns(httpContext.Object);

			this.controller.ControllerContext = controllerContext.Object;

			const string Thumbprint = "nonEmptyThumbprint";
			const int LifetimeInMinutes = 2;
			var result = this.controller.PostNotification(Thumbprint, LifetimeInMinutes).Result;
			Assert.That(result, Is.InstanceOf<EmptyResult>());

			// Confirm that retrieving the inbox now includes the posted message.
			var getResult = this.GetInboxItemsAsyncHelper(Thumbprint).Result;
			Assert.That(getResult.Items.Count, Is.EqualTo(1));
			Assert.That(getResult.Items[0].Location, Is.Not.Null);
			Assert.That(getResult.Items[0].DatePostedUtc, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromMinutes(1)));

			var blobStream = this.httpClient.GetStreamAsync(getResult.Items[0].Location).Result;
			var blobMemoryStream = new MemoryStream();
			blobStream.CopyTo(blobMemoryStream);
			Assert.That(blobMemoryStream.ToArray(), Is.EqualTo(inputStream.ToArray()));
		}

		[Test]
		public void PurgeExpiredAsync() {
			this.container.CreateIfNotExist();

			var expiredBlob = this.container.GetBlobReference(Utilities.CreateRandomWebSafeName(5));
			expiredBlob.UploadText("some content");
			expiredBlob.Metadata[InboxController.ExpirationDateMetadataKey] = (DateTime.UtcNow - TimeSpan.FromDays(1)).ToString();
			expiredBlob.SetMetadata();

			var freshBlob = this.container.GetBlobReference(Utilities.CreateRandomWebSafeName(5));
			freshBlob.UploadText("some more content");
			freshBlob.Metadata[InboxController.ExpirationDateMetadataKey] = (DateTime.UtcNow + TimeSpan.FromDays(1)).ToString();
			freshBlob.SetMetadata();

			this.controller.PurgeExpiredAsync().GetAwaiter().GetResult();

			Assert.That(expiredBlob.DeleteIfExists(), Is.False);
			Assert.That(freshBlob.DeleteIfExists(), Is.True);
		}

		private async Task<IncomingList> GetInboxItemsAsyncHelper(string thumbprint) {
			Requires.NotNullOrEmpty(thumbprint, "thumbprint");
			ActionResult result = await this.controller.GetInboxItemsAsync(thumbprint);

			Assert.That(result, Is.InstanceOf<JsonResult>());
			var jsonResult = (JsonResult)result;
			Assert.That(jsonResult.JsonRequestBehavior, Is.EqualTo(JsonRequestBehavior.AllowGet));
			var data = (IncomingList)jsonResult.Data;
			Assert.That(data, Is.Not.Null);
			return data;
		}
	}
}
