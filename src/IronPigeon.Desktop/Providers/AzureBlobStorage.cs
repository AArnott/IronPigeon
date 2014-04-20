namespace IronPigeon.Providers {
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Threading.Tasks.Dataflow;
	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.Storage;
	using Microsoft.WindowsAzure.Storage.Blob;
	using Microsoft.WindowsAzure.StorageClient;
	using Validation;

	/// <summary>
	/// A cloud blob storage provider that uses Azure blob storage directly.
	/// </summary>
	public class AzureBlobStorage : ICloudBlobStorageProvider {
		/// <summary>
		/// The Azure storage account.
		/// </summary>
		private readonly CloudStorageAccount account;

		/// <summary>
		/// The blob client.
		/// </summary>
		private readonly CloudBlobClient client;

		/// <summary>
		/// The container in which to store blobs.
		/// </summary>
		private readonly CloudBlobContainer container;

		/// <summary>
		/// Initializes a new instance of the <see cref="AzureBlobStorage" /> class.
		/// </summary>
		/// <param name="account">The Azure account to use.</param>
		/// <param name="containerAddress">The name of the Azure blob container to use for uploaded blobs.</param>
		public AzureBlobStorage(CloudStorageAccount account, string containerAddress) {
			Requires.NotNull(account, "account");
			Requires.NotNullOrEmpty(containerAddress, "containerAddress");
			Requires.Argument(DesktopUtilities.IsValidBlobContainerName(containerAddress), "containerAddress", "Invalid container name.");

			this.account = account;
			this.client = this.account.CreateCloudBlobClient();
			this.container = this.client.GetContainerReference(containerAddress);
		}

		#region ICloudBlobStorageProvider Members

		/// <inheritdoc/>
		public async Task<Uri> UploadMessageAsync(Stream content, DateTime expirationUtc, string contentType, string contentEncoding, IProgress<int> bytesCopiedProgress, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(content, "content");
			Requires.Range(expirationUtc > DateTime.UtcNow, "expirationUtc");

			string blobName = Utilities.CreateRandomWebSafeName(DesktopUtilities.BlobNameLength);
			if (expirationUtc < DateTime.MaxValue) {
				DateTime roundedUp = expirationUtc - expirationUtc.TimeOfDay + TimeSpan.FromDays(1);
				blobName = roundedUp.ToString("yyyy.MM.dd") + "/" + blobName;
			}

			var blob = this.container.GetBlockBlobReference(blobName);

			// Set metadata with the precise expiration time, although for efficiency we also put the blob into a directory
			// for efficient deletion based on approximate expiration date.
			if (expirationUtc < DateTime.MaxValue) {
				blob.Metadata["DeleteAfter"] = expirationUtc.ToString(CultureInfo.InvariantCulture);
			}

			blob.Properties.ContentType = contentType;
			blob.Properties.ContentEncoding = contentEncoding;

			await blob.UploadFromStreamAsync(content.ReadStreamWithProgress(bytesCopiedProgress), cancellationToken);
			return blob.Uri;
		}

		#endregion

		/// <summary>
		/// Creates the blob container if it does not exist, and sets its public access permission to allow
		/// downloading of blobs by their URIs.
		/// </summary>
		/// <returns>The task representing the asynchronous operation.</returns>
		public Task CreateContainerIfNotExistAsync() {
			return this.container.CreateContainerWithPublicBlobsIfNotExistAsync();
		}

		/// <summary>
		/// Purges all blobs set to expire prior to the specified date.
		/// </summary>
		/// <param name="deleteBlobsExpiringBefore">
		/// All blobs scheduled to expire prior to this date will be purged.  The default value
		/// is interpreted as <see cref="DateTime.UtcNow"/>.
		/// </param>
		/// <returns>The task representing the asynchronous operation.</returns>
		public async Task PurgeBlobsExpiringBeforeAsync(DateTime deleteBlobsExpiringBefore = default(DateTime)) {
			if (deleteBlobsExpiringBefore == default(DateTime)) {
				deleteBlobsExpiringBefore = DateTime.UtcNow;
			}

			Requires.Argument(deleteBlobsExpiringBefore.Kind == DateTimeKind.Utc, "expirationUtc", "UTC required.");

			var searchExpiredDirectoriesBlock = new TransformManyBlock<CloudBlobContainer, CloudBlobDirectory>(
				async c => {
					var results = await c.ListBlobsSegmentedAsync();
					return from directory in results.OfType<CloudBlobDirectory>()
						   let expires = DateTime.Parse(directory.Uri.Segments[directory.Uri.Segments.Length - 1].TrimEnd('/'))
						   where expires < deleteBlobsExpiringBefore
						   select directory;
				},
				new ExecutionDataflowBlockOptions {
					BoundedCapacity = 4,
				});
			var deleteDirectoryBlock = new TransformManyBlock<CloudBlobDirectory, CloudBlockBlob>(
				async directory => {
					var results = await directory.ListBlobsSegmentedAsync();
					return results.OfType<CloudBlockBlob>();
				},
				new ExecutionDataflowBlockOptions {
					MaxDegreeOfParallelism = 2,
					BoundedCapacity = 4,
				});
			var deleteBlobBlock = new ActionBlock<CloudBlockBlob>(
				blob => blob.DeleteAsync(),
				new ExecutionDataflowBlockOptions {
					MaxDegreeOfParallelism = 4,
					BoundedCapacity = 100,
				});

			searchExpiredDirectoriesBlock.LinkTo(deleteDirectoryBlock, new DataflowLinkOptions { PropagateCompletion = true });
			deleteDirectoryBlock.LinkTo(deleteBlobBlock, new DataflowLinkOptions { PropagateCompletion = true });

			searchExpiredDirectoriesBlock.Post(this.container);
			searchExpiredDirectoriesBlock.Complete();
			await deleteBlobBlock.Completion;
		}
	}
}
