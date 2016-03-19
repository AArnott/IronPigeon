// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.Mvc;
    using IronPigeon.Providers;
    using Microsoft.WindowsAzure;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.StorageClient;
    using Validation;

#if !DEBUG
	[RequireHttps]
#endif
    public class BlobController : ApiController
    {
        /// <summary>
        /// The default name of the Azure blob container to use for blobs.
        /// </summary>
        internal const string DefaultContainerName = "blobs";

        private static readonly SortedDictionary<int, TimeSpan> MaxBlobSizesAndLifetimes = new SortedDictionary<int, TimeSpan>
        {
            { 10 * 1024, TimeSpan.MaxValue }, // this is intended for address book entries.
            { 512 * 1024, TimeSpan.FromDays(7) },
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="BlobController" /> class.
        /// </summary>
        public BlobController()
            : this(AzureStorageConfig.DefaultCloudConfigurationName)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BlobController" /> class.
        /// </summary>
        /// <param name="cloudConfigurationName">Name of the cloud configuration.</param>
        /// <param name="containerName">The name of the Azure blob container to upload to.</param>
        public BlobController(string cloudConfigurationName, string containerName = DefaultContainerName)
        {
            Requires.NotNullOrEmpty(cloudConfigurationName, "cloudConfigurationName");

            var storage = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings[cloudConfigurationName].ConnectionString);
            this.CloudBlobStorageProvider = new AzureBlobStorage(storage, containerName);

            ////var p = new AzureBlobStorage(storage, containerName);
            ////Task.Run(async delegate { await p.CreateContainerIfNotExistAsync(); });
        }

        /// <summary>
        /// Gets or sets the cloud blob storage provider.
        /// </summary>
        public ICloudBlobStorageProvider CloudBlobStorageProvider { get; set; }

        // POST api/blob
        public async Task<HttpResponseMessage> Post([FromUri]int lifetimeInMinutes)
        {
            Requires.Range(lifetimeInMinutes > 0, "lifetimeInMinutes");

            var lifetime = TimeSpan.FromMinutes(lifetimeInMinutes);
            DateTime expirationUtc = DateTime.UtcNow + lifetime;
            string contentType = this.Request.Content.Headers.ContentType != null
                                     ? this.Request.Content.Headers.ContentType.ToString()
                                     : null;
            string contentEncoding = this.Request.Content.Headers.ContentEncoding.FirstOrDefault();
            var content = await this.Request.Content.ReadAsStreamAsync();
            var errorResponse = GetDisallowedLifetimeResponse(content.Length, lifetime);
            if (errorResponse != null)
            {
                return errorResponse;
            }

            var blobLocation = await this.CloudBlobStorageProvider.UploadMessageAsync(content, expirationUtc, contentType, contentEncoding);

            Uri resultLocation = contentType == AddressBookEntry.ContentType
                ? new Uri(this.Url.Link("Default", new { controller = "AddressBook", blob = blobLocation.AbsoluteUri }))
                : blobLocation;

            return this.ControllerContext.Request.CreateResponse(
                HttpStatusCode.Created,
                resultLocation.AbsoluteUri);
        }

        internal static async Task OneTimeInitializeAsync(CloudStorageAccount azureAccount)
        {
            var blobStorage = new AzureBlobStorage(azureAccount, BlobController.DefaultContainerName);
            await blobStorage.CreateContainerIfNotExistAsync();

            var nowait = Task.Run(async delegate
            {
                while (true)
                {
                    await blobStorage.PurgeBlobsExpiringBeforeAsync();
                    await Task.Delay(AzureStorageConfig.PurgeExpiredBlobsInterval);
                }
            });
        }

        private static HttpResponseMessage GetDisallowedLifetimeResponse(long blobSize, TimeSpan lifetime)
        {
            foreach (var rule in MaxBlobSizesAndLifetimes)
            {
                if (blobSize < rule.Key)
                {
                    if (lifetime > rule.Value)
                    {
                        return new HttpResponseMessage(HttpStatusCode.PaymentRequired);
                    }

                    return null;
                }
            }

            return new HttpResponseMessage(HttpStatusCode.RequestEntityTooLarge);
        }
    }
}