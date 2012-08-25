namespace IronPigeon.Relay.Controllers {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Threading.Tasks;
	using System.Web.Http;

	using IronPigeon.Providers;

	using Microsoft;
	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.StorageClient;

	public class BlobController : ApiController {
		/// <summary>
		/// The default name of the Azure blob container to use for blobs.
		/// </summary>
		private const string DefaultContainerName = "blobs";

		/// <summary>
		/// Initializes a new instance of the <see cref="BlobController" /> class.
		/// </summary>
		public BlobController()
			: this(InboxController.DefaultCloudConfigurationName) {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="BlobController" /> class.
		/// </summary>
		/// <param name="cloudConfigurationName">Name of the cloud configuration.</param>
		/// <parparam name="containerName">The name of the Azure blob container to upload to.</parparam>
		public BlobController(string cloudConfigurationName, string containerName = DefaultContainerName) {
			Requires.NotNullOrEmpty(cloudConfigurationName, "cloudConfigurationName");

			var storage = CloudStorageAccount.FromConfigurationSetting(cloudConfigurationName);
			this.CloudBlobStorageProvider = new AzureBlobStorage(storage, containerName);

			var client = storage.CreateCloudBlobClient();
			var container = client.GetContainerReference(containerName);
			container.Delete();
			var p = new AzureBlobStorage(storage, containerName);
			Task.Run(async delegate { await p.CreateContainerIfNotExistAsync(); });
		}

		/// <summary>
		/// Gets or sets the cloud blob storage provider.
		/// </summary>
		public ICloudBlobStorageProvider CloudBlobStorageProvider { get; set; }

		// POST api/blob
		public async Task<Uri> Post([FromUri]int lifetimeInMinutes) {
			Requires.Range(lifetimeInMinutes > 0, "lifetimeInMinutes");

			DateTime expirationUtc = DateTime.UtcNow + TimeSpan.FromMinutes(lifetimeInMinutes);
			string contentType = this.Request.Content.Headers.ContentType.ToString();
			string contentEncoding = this.Request.Content.Headers.ContentEncoding.FirstOrDefault();
			var content = await this.Request.Content.ReadAsStreamAsync();
			var location = await this.CloudBlobStorageProvider.UploadMessageAsync(content, expirationUtc, contentType, contentEncoding);
			return location;
		}
	}
}