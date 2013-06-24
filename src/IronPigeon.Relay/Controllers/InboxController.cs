namespace IronPigeon.Relay.Controllers {
	using System;
	using System.Collections.Generic;
	using System.Configuration;
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
	using Microsoft.WindowsAzure.StorageClient;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;
	using Validation;

#if !DEBUG
	[RequireHttps]
#endif
	public class InboxController : Controller {
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
			: this(DefaultInboxContainerName, DefaultInboxTableName, AzureStorageConfig.DefaultCloudConfigurationName) {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="InboxController" /> class.
		/// </summary>
		/// <param name="containerName">Name of the blob container.</param>
		/// <param name="tableName">Name of the table.</param>
		/// <param name="cloudConfigurationName">Name of the cloud configuration.</param>
		public InboxController(string containerName, string tableName, string cloudConfigurationName) {
			Requires.NotNullOrEmpty(containerName, "containerName");
			Requires.NotNullOrEmpty(cloudConfigurationName, "cloudConfigurationName");

			var storage = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings[cloudConfigurationName].ConnectionString);
			var blobClient = storage.CreateCloudBlobClient();
			this.InboxContainer = blobClient.GetContainerReference(containerName);
			var tableClient = storage.CreateCloudTableClient();
			this.InboxTable = new InboxContext(tableClient, tableName);
			this.HttpClient = new HttpClient();
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

		public static async Task PurgeExpiredAsync(CloudBlobContainer inboxContainer) {
			Requires.NotNull(inboxContainer, "inboxContainer");

			var deleteBlobsExpiringBefore = DateTime.UtcNow;
			int purgedBlobCount = 0;
			var searchExpiredBlobs = new TransformManyBlock<CloudBlobContainer, ICloudBlob>(
				async c => {
					try {
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
					} catch (StorageException ex) {
						var webException = ex.InnerException as WebException;
						if (webException != null) {
							var httpResponse = (HttpWebResponse)webException.Response;
							if (httpResponse.StatusCode == HttpStatusCode.NotFound) {
								// it's legit that some tests never created the container to begin with.
								return Enumerable.Empty<ICloudBlob>();
							}
						}

						throw;
					}
				},
				new ExecutionDataflowBlockOptions {
					BoundedCapacity = 4,
				});
			var deleteBlobBlock = new ActionBlock<ICloudBlob>(
				blob => {
					Interlocked.Increment(ref purgedBlobCount);
					return blob.DeleteAsync();
				},
				new ExecutionDataflowBlockOptions {
					MaxDegreeOfParallelism = 4,
					BoundedCapacity = 100,
				});

			searchExpiredBlobs.LinkTo(deleteBlobBlock, new DataflowLinkOptions { PropagateCompletion = true });

			searchExpiredBlobs.Post(inboxContainer);
			searchExpiredBlobs.Complete();
			await deleteBlobBlock.Completion;
		}

		[HttpPost, ActionName("Create")]
		public async Task<JsonResult> CreateAsync() {
			var inbox = InboxEntity.Create();
			this.InboxTable.AddObject(inbox);
			await this.InboxTable.SaveChangesAsync();

			string messageReceivingEndpoint = this.GetAbsoluteUrlForAction("Slot", new { id = inbox.RowKey }).AbsoluteUri;
			var result = new InboxCreationResponse {
				MessageReceivingEndpoint = messageReceivingEndpoint,
				InboxOwnerCode = inbox.InboxOwnerCode,
			};
			return new JsonResult { Data = result };
		}

		[HttpGet, ActionName("Slot"), InboxOwnerAuthorize]
		public async Task<ActionResult> GetInboxItemsAsync(string id, bool longPoll = false) {
			var directory = this.InboxContainer.GetDirectoryReference(id);
			var blobs = new List<IncomingList.IncomingItem>();
			do {
				try {
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
						select new IncomingList.IncomingItem { Location = blob.Uri, DatePostedUtc = blob.Properties.LastModified.Value.UtcDateTime });
				} catch (StorageException) {
				}

				if (longPoll && blobs.Count == 0) {
					await WaitIncomingMessageAsync(id).WithCancellation(this.Response.GetClientDisconnectedToken());
				}
			} while (longPoll && blobs.Count == 0);

			var list = new IncomingList() { Items = blobs };
			return new JsonResult() {
				Data = list,
				JsonRequestBehavior = JsonRequestBehavior.AllowGet
			};
		}

		[HttpPost, ActionName("Slot")]
		public async Task<ActionResult> PostNotificationAsync(string id, int lifetime) {
			Requires.NotNullOrEmpty(id, "id");
			Requires.Range(lifetime > 0, "lifetime");

			if (this.Request.ContentLength > MaxNotificationSize) {
				throw new ArgumentException("Maximum message notification size exceeded.");
			}

			InboxEntity inbox = await this.GetInboxAsync(id);
			if (inbox == null) {
				return new HttpNotFoundResult();
			}

			var directory = this.InboxContainer.GetDirectoryReference(id);
			var blob = directory.GetBlockBlobReference(Utilities.CreateRandomWebSafeName(24));

			var requestedLifeSpan = TimeSpan.FromMinutes(lifetime);
			var actualLifespan = requestedLifeSpan > MaxLifetimeCeiling ? MaxLifetimeCeiling : requestedLifeSpan;
			var expirationDate = DateTime.UtcNow + actualLifespan;
			blob.Metadata[ExpirationDateMetadataKey] = expirationDate.ToString(CultureInfo.InvariantCulture);

			await blob.UploadFromStreamAsync(this.Request.InputStream);

			// One more last ditch check that the max size was not exceeded, in case
			// the client is lying in the HTTP headers.
			if (blob.Properties.Length > MaxNotificationSize) {
				await blob.DeleteAsync();
				throw new ArgumentException("Maximum message notification size exceeded.");
			}

			await this.AlertLongPollWaiterAsync(inbox);

			return new EmptyResult();
		}

		[HttpPut, ActionName("Slot"), InboxOwnerAuthorize]
		public async Task<ActionResult> PushChannelAsync(string id) {
			var channelUri = new Uri(this.Request.Form["channel_uri"], UriKind.Absolute);
			var content = this.Request.Form["channel_content"];
			Requires.Argument(content == null || content.Length <= 4096, "content", "Push content too large");

			var inbox = await this.GetInboxAsync(id);
			inbox.PushChannelUri = channelUri.AbsoluteUri;
			inbox.PushChannelContent = content;
			inbox.ClientPackageSecurityIdentifier = this.Request.Form["package_security_identifier"];
			this.InboxTable.UpdateObject(inbox);
			await this.InboxTable.SaveChangesAsync();
			return new EmptyResult();
		}

		/// <summary>
		/// Deletes an inbox entirely.
		/// </summary>
		/// <param name="id">The ID of the inbox to delete.</param>
		/// <returns>The asynchronous operation.</returns>
		[NonAction] // to avoid ambiguity with the other overload.
		public Task<ActionResult> DeleteAsync(string id) {
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
		public async Task<ActionResult> DeleteAsync(string id, string notification) {
			Requires.NotNullOrEmpty(id, "id");

			if (notification == null) {
				return await this.DeleteAsync(id);
			}

			Requires.NotNullOrEmpty(notification, "notification");

			// The if check verifies that the notification URL is a blob that
			// belongs to the id'd container, thus ensuring that one valid user
			// can't delete another user's notifications.
			var directory = this.InboxContainer.GetDirectoryReference(id);
			if (directory.Uri.IsBaseOf(new Uri(notification, UriKind.Absolute))) {
				var blob = this.InboxContainer.GetBlockBlobReference(notification);
				try {
					await blob.DeleteAsync();
				} catch (StorageException ex) {
					if (ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.NotFound) {
						return new HttpNotFoundResult(ex.Message);
					}

					throw;
				}

				return new EmptyResult();
			} else {
				return new HttpUnauthorizedResult("Notification URL does not match owner id.");
			}
		}

		internal static async Task OneTimeInitializeAsync(CloudStorageAccount azureAccount) {
			var inboxTable = azureAccount.CreateCloudTableClient();

			var blobClient = azureAccount.CreateCloudBlobClient();
			var inboxContainer = blobClient.GetContainerReference(DefaultInboxContainerName);

			await Task.WhenAll(
					inboxContainer.CreateContainerWithPublicBlobsIfNotExistAsync(),
					inboxTable.GetTableReference(DefaultInboxTableName).CreateIfNotExistsAsync());

			Task.Run(
				async delegate {
					while (true) {
						await PurgeExpiredAsync(inboxContainer);
						await Task.Delay(AzureStorageConfig.PurgeExpiredBlobsInterval);
					}
				});
		}

		protected virtual Uri GetAbsoluteUrlForAction(string action, dynamic routeValues) {
			return new Uri(this.Request.Url, this.Url.Action(action, routeValues));
		}

		private static Task WaitIncomingMessageAsync(string id) {
			TaskCompletionSource<object> tcs;
			lock (LongPollWaiters) {
				if (!LongPollWaiters.TryGetValue(id, out tcs)) {
					LongPollWaiters[id] = tcs = new TaskCompletionSource<object>();
				}
			}

			return tcs.Task;
		}

		private async Task PushNotifyInboxMessageAsync(InboxEntity inbox, int failedAttempts = 0) {
			Requires.NotNull(inbox, "inbox");

			if (string.IsNullOrEmpty(inbox.ClientPackageSecurityIdentifier)) {
				return;
			}

			var client = await this.ClientTable.GetAsync(inbox.ClientPackageSecurityIdentifier);
			string bearerToken = client.AccessToken;
			var pushNotifyRequest = new HttpRequestMessage(HttpMethod.Post, inbox.PushChannelUri);
			pushNotifyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
			pushNotifyRequest.Headers.Add("X-WNS-Type", "wns/raw");
			pushNotifyRequest.Content = new StringContent(inbox.PushChannelContent ?? string.Empty);
			pushNotifyRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream"); // yes, it's a string, but we must claim it's an octet-stream

			var response = await this.HttpClient.SendAsync(pushNotifyRequest);
			if (!response.IsSuccessStatusCode) {
				if (failedAttempts == 0) {
					var authHeader = response.Headers.WwwAuthenticate.FirstOrDefault();
					if (authHeader != null) {
						if (authHeader.Parameter.Contains("Token expired")) {
							await client.AcquireWnsPushBearerTokenAsync(this.HttpClient);
							this.ClientTable.UpdateObject(client);
							await this.ClientTable.SaveChangesAsync();
							await this.PushNotifyInboxMessageAsync(inbox, failedAttempts + 1);
							return;
						}
					}
				}

				// Log a failure.
				// TODO: code here.
			}
		}

		private async Task AlertLongPollWaiterAsync(InboxEntity inbox) {
			Requires.NotNull(inbox, "inbox");

			if (inbox.PushChannelUri != null) {
				await this.PushNotifyInboxMessageAsync(inbox);
			}

			var id = inbox.RowKey;
			TaskCompletionSource<object> tcs;
			lock (LongPollWaiters) {
				if (LongPollWaiters.TryGetValue(id, out tcs)) {
					LongPollWaiters.Remove(id);
				}
			}

			if (tcs != null) {
				tcs.TrySetResult(null);
			}
		}

		private async Task<InboxEntity> GetInboxAsync(string id) {
			var queryResults = await this.InboxTable.Get(id).ExecuteSegmentedAsync();
			return queryResults.FirstOrDefault();
		}
	}
}
