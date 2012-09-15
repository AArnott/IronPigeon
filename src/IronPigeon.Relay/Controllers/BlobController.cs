namespace IronPigeon.Relay.Controllers {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Threading.Tasks;
	using System.Web.Http;
	using System.Web.Mvc;

	using IronPigeon.Providers;

	using Validation;
	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.StorageClient;
#if !NET40
	using TaskEx = System.Threading.Tasks.Task;
#endif

#if !DEBUG
	[RequireHttps]
#endif
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
		/// <param name="containerName">The name of the Azure blob container to upload to.</param>
		public BlobController(string cloudConfigurationName, string containerName = DefaultContainerName) {
			Requires.NotNullOrEmpty(cloudConfigurationName, "cloudConfigurationName");

			var storage = CloudStorageAccount.FromConfigurationSetting(cloudConfigurationName);
			this.CloudBlobStorageProvider = new AzureBlobStorage(storage, containerName);

			////var p = new AzureBlobStorage(storage, containerName);
			////TaskEx.Run(async delegate { await p.CreateContainerIfNotExistAsync(); });
		}

		/// <summary>
		/// Gets or sets the cloud blob storage provider.
		/// </summary>
		public ICloudBlobStorageProvider CloudBlobStorageProvider { get; set; }

		// POST api/blob
		public async Task<string> Post([FromUri]int lifetimeInMinutes) {
			Requires.Range(lifetimeInMinutes > 0, "lifetimeInMinutes");

			DateTime expirationUtc = DateTime.UtcNow + TimeSpan.FromMinutes(lifetimeInMinutes);
			string contentType = this.Request.Content.Headers.ContentType != null
									 ? this.Request.Content.Headers.ContentType.ToString()
									 : null;
			string contentEncoding = this.Request.Content.Headers.ContentEncoding.FirstOrDefault();
			var content = await this.Request.Content.ReadAsStreamAsync();
			var blobLocation = await this.CloudBlobStorageProvider.UploadMessageAsync(content, expirationUtc, contentType, contentEncoding);

			Uri resultLocation = contentType == AddressBookEntry.ContentType
				? new Uri(this.Url.Link("Default", new { controller = "AddressBook", blob = blobLocation.AbsoluteUri }))
				: blobLocation;

			return resultLocation.AbsoluteUri;
		}
	}
}