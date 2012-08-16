namespace IronPigeon.Providers {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Threading.Tasks.Dataflow;
	using Microsoft;
	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.StorageClient;

	public class AzureBlobStorage : ICloudBlobStorageProvider {
		private readonly CloudStorageAccount account;
		private readonly CloudBlobClient client;
		private readonly CloudBlobContainer container;

		public AzureBlobStorage(CloudStorageAccount account, string containerAddress) {
			Requires.NotNull(account, "account");
			Requires.NotNullOrEmpty(containerAddress, "containerAddress");
			Requires.Argument(DesktopUtilities.IsValidBlobContainerName(containerAddress), "containerAddress", "Invalid container name.");

			this.account = account;
			this.client = this.account.CreateCloudBlobClient();
			this.container = this.client.GetContainerReference(containerAddress);
		}

		#region ICloudBlobStorageProvider Members

		public async Task<Uri> UploadMessageAsync(Stream content, DateTime expirationUtc, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(content, "content");
			Requires.Range(expirationUtc > DateTime.UtcNow, "expirationUtc");

			string blobName = Utilities.CreateRandomWebSafeName(DesktopUtilities.BlobNameLength);
			DateTime roundedUp = expirationUtc - expirationUtc.TimeOfDay + TimeSpan.FromDays(1);
			blobName = roundedUp.ToString("yyyy.MM.dd") + "/" + blobName;

			var blob = this.container.GetBlobReference(blobName);
			blob.Metadata["DeleteAfter"] = expirationUtc.ToString();
			await blob.UploadFromStreamAsync(content);
			return blob.Uri;
		}

		#endregion

		/// <summary>
		/// Creates the blob container if it does not exist, and sets its public access permission to allow
		/// downloading of blobs by their URIs.
		/// </summary>
		public async Task CreateContainerIfNotExistAsync() {
			if (await this.container.CreateIfNotExistAsync()) {
				var permissions = new BlobContainerPermissions {
					PublicAccess = BlobContainerPublicAccessType.Blob,
				};
				await this.container.SetPermissionsAsync(permissions, null);
			}
		}

		/// <summary>
		/// Purges all blobs set to expire prior to the specified date.
		/// </summary>
		/// <param name="deleteBlobsExpiringBefore">
		/// All blobs scheduled to expire prior to this date will be purged.  The default value
		/// is interpreted as <see cref="DateTime.UtcNow"/>.
		/// </param>
		public Task PurgeBlobsExpiringBeforeAsync(DateTime deleteBlobsExpiringBefore = default(DateTime)) {
			Requires.Argument(deleteBlobsExpiringBefore.Kind == DateTimeKind.Utc, "expirationUtc", "UTC required.");

			if (deleteBlobsExpiringBefore == default(DateTime)) {
				deleteBlobsExpiringBefore = DateTime.UtcNow;
			}

			var searchExpiredDirectoriesBlock = new TransformManyBlock<CloudBlobContainer, CloudBlobDirectory>(
				async c => {
					var results = await c.ListBlobsSegmentedAsync(10);
					return from directory in results.OfType<CloudBlobDirectory>()
						   let expires = DateTime.Parse(directory.Uri.Segments[directory.Uri.Segments.Length - 1].TrimEnd('/'))
						   where expires < deleteBlobsExpiringBefore
						   select directory;
				},
				new ExecutionDataflowBlockOptions {
					BoundedCapacity = 4,
				});
			var deleteDirectoryBlock = new TransformManyBlock<CloudBlobDirectory, CloudBlob>(
				async directory => {
					var results = await directory.ListBlobsSegmentedAsync(10);
					return results.OfType<CloudBlob>();
				},
				new ExecutionDataflowBlockOptions {
					MaxDegreeOfParallelism = 2,
					BoundedCapacity = 4,
				});
			var deleteBlobBlock = new ActionBlock<CloudBlob>(
				blob => blob.DeleteAsync(),
				new ExecutionDataflowBlockOptions {
					MaxDegreeOfParallelism = 4,
					BoundedCapacity = 100,
				});

			searchExpiredDirectoriesBlock.LinkTo(deleteDirectoryBlock, new DataflowLinkOptions { PropagateCompletion = true });
			deleteDirectoryBlock.LinkTo(deleteBlobBlock, new DataflowLinkOptions { PropagateCompletion = true });

			searchExpiredDirectoriesBlock.Post(this.container);
			searchExpiredDirectoriesBlock.Complete();
			return deleteBlobBlock.Completion;
		}
	}
}
