// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Data.Services.Client;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using System.Web;
    using System.Web.Mvc;
    using IronPigeon.Relay.Models;
    using Microsoft.WindowsAzure;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Microsoft.WindowsAzure.Storage.Table.DataServices;
    using Microsoft.WindowsAzure.StorageClient;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using PushSharp;
    using PushSharp.Apple;
    using Validation;

#if !DEBUG
    [RequireHttps]
#endif
    public class InboxController : Controller
    {
        /// <summary>
        /// The key into a blob's metadata that stores the blob's expiration date.
        /// </summary>
        public const string ExpirationDateMetadataKey = "DeleteAfter";

        /// <summary>
        /// The maximum allowable size for a notification.
        /// </summary>
        public const int MaxNotificationSize = 10 * 1024;

        /// <summary>
        /// The maximum lifetime an inbox will retain a posted message.
        /// </summary>
        public static readonly TimeSpan MaxLifetimeCeiling = TimeSpan.FromDays(14);

        /// <summary>
        /// The default name for the container used to store posted messages.
        /// </summary>
        private const string DefaultInboxContainerName = "inbox";

        private const string DefaultInboxTableName = "inbox";

        private static readonly Dictionary<string, TaskCompletionSource<object>> LongPollWaiters = new Dictionary<string, TaskCompletionSource<object>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="InboxController" /> class.
        /// </summary>
        public InboxController()
            : this(DefaultInboxContainerName, DefaultInboxTableName, AzureStorageConfig.DefaultCloudConfigurationName)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InboxController" /> class.
        /// </summary>
        /// <param name="containerName">Name of the blob container.</param>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="cloudConfigurationName">Name of the cloud configuration.</param>
        /// <param name="httpHandler">The HTTP handler to use for outgoing HTTP requests.</param>
        public InboxController(string containerName, string tableName, string cloudConfigurationName, HttpMessageHandler httpHandler = null)
        {
            Requires.NotNullOrEmpty(containerName, "containerName");
            Requires.NotNullOrEmpty(cloudConfigurationName, "cloudConfigurationName");

            var storage = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings[cloudConfigurationName].ConnectionString);
            var blobClient = storage.CreateCloudBlobClient();
            this.InboxContainer = blobClient.GetContainerReference(containerName);
            var tableClient = storage.CreateCloudTableClient();
            this.InboxTable = new InboxContext(tableClient, tableName);
            this.HttpClient = new HttpClient(httpHandler ?? new HttpClientHandler());
            this.ClientTable = new PushNotificationContext(tableClient, WindowsPushNotificationClientController.DefaultTableName);
        }

        public PushNotificationContext ClientTable { get; set; }

        /// <summary>
        /// Gets or sets the inbox container.
        /// </summary>
        /// <value>
        /// The inbox container.
        /// </value>
        public CloudBlobContainer InboxContainer { get; set; }

        public InboxContext InboxTable { get; set; }

        public HttpClient HttpClient { get; set; }

        public static async Task PurgeExpiredAsync(CloudBlobContainer inboxContainer)
        {
            Requires.NotNull(inboxContainer, "inboxContainer");

            var deleteBlobsExpiringBefore = DateTime.UtcNow;
            int purgedBlobCount = 0;
            var searchExpiredBlobs = new TransformManyBlock<CloudBlobContainer, ICloudBlob>(
                async c =>
                {
                    try
                    {
                        var results = await c.ListBlobsSegmentedAsync(
                            string.Empty,
                            useFlatBlobListing: true,
                            pageSize: 50,
                            details: BlobListingDetails.Metadata,
                            options: new BlobRequestOptions(),
                            operationContext: null);
                        return from blob in results.OfType<ICloudBlob>()
                               let expires = DateTime.Parse(blob.Metadata[ExpirationDateMetadataKey])
                               where expires < deleteBlobsExpiringBefore
                               select blob;
                    }
                    catch (StorageException ex)
                    {
                        var webException = ex.InnerException as WebException;
                        if (webException != null)
                        {
                            var httpResponse = (HttpWebResponse)webException.Response;
                            if (httpResponse.StatusCode == HttpStatusCode.NotFound)
                            {
                                // it's legit that some tests never created the container to begin with.
                                return Enumerable.Empty<ICloudBlob>();
                            }
                        }

                        throw;
                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = 4,
                });
            var deleteBlobBlock = new ActionBlock<ICloudBlob>(
                blob =>
                {
                    Interlocked.Increment(ref purgedBlobCount);
                    return blob.DeleteAsync();
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 4,
                    BoundedCapacity = 100,
                });

            searchExpiredBlobs.LinkTo(deleteBlobBlock, new DataflowLinkOptions { PropagateCompletion = true });

            searchExpiredBlobs.Post(inboxContainer);
            searchExpiredBlobs.Complete();
            await deleteBlobBlock.Completion;
        }

        [HttpPost, ActionName("Create")]
        public async Task<JsonResult> CreateAsync()
        {
            var inbox = InboxEntity.Create();
            this.InboxTable.AddObject(inbox);
            await this.InboxTable.SaveChangesWithMergeAsync(inbox);

            string messageReceivingEndpoint = this.GetAbsoluteUrlForAction("Slot", new { id = inbox.RowKey }).AbsoluteUri;
            var result = new InboxCreationResponse
            {
                MessageReceivingEndpoint = messageReceivingEndpoint,
                InboxOwnerCode = inbox.InboxOwnerCode,
            };
            return new JsonResult { Data = result };
        }

        [HttpGet, ActionName("Slot"), InboxOwnerAuthorize]
        public async Task<ActionResult> GetInboxItemsAsync(string id, bool longPoll = false)
        {
            var blobs = await this.RetrieveInboxItemsAsync(id, longPoll);
            var list = new IncomingList() { Items = blobs };

            // Unit tests may not set this.Response
            if (this.Response != null)
            {
                // Help prevent clients such as WP8 from caching the result since they operate on it, then call us again
                this.Response.CacheControl = "no-cache";
            }

            return new JsonResult()
            {
                Data = list,
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        [HttpPost, ActionName("Slot")]
        public async Task<ActionResult> PostNotificationAsync(string id, int lifetime)
        {
            Requires.NotNullOrEmpty(id, "id");
            Requires.Range(lifetime > 0, "lifetime");

            if (this.Request.ContentLength > MaxNotificationSize)
            {
                throw new ArgumentException("Maximum message notification size exceeded.");
            }

            InboxEntity inbox = await this.GetInboxAsync(id);
            if (inbox == null)
            {
                return new HttpNotFoundResult();
            }

            var directory = this.InboxContainer.GetDirectoryReference(id);
            var blob = directory.GetBlockBlobReference(Utilities.CreateRandomWebSafeName(24));
            Debug.WriteLine("Defining blob: {0} ({1})", blob.Name, blob.Uri);

            var requestedLifeSpan = TimeSpan.FromMinutes(lifetime);
            var actualLifespan = requestedLifeSpan > MaxLifetimeCeiling ? MaxLifetimeCeiling : requestedLifeSpan;
            var expirationDate = DateTime.UtcNow + actualLifespan;
            blob.Metadata[ExpirationDateMetadataKey] = expirationDate.ToString(CultureInfo.InvariantCulture);

            await blob.UploadFromStreamAsync(this.Request.InputStream);

            // One more last ditch check that the max size was not exceeded, in case
            // the client is lying in the HTTP headers.
            if (blob.Properties.Length > MaxNotificationSize)
            {
                await blob.DeleteAsync();
                throw new ArgumentException("Maximum message notification size exceeded.");
            }

            // Notifying the receiver isn't something the sender needs to wait for.
            var nowait = Task.Run(async delegate
            {
                await this.AlertLongPollWaiterAsync(inbox);
                await this.InboxTable.SaveChangesWithMergeAsync(inbox);
            });
            return new HttpStatusCodeResult(HttpStatusCode.Created);
        }

        [HttpPut, ActionName("Slot"), InboxOwnerAuthorize]
        public async Task<ActionResult> PushChannelAsync(string id)
        {
            var inbox = await this.GetInboxAsync(id);

            if (this.Request.Form["channel_uri"] != null)
            {
                var channelUri = new Uri(this.Request.Form["channel_uri"], UriKind.Absolute);
                var content = this.Request.Form["channel_content"];
                Requires.Argument(content == null || content.Length <= 4096, "content", "Push content too large");

                inbox.PushChannelUri = channelUri.AbsoluteUri;
                inbox.PushChannelContent = content;
                inbox.ClientPackageSecurityIdentifier = this.Request.Form["package_security_identifier"];
            }
            else if (this.Request.Form["wp8_channel_uri"] != null)
            {
                var channelUri = new Uri(this.Request.Form["wp8_channel_uri"], UriKind.Absolute);
                var content = this.Request.Form["wp8_channel_content"];
                Requires.Argument(content == null || content.Length <= 4096, "content", "Push content too large");

                inbox.WinPhone8PushChannelUri = channelUri.AbsoluteUri;
                inbox.WinPhone8PushChannelContent = content;
                inbox.WinPhone8ToastText1 = this.Request.Form["wp8_channel_toast_text1"];
                inbox.WinPhone8ToastText2 = this.Request.Form["wp8_channel_toast_text2"];
                inbox.WinPhone8TileTemplate = this.Request.Form["wp8_channel_tile_template"];
            }
            else if (this.Request.Form["gcm_registration_id"] != null)
            {
                inbox.GoogleCloudMessagingRegistrationId = this.Request.Form["gcm_registration_id"];
            }
            else if (this.Request.Form["ios_device_token"] != null)
            {
                inbox.ApplePushNotificationGatewayDeviceToken = this.Request.Form["ios_device_token"];
            }
            else
            {
                // No data was posted. So skip updating the entity.
                return new HttpStatusCodeResult(HttpStatusCode.NoContent);
            }

            this.InboxTable.UpdateObject(inbox);
            await this.InboxTable.SaveChangesWithMergeAsync(inbox);
            return new HttpStatusCodeResult(HttpStatusCode.NoContent);
        }

        /// <summary>
        /// Deletes an inbox entirely.
        /// </summary>
        /// <param name="id">The ID of the inbox to delete.</param>
        /// <returns>The asynchronous operation.</returns>
        [NonAction] // to avoid ambiguity with the other overload.
        public Task<ActionResult> DeleteAsync(string id)
        {
            Requires.NotNullOrEmpty(id, "id");
            throw new NotImplementedException();
        }

        /// <summary>
        /// Deletes an individual notification from an inbox.
        /// </summary>
        /// <param name="id">The ID of the inbox with the notification to be deleted.</param>
        /// <param name="notification">The URL to the PayloadReference contained by the inbox item that should be deleted.</param>
        /// <returns>The asynchronous operation.</returns>
        [HttpDelete, ActionName("Slot"), InboxOwnerAuthorize]
        public async Task<ActionResult> DeleteAsync(string id, string notification)
        {
            Requires.NotNullOrEmpty(id, "id");

            if (notification == null)
            {
                return await this.DeleteAsync(id);
            }

            Requires.NotNullOrEmpty(notification, "notification");

            // The if check verifies that the notification URL is a blob that
            // belongs to the id'd container, thus ensuring that one valid user
            // can't delete another user's notifications.
            var directory = this.InboxContainer.GetDirectoryReference(id);
            if (directory.Uri.IsBaseOf(new Uri(notification, UriKind.Absolute)))
            {
                var blob = this.InboxContainer.GetBlockBlobReference(notification);
                try
                {
                    await blob.DeleteAsync();
                }
                catch (StorageException ex)
                {
                    if (ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.NotFound)
                    {
                        return new HttpNotFoundResult(ex.Message);
                    }

                    throw;
                }

                return new HttpStatusCodeResult(HttpStatusCode.NoContent);
            }
            else
            {
                return new HttpUnauthorizedResult("Notification URL does not match owner id.");
            }
        }

        internal static async Task OneTimeInitializeAsync(CloudStorageAccount azureAccount)
        {
            var inboxTable = azureAccount.CreateCloudTableClient();

            var blobClient = azureAccount.CreateCloudBlobClient();
            var inboxContainer = blobClient.GetContainerReference(DefaultInboxContainerName);

            await Task.WhenAll(
                    inboxContainer.CreateContainerWithPublicBlobsIfNotExistAsync(),
                    inboxTable.GetTableReference(DefaultInboxTableName).CreateIfNotExistsAsync());

            var nowait = Task.Run(
                async delegate
                {
                    while (true)
                    {
                        await PurgeExpiredAsync(inboxContainer);
                        await Task.Delay(AzureStorageConfig.PurgeExpiredBlobsInterval);
                    }
                });
        }

        protected virtual Uri GetAbsoluteUrlForAction(string action, dynamic routeValues)
        {
            return new Uri(this.Request.Url, this.Url.Action(action, routeValues));
        }

        private static Task WaitIncomingMessageAsync(string id)
        {
            TaskCompletionSource<object> tcs;
            lock (LongPollWaiters)
            {
                if (!LongPollWaiters.TryGetValue(id, out tcs))
                {
                    LongPollWaiters[id] = tcs = new TaskCompletionSource<object>();
                }
            }

            return tcs.Task;
        }

        private async Task<int> RetrieveInboxItemsCountAsync(string id)
        {
            int inboxCount = (await this.RetrieveInboxItemsAsync(id, longPoll: false)).Count;
            return inboxCount;
        }

        private async Task<List<IncomingList.IncomingItem>> RetrieveInboxItemsAsync(string id, bool longPoll)
        {
            var directory = this.InboxContainer.GetDirectoryReference(id);
            var blobs = new List<IncomingList.IncomingItem>();
            do
            {
                try
                {
                    var directoryListing = await directory.ListBlobsSegmentedAsync(
                        useFlatBlobListing: true,
                        pageSize: 50,
                        details: BlobListingDetails.Metadata,
                        options: new BlobRequestOptions(),
                        operationContext: null);
                    var notExpiringBefore = DateTime.UtcNow;
                    blobs.AddRange(
                        from blob in directoryListing.OfType<ICloudBlob>()
                        let expirationString = blob.Metadata[ExpirationDateMetadataKey]
                        where expirationString != null && DateTime.Parse(expirationString) > notExpiringBefore
                        select
                            new IncomingList.IncomingItem
                            {
                                Location = blob.Uri,
                                DatePostedUtc = blob.Properties.LastModified.Value.UtcDateTime
                            });
                }
                catch (StorageException)
                {
                }

                if (longPoll && blobs.Count == 0)
                {
                    await WaitIncomingMessageAsync(id).WithCancellation(this.Response.GetClientDisconnectedToken());
                }
            }
            while (longPoll && blobs.Count == 0);
            return blobs;
        }

        private async Task PushNotifyInboxMessageAsync(InboxEntity inbox)
        {
            Requires.NotNull(inbox, "inbox");

            await Task.WhenAll(
                this.PushNotifyInboxMessageWinStoreAsync(inbox),
                this.PushNotifyInboxMessageWinPhoneAsync(inbox),
                this.PushNotifyInboxMessageGoogleAsync(inbox),
                this.PushNotifyInboxMessageAppleAsync(inbox));
        }

        private async Task PushNotifyInboxMessageWinStoreAsync(InboxEntity inbox, int failedAttempts = 0)
        {
            if (string.IsNullOrEmpty(inbox.ClientPackageSecurityIdentifier) || string.IsNullOrEmpty(inbox.PushChannelUri))
            {
                return;
            }

            var client = await this.ClientTable.GetAsync(inbox.ClientPackageSecurityIdentifier);
            string bearerToken = client.AccessToken;
            var pushNotifyRequest = new HttpRequestMessage(HttpMethod.Post, inbox.PushChannelUri);
            pushNotifyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            pushNotifyRequest.Headers.Add("X-WNS-Type", "wns/raw");
            pushNotifyRequest.Content = new StringContent(inbox.PushChannelContent ?? string.Empty);

            // yes, it's a string, but we must claim it's an octet-stream
            pushNotifyRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await this.HttpClient.SendAsync(pushNotifyRequest);
            if (response.IsSuccessStatusCode)
            {
                inbox.LastWindows8PushNotificationUtc = DateTime.UtcNow;
                this.InboxTable.UpdateObject(inbox);
            }
            else
            {
                if (failedAttempts == 0)
                {
                    var authHeader = response.Headers.WwwAuthenticate.FirstOrDefault();
                    if (authHeader != null)
                    {
                        if (authHeader.Parameter.Contains("Token expired"))
                        {
                            await client.AcquireWnsPushBearerTokenAsync(this.HttpClient);
                            this.ClientTable.UpdateObject(client);
                            await this.ClientTable.SaveChangesAsync();
                            await this.PushNotifyInboxMessageWinStoreAsync(inbox, failedAttempts + 1);
                            return;
                        }
                    }
                }

                // Log a failure.
                // TODO: code here.
            }
        }

        private async Task PushNotifyInboxMessageWinPhoneAsync(InboxEntity inbox)
        {
            if (!string.IsNullOrEmpty(inbox.WinPhone8PushChannelUri))
            {
                var notifications = new WinPhonePushNotifications(this.HttpClient, new Uri(inbox.WinPhone8PushChannelUri));

                int count = await this.RetrieveInboxItemsCountAsync(inbox.RowKey);
                bool invalidChannel = false;
                try
                {
                    var pushTile = notifications.PushWinPhoneTileAsync(inbox.WinPhone8TileTemplate, count: count);
                    Task<bool> pushToast = Task.FromResult(false);
                    if (!string.IsNullOrEmpty(inbox.WinPhone8ToastText1) || !string.IsNullOrEmpty(inbox.WinPhone8ToastText2))
                    {
                        var line1 = string.Format(CultureInfo.InvariantCulture, inbox.WinPhone8ToastText1 ?? string.Empty, count);
                        var line2 = string.Format(CultureInfo.InvariantCulture, inbox.WinPhone8ToastText2 ?? string.Empty, count);
                        if (inbox.LastWinPhone8PushNotificationUtc.HasValue && inbox.LastAuthenticatedInteractionUtc.HasValue && inbox.LastWinPhone8PushNotificationUtc.Value > inbox.LastAuthenticatedInteractionUtc.Value)
                        {
                            // We've sent a toast notification more recently than the user has checked messages,
                            // so there's no reason to send another for now.
                            pushToast = Task.FromResult(true);
                        }
                        else
                        {
                            pushToast = notifications.PushWinPhoneToastAsync(line1, line2);
                        }
                    }

                    Task<bool> pushRaw = Task.FromResult(false);
                    if (!string.IsNullOrEmpty(inbox.WinPhone8PushChannelContent))
                    {
                        pushRaw = notifications.PushWinPhoneRawNotificationAsync(inbox.WinPhone8PushChannelContent);
                    }

                    await Task.WhenAll(pushTile, pushToast, pushRaw);
                    invalidChannel |= !(pushTile.Result || pushToast.Result || pushRaw.Result);
                }
                catch (HttpRequestException)
                {
                    invalidChannel = true;
                }

                if (invalidChannel)
                {
                    inbox.WinPhone8PushChannelUri = null;
                    inbox.WinPhone8PushChannelContent = null;
                    inbox.WinPhone8ToastText1 = null;
                    inbox.WinPhone8ToastText2 = null;
                }
                else
                {
                    inbox.LastWinPhone8PushNotificationUtc = DateTime.UtcNow;
                }

                this.InboxTable.UpdateObject(inbox);
            }
        }

        private async Task PushNotifyInboxMessageGoogleAsync(InboxEntity inbox)
        {
            if (!string.IsNullOrEmpty(inbox.GoogleCloudMessagingRegistrationId))
            {
                var notifications = new GooglePushNotifications(this.HttpClient, ConfigurationManager.AppSettings["GoogleApiKey"], inbox.GoogleCloudMessagingRegistrationId);

                bool invalidChannel = false;
                try
                {
                    bool successfulPush = await notifications.PushGoogleRawNotificationAsync(CancellationToken.None);
                    invalidChannel |= !successfulPush;
                }
                catch (HttpRequestException)
                {
                    invalidChannel = true;
                }

                if (invalidChannel)
                {
                    inbox.GoogleCloudMessagingRegistrationId = null;
                    this.InboxTable.UpdateObject(inbox);
                }
            }
        }

        private async Task PushNotifyInboxMessageAppleAsync(InboxEntity inbox)
        {
            if (MvcApplication.IsApplePushRegistered)
            {
                if (!string.IsNullOrEmpty(inbox.ApplePushNotificationGatewayDeviceToken))
                {
                    int count = await this.RetrieveInboxItemsCountAsync(inbox.RowKey);
                    MvcApplication.PushBroker.QueueNotification(new AppleNotification()
                        .ForDeviceToken(inbox.ApplePushNotificationGatewayDeviceToken)
                        .WithBadge(count));
                }
            }
        }

        private async Task AlertLongPollWaiterAsync(InboxEntity inbox)
        {
            Requires.NotNull(inbox, "inbox");

            await this.PushNotifyInboxMessageAsync(inbox);

            var id = inbox.RowKey;
            TaskCompletionSource<object> tcs;
            lock (LongPollWaiters)
            {
                if (LongPollWaiters.TryGetValue(id, out tcs))
                {
                    LongPollWaiters.Remove(id);
                }
            }

            if (tcs != null)
            {
                tcs.TrySetResult(null);
            }
        }

        private async Task<InboxEntity> GetInboxAsync(string id)
        {
            var queryResults = await this.InboxTable.Get(id).ExecuteSegmentedAsync();
            return queryResults.FirstOrDefault();
        }
    }
}
