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

	using Microsoft;
	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.StorageClient;

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

		/// <summary>
		/// The key to the Azure account configuration information.
		/// </summary>
		private const string DefaultCloudConfigurationName = "StorageConnectionString";

		private static readonly char[] DisallowedThumbprintCharacters = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

		/// <summary>
		/// Initializes a new instance of the <see cref="InboxController" /> class.
		/// </summary>
		public InboxController()
			: this(DefaultInboxContainerName, DefaultCloudConfigurationName) {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="InboxController" /> class.
		/// </summary>
		/// <param name="containerName">Name of the container.</param>
		/// <param name="cloudConfigurationName">Name of the cloud configuration.</param>
		public InboxController(string containerName, string cloudConfigurationName) {
			Requires.NotNullOrEmpty(containerName, "containerName");
			Requires.NotNullOrEmpty(cloudConfigurationName, "cloudConfigurationName");

			var storage = CloudStorageAccount.FromConfigurationSetting(cloudConfigurationName);
			var client = storage.CreateCloudBlobClient();
			this.InboxContainer = client.GetContainerReference(containerName);
		}

		/// <summary>
		/// Gets or sets the inbox container.
		/// </summary>
		/// <value>
		/// The inbox container.
		/// </value>
		public CloudBlobContainer InboxContainer { get; set; }

		[HttpPost]
		public async Task<ActionResult> Create() {
			return new EmptyResult();
		}

		[HttpGet, ActionName("Index")]
		public async Task<ActionResult> GetInboxItemsAsync(string thumbprint) {
			VerifyValidThumbprint(thumbprint);
			await this.EnsureContainerInitializedAsync();

			var directory = this.InboxContainer.GetDirectoryReference(thumbprint);
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

		[HttpPost, ActionName("Index")]
		public async Task<ActionResult> PostNotification(string thumbprint, int lifetime) {
			VerifyValidThumbprint(thumbprint);
			Requires.Range(lifetime > 0, "lifetime");
			await this.EnsureContainerInitializedAsync();

			if (this.Request.ContentLength > MaxNotificationSize) {
				throw new ArgumentException("Maximum message notification size exceeded.");
			}

			var directory = this.InboxContainer.GetDirectoryReference(thumbprint);
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

		[HttpPost]
		public async Task<ActionResult> Delete(string thumbprint, string notification) {
			Requires.NotNullOrEmpty(thumbprint, "thumbprint");
			Requires.NotNullOrEmpty(notification, "notification");

			// TODO: Add authentication to delete so attackers can't delete others' notifications.
			var blob = this.InboxContainer.GetBlobReference(notification);
			await blob.DeleteAsync();
			return new EmptyResult();
		}

		[NonAction]
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

		private static void VerifyValidThumbprint(string thumbprint) {
			Requires.NotNullOrEmpty(thumbprint, "thumbprint");
			Requires.Argument(thumbprint.IndexOfAny(DisallowedThumbprintCharacters) < 0, "thumbprint", "Disallowed characters.");
		}

		private async Task EnsureContainerInitializedAsync() {
			await this.InboxContainer.CreateIfNotExistAsync();

			var permissions = new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob };
			await this.InboxContainer.SetPermissionsAsync(permissions);
		}
	}
}
