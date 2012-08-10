namespace IronPigeon.Relay.Controllers {
	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;
	using System.Web;
	using System.Web.Mvc;
	using Microsoft;
	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.StorageClient;

	public class InboxController : Controller {
		private static readonly char[] DisallowedThumbprintCharacters = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

		/// <summary>
		/// The default name for the container used to store posted messages.
		/// </summary>
		private const string DefaultInboxContainerName = "inbox";

		/// <summary>
		/// The key to the Azure account configuration information.
		/// </summary>
		private const string DefaultCloudConfigurationName = "StorageConnectionString";

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
				blobs.AddRange(
					from blob in (await directory.ListBlobsSegmentedAsync(50)).OfType<CloudBlob>()
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
		public async Task<ActionResult> PostNotification(string thumbprint) {
			VerifyValidThumbprint(thumbprint);
			await this.EnsureContainerInitializedAsync();

			var directory = this.InboxContainer.GetDirectoryReference(thumbprint);
			var blob = directory.GetBlobReference(Utilities.CreateRandomWebSafeName(24));
			await blob.UploadFromStreamAsync(this.Request.InputStream);
			return new EmptyResult();
		}

		private static void VerifyValidThumbprint(string thumbprint) {
			Requires.NotNullOrEmpty(thumbprint, "thumbprint");
			Requires.Argument(thumbprint.IndexOfAny(DisallowedThumbprintCharacters) < 0, "thumbprint", "Disallowed characters.");
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

		private async Task EnsureContainerInitializedAsync() {
			await this.InboxContainer.CreateIfNotExistAsync();

			var permissions = new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob };
			await this.InboxContainer.SetPermissionsAsync(permissions);
		}
	}
}
