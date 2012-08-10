namespace IronPigeon.Relay.Tests.Controllers {
	using System;
	using System.IO;
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

		private string testContainerName;

		private InboxController controller;

		[SetUp]
		public void SetUp() {
			AzureStorageConfig.RegisterConfiguration();

			this.testContainerName = "unittests" + Guid.NewGuid().ToString();
			this.controller = new InboxController(this.testContainerName, CloudConfigurationName);
		}

		[TearDown]
		public void TearDown() {
			var account = CloudStorageAccount.FromConfigurationSetting(CloudConfigurationName);
			var client = account.CreateCloudBlobClient();
			var container = client.GetContainerReference(this.testContainerName);
			container.Delete();
		}

		[Test, Ignore("Not yet implemented")]
		public void CreateAction() {
		}

		[Test]
		public void GetInboxItemsAsyncAction() {
			var data = this.GetInboxItemsAsyncHelper("emptyThumbprint").Result;
			Assert.That(data.Items, Is.Empty);
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
			var result = this.controller.PostNotification(Thumbprint).Result;
			Assert.That(result, Is.InstanceOf<EmptyResult>());

			// Confirm that retrieving the inbox now includes the posted message.
			var getResult = this.GetInboxItemsAsyncHelper(Thumbprint).Result;
			Assert.That(getResult.Items.Count, Is.EqualTo(1));
			var blobStream = this.httpClient.GetStreamAsync(getResult.Items[0].Location).Result;
			var blobMemoryStream = new MemoryStream();
			blobStream.CopyTo(blobMemoryStream);
			Assert.That(blobMemoryStream.ToArray(), Is.EqualTo(inputStream.ToArray()));
		}

		[Test, Ignore("Not yet implemented")]
		public void GetNotificationAction() {

		}

		[Test, Ignore("Not yet implemented")]
		public void DeleteNotificationAction() {

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
