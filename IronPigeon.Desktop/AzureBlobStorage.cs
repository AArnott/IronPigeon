namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
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

			this.account = account;
			this.client = this.account.CreateCloudBlobClient();
			this.container = this.client.GetContainerReference(containerAddress);
		}

		public static async Task<AzureBlobStorage> CreateWithContainerAsync(CloudStorageAccount account, string containerAddress) {
			Requires.NotNull(account, "account");
			Requires.NotNullOrEmpty(containerAddress, "containerAddress");

			var storage = new AzureBlobStorage(account, containerAddress);
			await storage.CreatePublicBlobContainerAsync();
			return storage;
		}

		#region ICloudBlobStorageProvider Members

		public async Task<Uri> UploadMessageAsync(Stream content, DateTime expirationUtc, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(content, "content");

			var blob = this.container.GetBlobReference(CreateRandomBlobName());
			await blob.UploadFromStreamAsync(content);
			blob.Metadata["DeleteAfter"] = expirationUtc.ToString();
			await blob.SetMetadataAsync();
			return blob.Uri;
		}

		#endregion

		private async Task CreatePublicBlobContainerAsync() {
			Requires.NotNull(container, "container");

			if (await this.container.CreateIfNotExistAsync()) {
				var permissions = new BlobContainerPermissions {
					PublicAccess = BlobContainerPublicAccessType.Blob,
				};
				await this.container.SetPermissionsAsync(permissions, null);
			}
		}

		private static string CreateRandomBlobName() {
			var random = new Random();
			var buffer = new byte[16];
			random.NextBytes(buffer);
			return Utilities.ToBase64WebSafe(buffer);
		}
	}
}
