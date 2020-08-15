// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay.Tests.Controllers
{
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
    using Validation;
    using Xunit;

    public class InboxControllerTest : IDisposable
    {
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

        public InboxControllerTest()
        {
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

        public void Dispose()
        {
            try
            {
                this.container.Delete();
                var table = this.tableClient.GetTableReference(this.controller.InboxTable.TableName);
                table.DeleteIfExists();
            }
            catch (StorageException ex)
            {
                bool handled = false;
                var webException = ex.InnerException as WebException;
                if (webException != null)
                {
                    var httpResponse = (HttpWebResponse)webException.Response;
                    if (httpResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        handled = true; // it's legit that some tests never created the container to begin with.
                    }
                }

                if (!handled)
                {
                    throw;
                }
            }
        }

        [Fact]
        public void GetInboxItemsAsyncAction()
        {
            this.CreateInboxHelperAsync().Wait();
            var data = this.GetInboxItemsAsyncHelper().Result;
            Assert.Empty(data.Items);
        }

        [Fact]
        public void PostNotificationActionRejectsNonPositiveLifetime()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => this.controller.PostNotificationAsync("thumbprint", 0).GetAwaiter().GetResult());
            Assert.Throws<ArgumentOutOfRangeException>(() => this.controller.PostNotificationAsync("thumbprint", -1).GetAwaiter().GetResult());
        }

        /// <summary>
        /// Verifies that even before a purge removes an expired inbox entry,
        /// those entries are not returned in query results.
        /// </summary>
        [Fact]
        public void GetInboxItemsOnlyReturnsUnexpiredItems()
        {
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
            Assert.Equal(1, results.Items.Count);
            Assert.Equal(freshBlob.Uri, results.Items[0].Location);
        }

        [Fact]
        public void DeleteNotificationAction()
        {
            this.CreateInboxHelperAsync().Wait();
            this.PostNotificationHelper(this.controller).Wait();
            var inbox = this.GetInboxItemsAsyncHelper().Result;
            this.controller.DeleteAsync(this.inboxId, inbox.Items[0].Location.AbsoluteUri).GetAwaiter().GetResult();

            var blobReference = this.container.GetBlockBlobReference(inbox.Items[0].Location.AbsoluteUri);
            Assert.False(blobReference.DeleteIfExists(), "The blob should have already been deleted.");
            inbox = this.GetInboxItemsAsyncHelper().Result;
            Assert.Empty(inbox.Items);
        }

        [Fact]
        public void PostNotificationAction()
        {
            this.CreateInboxHelperAsync().Wait();
            var inputStream = new MemoryStream(new byte[] { 0x1, 0x3, 0x2 });
            this.PostNotificationHelper(this.controller, inputStream: inputStream).Wait();

            // Confirm that retrieving the inbox now includes the posted message.
            var getResult = this.GetInboxItemsAsyncHelper().Result;
            Assert.Equal(1, getResult.Items.Count);
            Assert.NotNull(getResult.Items[0].Location);
            var tolerance = TimeSpan.FromMinutes(1);
            Assert.InRange(getResult.Items[0].DatePostedUtc, DateTime.UtcNow - tolerance, DateTime.UtcNow + tolerance);

            var blobStream = this.httpClient.GetStreamAsync(getResult.Items[0].Location).Result;
            var blobMemoryStream = new MemoryStream();
            blobStream.CopyTo(blobMemoryStream);
            Assert.Equal(inputStream.ToArray(), blobMemoryStream.ToArray());
        }

        [Fact]
        public void PostNotificationActionHasExpirationCeiling()
        {
            this.CreateInboxHelperAsync().Wait();
            this.PostNotificationHelper(this.controller, lifetimeInMinutes: (int)InboxController.MaxLifetimeCeiling.TotalMinutes + 5).Wait();

            var results = this.GetInboxItemsAsyncHelper().Result;
            var blob = this.container.GetBlockBlobReference(results.Items[0].Location.AbsoluteUri);
            blob.FetchAttributes();
            var tolerance = TimeSpan.FromMinutes(1);
            var target = DateTime.UtcNow + InboxController.MaxLifetimeCeiling;
            Assert.InRange(
                DateTime.Parse(blob.Metadata[InboxController.ExpirationDateMetadataKey]),
                target - tolerance,
                target + tolerance);
        }

        [Fact]
        public void PostNotificationActionRejectsLargePayloads()
        {
            this.CreateInboxHelperAsync().Wait();
            var inputStream = new MemoryStream(new byte[InboxController.MaxNotificationSize + 1]);
            Assert.Throws<ArgumentException>(
                () => this.PostNotificationHelper(this.controller, inputStream: inputStream).GetAwaiter().GetResult());
        }

        [Fact]
        public void PurgeExpiredAsync()
        {
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

            Assert.False(expiredBlob.DeleteIfExists());
            Assert.True(freshBlob.DeleteIfExists());
        }

        [Fact, Trait("Stress", "true")]
        public async Task HighFrequencyPostingTest()
        {
            const int MessageCount = 5;
            await this.CreateInboxHelperAsync();
            await this.RegisterPushNotificationsAsync();
            await Task.WhenAll(Enumerable.Range(1, MessageCount).Select(n =>
            {
                var controller = this.CreateController();
                var clientTableMock = new Mock<PushNotificationContext>(controller.ClientTable.ServiceClient, controller.ClientTable.TableName);
                clientTableMock
                    .Setup<Task<PushNotificationClientEntity>>(c => c.GetAsync(MockClientIdentifier))
                    .Returns(Task.FromResult(new PushNotificationClientEntity() { AccessToken = "sometoken" }));
                controller.ClientTable = clientTableMock.Object;
                return this.PostNotificationHelper(controller);
            }));
            var getResult = await this.GetInboxItemsAsyncHelper();
            Assert.Equal(MessageCount, getResult.Items.Count);
        }

        /// <summary>
        /// Tests that when an Inbox entity optimistic locking conflict is reported
        /// that changes are merged together successfully.
        /// </summary>
        /// <returns>A task representing the async test.</returns>
        [Fact, Trait("Stress", "true")]
        public async Task OptimisticLockingMergeResolution()
        {
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
            Assert.Equal("new text1", inbox3.WinPhone8ToastText1);
            Assert.Equal("new text2", inbox3.WinPhone8ToastText2);
        }

        private static string AssembleQueryString(NameValueCollection args)
        {
            Requires.NotNull(args, "args");

            var builder = new StringBuilder();
            foreach (string key in args)
            {
                if (builder.Length > 0)
                {
                    builder.Append("&");
                }

                builder.Append(Uri.EscapeDataString(key));
                builder.Append("=");
                builder.Append(Uri.EscapeDataString(args[key]));
            }

            return builder.ToString();
        }

        private static void SetupNextRequest(InboxController controller, string httpMethod, Stream inputStream = null, NameValueCollection form = null)
        {
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

        private async Task CreateInboxHelperAsync()
        {
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

        private async Task RegisterPushNotificationsAsync()
        {
            var queryArgs = new NameValueCollection
            {
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

        private async Task PostNotificationHelper(InboxController controller, Stream inputStream = null, int lifetimeInMinutes = 10)
        {
            SetupNextRequest(controller, "POST", inputStream);

            var result = await controller.PostNotificationAsync(this.inboxId, lifetimeInMinutes);
            Assert.IsType(typeof(HttpStatusCodeResult), result);
            var actualStatus = (HttpStatusCode)((HttpStatusCodeResult)result).StatusCode;
            Assert.Equal(HttpStatusCode.Created, actualStatus);
        }

        private InboxControllerForTest CreateController()
        {
            return new InboxControllerForTest(this.container.Name, this.testTableName, CloudConfigurationName, new MockHandler());
        }

        private async Task<IncomingList> GetInboxItemsAsyncHelper()
        {
            ActionResult result = await this.controller.GetInboxItemsAsync(this.inboxId);

            Assert.IsType(typeof(JsonResult), result);
            var jsonResult = (JsonResult)result;
            Assert.Equal(JsonRequestBehavior.AllowGet, jsonResult.JsonRequestBehavior);
            var data = (IncomingList)jsonResult.Data;
            Assert.NotNull(data);
            return data;
        }

        public class InboxControllerForTest : InboxController
        {
            public InboxControllerForTest(string containerName, string tableName, string cloudConfigurationName, HttpMessageHandler httpHandler)
                : base(containerName, tableName, cloudConfigurationName, httpHandler)
            {
                this.HttpHandler = httpHandler;
            }

            public HttpMessageHandler HttpHandler { get; private set; }

            protected override Uri GetAbsoluteUrlForAction(string action, dynamic routeValues)
            {
                routeValues = new ReflectionDynamicObject(routeValues);
                return new Uri(
                    string.Format(CultureInfo.InvariantCulture, "http://localhost/inbox/{0}/{1}", action, routeValues.id));
            }
        }

        private class MockHandler : HttpMessageHandler
        {
            internal MockHandler()
            {
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage());
            }
        }
    }
}
