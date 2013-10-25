namespace IronPigeon.Relay.Tests.Controllers {
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Text;
	using System.Threading.Tasks;
	using System.Web;
	using System.Web.Mvc;
	using System.Web.Routing;
	using IronPigeon.Relay.Controllers;
	using IronPigeon.Relay.Models;
	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.Storage;
	using Microsoft.WindowsAzure.Storage.Blob;
	using Microsoft.WindowsAzure.Storage.Table;
	using Microsoft.WindowsAzure.StorageClient;
	using Moq;
	using Newtonsoft.Json;
	using NUnit.Framework;
	using Validation;

	[TestFixture]
	public class InboxControllerTest {
		private const string CloudConfigurationName = "StorageConnectionString";
		private const string MockClientIdentifier = "ms-app://w8id/";

		private readonly HttpClient httpClient = new HttpClient();

		private CloudBlobContainer container;

		private CloudTableClient tableClient;

		private InboxController controller;

		private InboxCreationResponse inbox;

		private string inboxId;
		private string testTableName;
		private string testContainerName;

		[SetUp]
		public void SetUp() {
			AzureStorageConfig.RegisterConfiguration();

			this.testContainerName = "unittests" + Guid.NewGuid().ToString();
			this.testTableName = "unittests" + Guid.NewGuid().ToString().Replace("-", string.Empty);
			var account = CloudStorageAccount.DevelopmentStorageAccount;
			var client = account.CreateCloudBlobClient();
			this.tableClient = account.CreateCloudTableClient();
			this.tableClient.GetTableReference(this.testTableName).CreateIfNotExists();
			this.container = client.GetContainerReference(this.testContainerName);
			var nowait = this.container.CreateContainerWithPublicBlobsIfNotExistAsync();
			this.controller = this.CreateController();
		}

		[TearDown]
		public void TearDown() {
			try {
				this.container.Delete();
				var table = this.tableClient.GetTableReference(this.controller.InboxTable.TableName);
				table.DeleteIfExists();
			} catch (StorageException ex) {
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
			var expiredBlob = dir.GetBlockBlobReference("expiredBlob");
			var freshBlob = dir.GetBlockBlobReference("freshBlob");
			expiredBlob.UploadFromStream(new MemoryStream(Encoding.ASCII.GetBytes("content")));
			expiredBlob.Metadata[InboxController.ExpirationDateMetadataKey] = (DateTime.UtcNow - TimeSpan.FromDays(1)).ToString(CultureInfo.InvariantCulture);
			expiredBlob.SetMetadata();
			freshBlob.UploadFromStream(new MemoryStream(Encoding.ASCII.GetBytes("content")));
			freshBlob.Metadata[InboxController.ExpirationDateMetadataKey] = (DateTime.UtcNow + TimeSpan.FromDays(1)).ToString(CultureInfo.InvariantCulture);
			freshBlob.SetMetadata();

			var results = this.GetInboxItemsAsyncHelper().Result;
			Assert.That(results.Items.Count, Is.EqualTo(1));
			Assert.That(results.Items[0].Location, Is.EqualTo(freshBlob.Uri));
		}

		[Test]
		public void DeleteNotificationAction() {
			this.CreateInboxHelperAsync().Wait();
			this.PostNotificationHelper(this.controller).Wait();
			var inbox = this.GetInboxItemsAsyncHelper().Result;
			this.controller.DeleteAsync(this.inboxId, inbox.Items[0].Location.AbsoluteUri).GetAwaiter().GetResult();

			var blobReference = this.container.GetBlockBlobReference(inbox.Items[0].Location.AbsoluteUri);
			Assert.That(blobReference.DeleteIfExists(), Is.False, "The blob should have already been deleted.");
			inbox = this.GetInboxItemsAsyncHelper().Result;
			Assert.That(inbox.Items, Is.Empty);
		}

		[Test]
		public void PostNotificationAction() {
			this.CreateInboxHelperAsync().Wait();
			var inputStream = new MemoryStream(new byte[] { 0x1, 0x3, 0x2 });
			this.PostNotificationHelper(this.controller, inputStream: inputStream).Wait();

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
			this.PostNotificationHelper(this.controller, lifetimeInMinutes: (int)InboxController.MaxLifetimeCeiling.TotalMinutes + 5).Wait();

			var results = this.GetInboxItemsAsyncHelper().Result;
			var blob = this.container.GetBlockBlobReference(results.Items[0].Location.AbsoluteUri);
			blob.FetchAttributes();
			Assert.That(
				DateTime.Parse(blob.Metadata[InboxController.ExpirationDateMetadataKey]),
				Is.EqualTo(DateTime.UtcNow + InboxController.MaxLifetimeCeiling).Within(TimeSpan.FromMinutes(1)));
		}

		[Test]
		public void PostNotificationActionRejectsLargePayloads() {
			var inputStream = new MemoryStream(new byte[InboxController.MaxNotificationSize + 1]);
			Assert.Throws<ArgumentException>(
				() => this.PostNotificationHelper(this.controller, inputStream: inputStream).GetAwaiter().GetResult());
		}

		[Test]
		public void PurgeExpiredAsync() {
			this.container.CreateIfNotExists();

			var expiredBlob = this.container.GetBlockBlobReference(Utilities.CreateRandomWebSafeName(5));
			expiredBlob.UploadFromStream(new MemoryStream(Encoding.ASCII.GetBytes("some content")));
			expiredBlob.Metadata[InboxController.ExpirationDateMetadataKey] = (DateTime.UtcNow - TimeSpan.FromDays(1)).ToString();
			expiredBlob.SetMetadata();

			var freshBlob = this.container.GetBlockBlobReference(Utilities.CreateRandomWebSafeName(5));
			freshBlob.UploadFromStream(new MemoryStream(Encoding.ASCII.GetBytes("some more content")));
			freshBlob.Metadata[InboxController.ExpirationDateMetadataKey] = (DateTime.UtcNow + TimeSpan.FromDays(1)).ToString();
			freshBlob.SetMetadata();

			InboxController.PurgeExpiredAsync(this.container).GetAwaiter().GetResult();

			Assert.That(expiredBlob.DeleteIfExists(), Is.False);
			Assert.That(freshBlob.DeleteIfExists(), Is.True);
		}

		[Test, Category("Stress")]
		public void HighFrequencyPostingTest() {
			const int MessageCount = 2;
			this.CreateInboxHelperAsync().Wait();
			this.RegisterPushNotificationsAsync().Wait();
			Task task = Task.WhenAll(Enumerable.Range(1, MessageCount).Select(n => {
				var controller = this.CreateController();
				var clientTableMock = new Mock<PushNotificationContext>(controller.ClientTable.ServiceClient, controller.ClientTable.TableName);
				clientTableMock
					.Setup<Task<PushNotificationClientEntity>>(c => c.GetAsync(MockClientIdentifier))
					.Returns(Task.FromResult(new PushNotificationClientEntity() { AccessToken = "sometoken" }));
				controller.ClientTable = clientTableMock.Object;
				return this.PostNotificationHelper(controller);
			}));
			task.Wait();
			var getResult = this.GetInboxItemsAsyncHelper().Result;
			Assert.AreEqual(MessageCount, getResult.Items.Count);
		}

		/// <summary>
		/// Tests that when an Inbox entity optimistic locking conflict is reported
		/// that changes are merged together successfully.
		/// </summary>
		[Test, Category("Stress")]
		public async Task OptimisticLockingMergeResolution() {
			await this.CreateInboxHelperAsync();
			var inboxContext1 = new InboxContext(this.controller.InboxTable.ServiceClient, this.controller.InboxTable.TableName);
			var inboxContext2 = new InboxContext(this.controller.InboxTable.ServiceClient, this.controller.InboxTable.TableName);
			var inboxContext3 = new InboxContext(this.controller.InboxTable.ServiceClient, this.controller.InboxTable.TableName);

			var inbox1 = inboxContext1.Get(this.inboxId).Single();
			var inbox2 = inboxContext2.Get(this.inboxId).Single();

			inbox1.WinPhone8ToastText1 = "new text1";
			inbox2.WinPhone8ToastText2 = "new text2";

			inboxContext1.UpdateObject(inbox1);
			inboxContext2.UpdateObject(inbox2);

			await inboxContext1.SaveChangesWithMergeAsync(inbox1);
			await inboxContext2.SaveChangesWithMergeAsync(inbox2);

			// Verify that a fresh entity obtained has both property changes preserved.
			var inbox3 = inboxContext3.Get(this.inboxId).Single();
			Assert.AreEqual("new text1", inbox3.WinPhone8ToastText1);
			Assert.AreEqual("new text2", inbox3.WinPhone8ToastText2);
		}

		private static string AssembleQueryString(NameValueCollection args) {
			Requires.NotNull(args, "args");

			var builder = new StringBuilder();
			foreach (string key in args) {
				if (builder.Length > 0) {
					builder.Append("&");
				}

				builder.Append(Uri.EscapeDataString(key));
				builder.Append("=");
				builder.Append(Uri.EscapeDataString(args[key]));
			}

			return builder.ToString();
		}

		private static void SetupNextRequest(InboxController controller, string httpMethod, Stream inputStream = null, NameValueCollection form = null) {
			Requires.NotNull(controller, "controller");
			inputStream = inputStream ?? new MemoryStream(new byte[] { 0x1, 0x3, 0x2 });

			var request = new Mock<HttpRequestBase>();
			request.SetupGet(r => r.InputStream).Returns(inputStream);
			request.SetupGet(r => r.HttpMethod).Returns(httpMethod);
			request.SetupGet(r => r.ContentLength).Returns((int)inputStream.Length);
			request.SetupGet(r => r.Form).Returns(form ?? new NameValueCollection());

			var httpContext = new Mock<HttpContextBase>();
			httpContext.SetupGet(c => c.Request).Returns(request.Object);

			var controllerContext = new Mock<ControllerContext>();
			controllerContext.SetupGet(cc => cc.HttpContext).Returns(httpContext.Object);

			controller.ControllerContext = controllerContext.Object;
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

		private async Task RegisterPushNotificationsAsync() {
			var queryArgs = new NameValueCollection {
				{ "channel_uri", "http://localhost/win8push" },
				{ "channel_content", "w8content" },
				{ "package_security_identifier", MockClientIdentifier },
				{ "wp8_channel_uri", "http://localhost/wp8push" },
				{ "wp8_channel_content", "wp8content" },
				{ "wp8_channel_toast_text1", "wp8toast1" },
				{ "wp8_channel_toast_text2", "wp8toast2" },
			};
			var inputStream = new MemoryStream(Encoding.ASCII.GetBytes(AssembleQueryString(queryArgs)));
			SetupNextRequest(this.controller, "PUT", inputStream, queryArgs);

			await this.controller.PushChannelAsync(this.inboxId);
		}

		private async Task PostNotificationHelper(InboxController controller, Stream inputStream = null, int lifetimeInMinutes = 2) {
			SetupNextRequest(controller, "POST", inputStream);

			var result = await controller.PostNotificationAsync(this.inboxId, lifetimeInMinutes);
			Assert.That(result, Is.InstanceOf<EmptyResult>());
		}

		private InboxControllerForTest CreateController() {
			return new InboxControllerForTest(this.container.Name, this.testTableName, CloudConfigurationName, new MockHandler());
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
			public InboxControllerForTest(string containerName, string tableName, string cloudConfigurationName, HttpMessageHandler httpHandler)
				: base(containerName, tableName, cloudConfigurationName, httpHandler) {
				this.HttpHandler = httpHandler;
			}

			public HttpMessageHandler HttpHandler { get; private set; }

			protected override Uri GetAbsoluteUrlForAction(string action, dynamic routeValues) {
				routeValues = new ReflectionDynamicObject(routeValues);
				return new Uri(
					string.Format(CultureInfo.InvariantCulture, "http://localhost/inbox/{0}/{1}", action, routeValues.id));
			}
		}

		private class MockHandler : HttpMessageHandler {
			internal MockHandler() {
			}

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken) {
				return Task.FromResult(new HttpResponseMessage());
			}
		}
	}
}
