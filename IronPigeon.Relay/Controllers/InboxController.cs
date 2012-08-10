namespace IronPigeon.Relay.Controllers {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;
	using System.Web;
	using System.Web.Mvc;
	using Microsoft;
	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.StorageClient;

	public class InboxController : Controller {
		private const string InboxContainerName = "inbox";
		private static bool ContainerInitialized;
		private static readonly CloudStorageAccount storage = CloudStorageAccount.FromConfigurationSetting("StorageConnectionString");

		[HttpPost]
		public async Task<ActionResult> Create() {
			return new EmptyResult();
		}

		[HttpGet, ActionName("Index")]
		public async Task<ActionResult> GetInboxItemsAsync(string thumbprint) {
			Requires.NotNullOrEmpty(thumbprint, "thumbprint");
			EnsureContainerInitializedAsync();

			var blobClient = storage.CreateCloudBlobClient();
			var container = blobClient.GetContainerReference(InboxContainerName);
			var directory = container.GetDirectoryReference(thumbprint);
			var blobs = new List<IncomingList.IncomingItem>();
			try {
				blobs.AddRange(
					from blob in (await directory.ListBlobsSegmentedAsync(50)).OfType<CloudBlob>()
					select new IncomingList.IncomingItem { Location = blob.Uri });
			} catch (StorageClientException) {
			}

			var list = new IncomingList() { Items = blobs };
			return new JsonResult() {
				Data = list,
				JsonRequestBehavior = JsonRequestBehavior.AllowGet
			};
		}

		[HttpPost, ActionName("Index")]
		public async Task<ActionResult> PostNotification(string thumbprint) {
			VerifyValidThumbprint(thumbprint);
			EnsureContainerInitializedAsync();
			
			var blobClient = storage.CreateCloudBlobClient();
			var container = blobClient.GetContainerReference(InboxContainerName);
			var directory = container.GetDirectoryReference(thumbprint);
			var blob = directory.GetBlobReference(Utilities.CreateRandomWebSafeName(24));
			await blob.UploadFromStreamAsync(this.Request.InputStream);
			return new EmptyResult();
		}

		private static async Task EnsureContainerInitializedAsync() {
			if (!ContainerInitialized) {
				var blobClient = storage.CreateCloudBlobClient();
				var container = blobClient.GetContainerReference(InboxContainerName);
				await container.CreateIfNotExistAsync();
			}
		}

		private static void VerifyValidThumbprint(string thumbprint) {
			Requires.NotNullOrEmpty(thumbprint, "thumbprint");
			Requires.Argument(Regex.IsMatch(thumbprint, "^[a-f0-9]+$"), "thumbprint", "Disallowed characters.");
			Requires.Argument(IsValidBlobContainerName(thumbprint), "thumbprint", "Illegal blob container name.");
		}

		private static bool IsValidBlobContainerName(string containerName) {
			if (containerName == null) {
				return false;
			}

			// Rule #1: can only contain (lowercase) letters, numbers and dashes.
			if (!Regex.IsMatch(containerName, @"^[a-z0-9\-]+$")) {
				return false;
			}

			// Rule #2: all dashes must be preceded and followed by a letter or number.
			if (containerName.StartsWith("-") || containerName.EndsWith("-") || containerName.Contains("--")) {
				return false;
			}

			// Rule #3: all lowercase.
			if (containerName.ToLowerInvariant() != containerName) {
				return false;
			}

			// Rule #4: length is 3-63
			if (containerName.Length < 3 || containerName.Length > 63) {
				return false;
			}

			return true;
		}
	}
}
