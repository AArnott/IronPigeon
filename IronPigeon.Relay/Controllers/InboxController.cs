namespace IronPigeon.Relay.Controllers {
	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;
	using System.Threading.Tasks.Dataflow;
	using System.Web;
	using System.Web.Mvc;
	using IronPigeon.Relay.Models;
	using Microsoft;
	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.StorageClient;

	////[RequireHttps]
	public class InboxController : Controller {
		/// <summary>
		/// The key into a blob's metadata that stores the blob's expiration date.
		/// </summary>
		public const string ExpirationDateMetadataKey = "expiration_date";

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

		/// <summary>
		/// The key to the Azure account configuration information.
		/// </summary>
		private const string DefaultCloudConfigurationName = "StorageConnectionString";

		/// <summary>
		/// Initializes a new instance of the <see cref="InboxController" /> class.
		/// </summary>
		public InboxController()
			: this(DefaultInboxContainerName, DefaultInboxTableName, DefaultCloudConfigurationName) {
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

			var storage = CloudStorageAccount.FromConfigurationSetting(cloudConfigurationName);
			var blobClient = storage.CreateCloudBlobClient();
			this.InboxContainer = blobClient.GetContainerReference(containerName);
			var tableClient = storage.CreateCloudTableClient();
			this.InboxTable = new InboxContext(tableClient, tableName);
		}

		/// <summary>
		/// Gets or sets the inbox container.
		/// </summary>
		/// <value>
		/// The inbox container.
		/// </value>
		public CloudBlobContainer InboxContainer { get; set; }

		public InboxContext InboxTable { get; set; }

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
		public async Task<ActionResult> GetInboxItemsAsync(string id) {
			await this.EnsureContainerInitializedAsync();

			InboxEntity inbox = await this.GetInboxAsync(id);
			if (inbox == null) {
				return new HttpNotFoundResult();
			}

			var directory = this.InboxContainer.GetDirectoryReference(id);
			var blobs = new List<IncomingList.IncomingItem>();
			try {
				var directoryListing = await directory.ListBlobsSegmentedAsync(50);
				var notExpiringBefore = DateTime.UtcNow;
				blobs.AddRange(
					from blob in directoryListing.OfType<CloudBlob>()
					where DateTime.Parse(blob.Metadata[ExpirationDateMetadataKey]) > notExpiringBefore
					select new IncomingList.IncomingItem { Location = blob.Uri, DatePostedUtc = blob.Properties.LastModifiedUtc });
			} catch (StorageClientException) {
			}

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
			await this.EnsureContainerInitializedAsync();

			if (this.Request.ContentLength > MaxNotificationSize) {
				throw new ArgumentException("Maximum message notification size exceeded.");
			}

			InboxEntity inbox = await this.GetInboxAsync(id);
			if (inbox == null) {
				return new HttpNotFoundResult();
			}

			var directory = this.InboxContainer.GetDirectoryReference(id);
			var blob = directory.GetBlobReference(Utilities.CreateRandomWebSafeName(24));
			await blob.UploadFromStreamAsync(this.Request.InputStream);

			// One more last ditch check that the max size was not exceeded, in case
			// the client is lying in the HTTP headers.
			if (blob.Properties.Length > MaxNotificationSize) {
				await blob.DeleteAsync();
				throw new ArgumentException("Maximum message notification size exceeded.");
			}

			var requestedLifeSpan = TimeSpan.FromMinutes(lifetime);
			var actualLifespan = requestedLifeSpan > MaxLifetimeCeiling ? MaxLifetimeCeiling : requestedLifeSpan;
			var expirationDate = DateTime.UtcNow + actualLifespan;
			blob.Metadata[ExpirationDateMetadataKey] = expirationDate.ToString(CultureInfo.InvariantCulture);
			await blob.SetMetadataAsync();
			return new EmptyResult();
		}

		/// <summary>
		/// Deletes an inbox entirely.
		/// </summary>
		[NonAction] // to avoid ambiguity with the other overload.
		public async Task<ActionResult> DeleteAsync(string id) {
			Requires.NotNullOrEmpty(id, "id");
			throw new NotImplementedException();
		}

		/// <summary>
		/// Deletes an individual notification from an inbox.
		/// </summary>
		[HttpDelete, ActionName("Slot"), InboxOwnerAuthorize]
		public async Task<ActionResult> DeleteAsync(string id, string notification) {
			Requires.NotNullOrEmpty(id, "id");

			if (notification == null) {
				return await DeleteAsync(id);
			}

			Requires.NotNullOrEmpty(notification, "notification");

			// The if check verifies that the notification URL is a blob that
			// belongs to the id'd container, thus ensuring that one valid user
			// can't delete another user's notifications.
			var directory = this.InboxContainer.GetDirectoryReference(id);
			if (directory.Uri.IsBaseOf(new Uri(notification, UriKind.Absolute))) {
				var blob = this.InboxContainer.GetBlobReference(notification);
				await blob.DeleteAsync();
				return new EmptyResult();
			} else {
				return new HttpUnauthorizedResult("Notification URL does not match owner id.");
			}
		}

		[NonAction, ActionName("Purge"), Authorize(Roles = "admin")]
		public Task PurgeExpiredAsync() {
			var deleteBlobsExpiringBefore = DateTime.UtcNow;
			var searchExpiredBlobs = new TransformManyBlock<CloudBlobContainer, CloudBlob>(
				async c => {
					try {
						var results = await c.ListBlobsSegmentedAsync(10);
						return from blob in results.OfType<CloudBlob>()
							   let expires = DateTime.Parse(blob.Metadata[ExpirationDateMetadataKey])
							   where expires < deleteBlobsExpiringBefore
							   select blob;
					} catch (StorageClientException ex) {
						var webException = ex.InnerException as WebException;
						if (webException != null) {
							var httpResponse = (HttpWebResponse)webException.Response;
							if (httpResponse.StatusCode == HttpStatusCode.NotFound) {
								// it's legit that some tests never created the container to begin with.
								return Enumerable.Empty<CloudBlob>();
							}
						}

						throw;
					}
				},
				new ExecutionDataflowBlockOptions {
					BoundedCapacity = 4,
				});
			var deleteBlobBlock = new ActionBlock<CloudBlob>(
				blob => blob.DeleteAsync(),
				new ExecutionDataflowBlockOptions {
					MaxDegreeOfParallelism = 4,
					BoundedCapacity = 100,
				});

			searchExpiredBlobs.LinkTo(deleteBlobBlock, new DataflowLinkOptions { PropagateCompletion = true });

			searchExpiredBlobs.Post(this.InboxContainer);
			searchExpiredBlobs.Complete();
			return deleteBlobBlock.Completion;
		}

		protected virtual Uri GetAbsoluteUrlForAction(string action, dynamic routeValues) {
			return new Uri(this.Request.Url, this.Url.Action(action, routeValues));
		}

		private async Task<InboxEntity> GetInboxAsync(string id) {
			var queryResults = await this.InboxTable.Get(id).ExecuteAsync();
			return queryResults.FirstOrDefault();
		}

		private async Task EnsureContainerInitializedAsync() {
			await this.InboxContainer.CreateIfNotExistAsync();

			var permissions = new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob };
			await this.InboxContainer.SetPermissionsAsync(permissions);
		}
	}
}
