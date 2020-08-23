// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using Microsoft;

    /// <summary>
    /// A cloud blob storage provider that uses Azure blob storage directly.
    /// </summary>
    public class AzureBlobStorage : ICloudBlobStorageProvider
    {
        private const string PathDelimiter = "/";

        /// <summary>
        /// The blob container.
        /// </summary>
        private readonly BlobContainerClient container;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureBlobStorage" /> class.
        /// </summary>
        /// <param name="container">The blob container client.</param>
        public AzureBlobStorage(BlobContainerClient container)
        {
            Requires.NotNull(container, nameof(container));

            this.container = container;
        }

        /// <inheritdoc/>
        public async Task<Uri> UploadMessageAsync(Stream content, DateTime expirationUtc, string? contentType, string? contentEncoding, IProgress<long>? bytesCopiedProgress, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(content, nameof(content));
            Requires.Range(expirationUtc > DateTime.UtcNow, "expirationUtc");

            string blobName = Utilities.CreateRandomWebSafeName(Utilities.BlobNameLength);
            if (expirationUtc < DateTime.MaxValue)
            {
                DateTime roundedUp = expirationUtc - expirationUtc.TimeOfDay + TimeSpan.FromDays(1);
                blobName = roundedUp.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture) + "/" + blobName;
            }

            BlobClient blobClient = this.container.GetBlobClient(blobName);

            // Set metadata with the precise expiration time, although for efficiency we also put the blob into a directory
            // for efficient deletion based on approximate expiration date.
            var metadata = new Dictionary<string, string>();
            if (expirationUtc < DateTime.MaxValue)
            {
                metadata["DeleteAfter"] = expirationUtc.ToString(CultureInfo.InvariantCulture);
            }

            var uploadOptions = new BlobUploadOptions
            {
                Metadata = metadata,
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType,
                    ContentEncoding = contentEncoding,
                },
                ProgressHandler = bytesCopiedProgress,
            };

            await blobClient.UploadAsync(content, uploadOptions, cancellationToken).ConfigureAwait(false);
            return blobClient.Uri;
        }

        /// <summary>
        /// Creates the blob container if it does not exist, and sets its public access permission to allow
        /// downloading of blobs by their URIs.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The task representing the asynchronous operation.</returns>
        public Task CreateContainerIfNotExistAsync(CancellationToken cancellationToken)
        {
            return this.container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Purges all blobs set to expire prior to the specified date.
        /// </summary>
        /// <param name="deleteBlobsExpiringBefore">
        /// All blobs scheduled to expire prior to this date will be purged.  The default value
        /// is interpreted as <see cref="DateTime.UtcNow"/>.
        /// </param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The task representing the asynchronous operation.</returns>
        public async Task PurgeBlobsExpiringBeforeAsync(DateTime? deleteBlobsExpiringBefore, CancellationToken cancellationToken = default)
        {
            if (!deleteBlobsExpiringBefore.HasValue)
            {
                deleteBlobsExpiringBefore = DateTime.UtcNow;
            }

            Requires.Argument(deleteBlobsExpiringBefore.Value.Kind == DateTimeKind.Utc, "expirationUtc", "UTC required.");

            var deleteBlobBlock = new ActionBlock<BlobItem>(
                blob => this.container.DeleteBlobAsync(blob.Name, cancellationToken: cancellationToken),
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 4,
                    BoundedCapacity = 100,
                    MaxMessagesPerTask = 50,
                    CancellationToken = cancellationToken,
                });

            var directoryToBlobs = new ActionBlock<BlobHierarchyItem>(
                async directory =>
                {
                    await foreach (BlobHierarchyItem? blob in this.container.GetBlobsByHierarchyAsync(prefix: directory.Prefix))
                    {
                        await deleteBlobBlock.SendAsync(blob.Blob, cancellationToken).ConfigureAwait(false);
                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 2,
                    BoundedCapacity = 4,
                    CancellationToken = cancellationToken,
                });

            await foreach (BlobHierarchyItem? hierarchyItem in this.container.GetBlobsByHierarchyAsync(delimiter: PathDelimiter, cancellationToken: cancellationToken))
            {
                if (hierarchyItem.IsPrefix)
                {
                    DateTime expires = DateTime.Parse(hierarchyItem.Prefix.TrimEnd(PathDelimiter[0]), CultureInfo.InvariantCulture);

                    // As soon as we see the first 'directory' with a greater date, stop enumerating.
                    if (expires >= deleteBlobsExpiringBefore)
                    {
                        break;
                    }

                    await directoryToBlobs.SendAsync(hierarchyItem, cancellationToken).ConfigureAwait(false);
                }
            }

            directoryToBlobs.Complete();
            await directoryToBlobs.Completion.ConfigureAwait(false);
            deleteBlobBlock.Complete();
            await deleteBlobBlock.Completion.ConfigureAwait(false);
        }
    }
}
