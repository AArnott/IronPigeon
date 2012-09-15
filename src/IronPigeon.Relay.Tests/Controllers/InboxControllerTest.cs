namespace IronPigeon.Relay.Tests.Controllers {
	using System;
	using System.Globalization;
	using System.IO;
	using System.Net;
	using System.Net.Http;
	using System.Threading.Tasks;
	using System.Web;
	using System.Web.Mvc;
	using System.Web.Routing;
	using IronPigeon.Relay.Controllers;
	using Validation;
	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.StorageClient;
	using Moq;
	using Newtonsoft.Json;
	using NUnit.Framework;

	[TestFixture]
	public class InboxControllerTest {
		private const string CloudConfigurationName = "StorageConnectionString";

		private readonly HttpClient httpClient = new HttpClient();

		private CloudBlobContainer container;

		private CloudTableClient tableClient;

		private InboxController controller;

		private InboxCreationResponse inbox;

		private string inboxId;

		[SetUp]
		public void SetUp() {
			AzureStorageConfig.RegisterConfiguration();

			var testContainerName = "unittests" + Guid.NewGuid().ToString();
			var testTableName = "unittests" + Guid.NewGuid().ToString().Replace("-", string.Empty);
			var account = CloudStorageAccount.FromConfigurationSetting(CloudConfigurationName);
			var client = account.CreateCloudBlobClient();
			this.tableClient = account.CreateCloudTableClient();
			this.tableClient.CreateTableIfNotExist(testTableName);
			this.container = client.GetContainerReference(testContainerName);
			this.controller = new InboxControllerForTest(this.container.Name, testTableName, CloudConfigurationName);
		}

		[TearDown]
		public void TearDown() {
			try {
				this.container.Delete();
				this.tableClient.DeleteTableIfExist(this.controller.InboxTable.TableName);
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
			this.CreateInboxHelperAsync().Wait();
			var data = this.GetInboxItemsAsyncHelper().Result;
			Assert.That(data.Items, Is.Empty);
		}

		[Test]
		public void PostNotificationActionRejectsNonPositiveLifetime() {
			Assert.Throws<ArgumentOutOfRangeException>(() => this.controller.PostNotificationAsync("thumbprint", 0).GetAwaiter().GetResult());
			Assert.Throws<ArgumentOutOfRangeException>(() => this.controller.PostNotificationAsync("thumbprint", -1).GetAwaiter().GetResult());
		}

		/// <summary>
		/// Verifies that even before a purge removes an expired inbox entry,
		/// those entries are not returned in query results.
		/// </summary>
		[Test]
		public void GetInboxItemsOnlyReturnsUnexpiredItems() {
			this.CreateInboxHelperAsync().Wait();
			var dir = this.container.GetDirectoryReference(this.inboxId);
			var expiredBlob = dir.GetBlobReference("expiredBlob");
			var freshBlob = dir.GetBlobReference("freshBlob");
			expiredBlob.UploadText("content");
			expiredBlob.Metadata[InboxController.ExpirationDateMetadataKey] = (DateTime.UtcNow - TimeSpan.FromDays(1)).ToString(CultureInfo.InvariantCulture);
			expiredBlob.SetMetadata();
			freshBlob.UploadText("content");
			freshBlob.Metadata[InboxController.ExpirationDateMetadataKey] = (DateTime.UtcNow + TimeSpan.FromDays(1)).ToString(CultureInfo.InvariantCulture);
			freshBlob.SetMetadata();

			var results = this.GetInboxItemsAsyncHelper().Result;
			Assert.That(results.Items.Count, Is.EqualTo(1));
			Assert.That(results.Items[0].Location, Is.EqualTo(freshBlob.Uri));
		}

		[Test]
		public void DeleteNotificationAction() {
			this.CreateInboxHelperAsync().Wait();
			this.PostNotificationHelper().Wait();
			var inbox = this.GetInboxItemsAsyncHelper().Result;
			this.controller.DeleteAsync(this.inboxId, inbox.Items[0].Location.AbsoluteUri).GetAwaiter().GetResult();

			var blobReference = this.container.GetBlobReference(inbox.Items[0].Location.AbsoluteUri);
			Assert.That(blobReference.DeleteIfExists(), Is.False, "The blob should have already been deleted.");
			inbox = this.GetInboxItemsAsyncHelper().Result;
			Assert.That(inbox.Items, Is.Empty);
		}

		[Test]
		public void PostNotificationAction() {
			this.CreateInboxHelperAsync().Wait();
			var inputStream = new MemoryStream(new byte[] { 0x1, 0x3, 0x2 });
			this.PostNotificationHelper(inputStream: inputStream).Wait();

			// Confirm that retrieving the inbox now includes the posted message.
			var getResult = this.GetInboxItemsAsyncHelper().Result;
			Assert.That(getResult.Items.Count, Is.EqualTo(1));
			Assert.That(getResult.Items[0].Location, Is.Not.Null);
			Assert.That(getResult.Items[0].DatePostedUtc, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromMinutes(1)));

			var blobStream = this.httpClient.GetStreamAsync(getResult.Items[0].Location).Result;
			var blobMemoryStream = new MemoryStream();
			blobStream.CopyTo(blobMemoryStream);
			Assert.That(blobMemoryStream.ToArray(), Is.EqualTo(inputStream.ToArray()));
		}

		[Test]
		public void PostNotificationActionHasExpirationCeiling() {
			this.CreateInboxHelperAsync().Wait();
			this.PostNotificationHelper(lifetimeInMinutes: (int)InboxController.MaxLifetimeCeiling.TotalMinutes + 5).Wait();

			var results = this.GetInboxItemsAsyncHelper().Result;
			var blob = this.container.GetBlobReference(results.Items[0].Location.AbsoluteUri);
			blob.FetchAttributes();
			Assert.That(
				DateTime.Parse(blob.Metadata[InboxController.ExpirationDateMetadataKey]),
				Is.EqualTo(DateTime.UtcNow + InboxController.MaxLifetimeCeiling).Within(TimeSpan.FromMinutes(1)));
		}

		[Test]
		public void PostNotificationActionRejectsLargePayloads() {
			var inputStream = new MemoryStream(new byte[InboxController.MaxNotificationSize + 1]);
			Assert.Throws<ArgumentException>(
				() => this.PostNotificationHelper(inputStream: inputStream).GetAwaiter().GetResult());
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

		private async Task CreateInboxHelperAsync() {
			var jsonResult = await this.controller.CreateAsync();
			var result = (InboxCreationResponse)jsonResult.Data;
			this.inbox = result;

			var routes = new RouteCollection();
			RouteConfig.RegisterRoutes(routes);
			var httpContextMock = new Mock<HttpContextBase>();
			httpContextMock.Setup(c => c.Request.AppRelativeCurrentExecutionFilePath)
				.Returns("~" + new Uri(result.MessageReceivingEndpoint).PathAndQuery); // POST
			var routeData = routes.GetRouteData(httpContextMock.Object);
			this.inboxId = (string)routeData.Values["id"];
		}

		private async Task PostNotificationHelper(Stream inputStream = null, int lifetimeInMinutes = 2) {
			inputStream = inputStream ?? new MemoryStream(new byte[] { 0x1, 0x3, 0x2 });

			var request = new Mock<HttpRequestBase>();
			request.SetupGet(r => r.InputStream).Returns(inputStream);
			request.SetupGet(r => r.HttpMethod).Returns("POST");
			request.SetupGet(r => r.ContentLength).Returns((int)inputStream.Length);

			var httpContext = new Mock<HttpContextBase>();
			httpContext.SetupGet(c => c.Request).Returns(request.Object);

			var controllerContext = new Mock<ControllerContext>();
			controllerContext.SetupGet(cc => cc.HttpContext).Returns(httpContext.Object);

			this.controller.ControllerContext = controllerContext.Object;

			var result = await this.controller.PostNotificationAsync(this.inboxId, lifetimeInMinutes);
			Assert.That(result, Is.InstanceOf<EmptyResult>());
		}

		private async Task<IncomingList> GetInboxItemsAsyncHelper() {
			ActionResult result = await this.controller.GetInboxItemsAsync(this.inboxId);

			Assert.That(result, Is.InstanceOf<JsonResult>());
			var jsonResult = (JsonResult)result;
			Assert.That(jsonResult.JsonRequestBehavior, Is.EqualTo(JsonRequestBehavior.AllowGet));
			var data = (IncomingList)jsonResult.Data;
			Assert.That(data, Is.Not.Null);
			return data;
		}

		public class InboxControllerForTest : InboxController {
			public InboxControllerForTest(string containerName, string tableName, string cloudConfigurationName)
				: base(containerName, tableName, cloudConfigurationName) {
			}

			protected override Uri GetAbsoluteUrlForAction(string action, dynamic routeValues) {
				routeValues = new ReflectionDynamicObject(routeValues);
				return new Uri(
					string.Format(CultureInfo.InvariantCulture, "http://localhost/inbox/{0}/{1}", action, routeValues.id));
			}
		}
	}
}
