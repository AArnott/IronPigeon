// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Functions
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.IO.Pipelines;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using IronPigeon.Functions.Models;
    using IronPigeon.Providers;
    using IronPigeon.Relay;
    using MessagePack;
    using Microsoft;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Microsoft.Net.Http.Headers;
    using Nerdbank.Streams;

    public class Inbox
    {
        /// <summary>
        /// The maximum allowable size for a notification.
        /// </summary>
        public const int MaxNotificationSize = 10 * 1024;

        private const string BearerTokenPrefix = "Bearer ";

        private const string InboxContentType = "ironpigeon/inbox";

        private const string InboxItemContentType = "ironpigeon/inbox-item";

        /// <summary>
        /// The maximum lifetime an inbox will retain a posted message.
        /// </summary>
        private static readonly TimeSpan MaxLifetimeCeiling = TimeSpan.FromDays(14);

        private readonly AzureStorage azureStorage;

        public Inbox(AzureStorage azureStorage)
        {
            this.azureStorage = azureStorage;
        }

        /// <summary>
        /// Reactivates a <see cref="Mailbox"/> whose <see cref="Mailbox.Inactive"/> flag has been set and thus is inaccessible.
        /// </summary>
        [FunctionName("PUT-inbox-name")]
        public async Task<IActionResult> ReactivateInboxAsync(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "inbox/{name}")] HttpRequest req,
            string name,
            ILogger log)
        {
            (IActionResult? FailedResult, Mailbox? Mailbox) input = await this.CheckInboxAuthenticationAsync(name, req.Headers, allowInactive: true);
            if (input.FailedResult is object)
            {
                return input.FailedResult;
            }

            if (input.Mailbox.Inactive)
            {
                // Reactivate mailbox.
                input.Mailbox.Inactive = false;
                await this.azureStorage.InboxTable.ExecuteAsync(TableOperation.Merge(input.Mailbox));

                log.LogInformation($"Reactivated inbox: {name}");
            }

            return new OkResult();
        }

        /// <summary>
        /// Permanently deletes a mailbox and all its contents.
        /// </summary>
        [FunctionName("DELETE-inbox-name")]
        public async Task<IActionResult> DeleteInboxAsync(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "inbox/{name}")] HttpRequest req,
            string name,
            ILogger log,
            CancellationToken cancellationToken)
        {
            (IActionResult? FailedResult, Mailbox? Mailbox) input = await this.CheckInboxAuthenticationAsync(name, req.Headers, allowInactive: true, cancellationToken);
            if (input.FailedResult is object)
            {
                return input.FailedResult;
            }

            // Mark the mailbox as clear for deletion immediately.
            // We'll actually purge its contents and remove the mailbox entity itself later in a CRON job.
            // This ensures we respond quickly to the client and that the work completes even if it takes multiple tries.
            input.Mailbox.Deleted = true;
            await this.azureStorage.InboxTable.ExecuteAsync(TableOperation.Merge(input.Mailbox), cancellationToken);

            log.LogInformation($"Deleted mailbox: {input.Mailbox.Name}");
            return new OkResult();
        }

        /// <summary>
        /// Adds an item to an existing mailbox.
        /// </summary>
        [FunctionName("POST-inbox-name")]
        public async Task<IActionResult> PostInboxAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "inbox/{name}")] HttpRequest req,
            string name,
            ILogger log,
            CancellationToken cancellationToken)
        {
            if (!(req.GetTypedHeaders().ContentLength is long contentLength))
            {
                return new StatusCodeResult(StatusCodes.Status411LengthRequired);
            }

            Mailbox? mailbox = await this.azureStorage.LookupMailboxAsync(name, req.HttpContext.RequestAborted);
            if (mailbox is null || mailbox.Deleted)
            {
                return new NotFoundResult();
            }

            if (mailbox.Inactive)
            {
                return new StatusCodeResult(StatusCodes.Status410Gone);
            }

            if (contentLength > MaxNotificationSize)
            {
                return new BadRequestErrorMessageResult("Notification too large.");
            }

            if (req.Query["lifetime"].Count != 1 || !int.TryParse(req.Query["lifetime"][0], out int lifetimeInMinutes))
            {
                return new BadRequestErrorMessageResult("\"lifetime\" query parameter must specify the lifetime of this notification in minutes.");
            }

            TimeSpan lifetime = TimeSpan.FromMinutes(lifetimeInMinutes);
            if (lifetime > MaxLifetimeCeiling)
            {
                return new BadRequestErrorMessageResult($"\"lifetime\" query parameter cannot exceed {MaxLifetimeCeiling.TotalMinutes}.");
            }

            DateTime expiresUtc = DateTime.UtcNow + lifetime;

            var blobStorage = new AzureBlobStorage(this.azureStorage.InboxItemContainer, mailbox.Name);
            bool retriedOnceAlready = false;
retry:
            try
            {
                await blobStorage.UploadMessageAsync(req.Body, expiresUtc, cancellationToken: req.HttpContext.RequestAborted);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                // They caught us uninitialized. Ask them to try again after we mitigate the problem.
                log.LogInformation("JIT creating inbox item container.");
                if (retriedOnceAlready)
                {
                    return new StatusCodeResult(503); // Service Unavailable.
                }

                await blobStorage.CreateContainerIfNotExistAsync(cancellationToken);
                retriedOnceAlready = true;
                goto retry;
            }

            log.LogInformation($"Saved notification ({contentLength} bytes) to mailbox: {mailbox.Name}");
            return new OkResult();
        }

        /// <summary>
        /// Retrieves all the items in an existing mailbox.
        /// </summary>
        [FunctionName("GET-inbox-name")]
        public async Task<IActionResult> GetInboxAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "inbox/{name}")] HttpRequest req,
            string name)
        {
            (IActionResult? FailedResult, Mailbox? Mailbox) input = await this.CheckInboxAuthenticationAsync(name, req.Headers);
            if (input.FailedResult is object)
            {
                return input.FailedResult;
            }

            // Arrange to write to the live stream as the client downloads it.
            // If the client disconnects, we abort the work and save ourselves trouble.
            HttpResponse response = req.HttpContext.Response;
            response.StatusCode = StatusCodes.Status200OK;
            response.ContentType = InboxContentType;
            using (response.Body)
            {
                PipeWriter responseWriter = response.Body.UsePipeWriter(cancellationToken: req.HttpContext.RequestAborted);

                void WriteInboxItemHeader(BlobItem blob, BlobDownloadInfo blobContent)
                {
                    Verify.Operation(blob.Properties.CreatedOn.HasValue, "Cannot determine blob's creation date.");

                    var writer = new MessagePackWriter(responseWriter);
                    writer.WriteMapHeader(3);
                    writer.Write(nameof(IncomingInboxItem.Identity));
                    writer.Write(blob.Name.Substring(input.Mailbox.Name.Length + 1));

                    writer.Write(nameof(IncomingInboxItem.DatePostedUtc));
                    writer.Write(blob.Properties.CreatedOn.Value.UtcDateTime);

                    writer.Write(nameof(IncomingInboxItem.Envelope));
                    writer.WriteBinHeader(checked((int)blobContent.ContentLength));

                    writer.Flush();
                }

                await foreach (BlobHierarchyItem blob in this.azureStorage.InboxItemContainer.GetBlobsByHierarchyAsync(prefix: input.Mailbox.Name + "/", cancellationToken: req.HttpContext.RequestAborted))
                {
                    if (blob.IsBlob)
                    {
                        BlobClient blobClient = this.azureStorage.InboxItemContainer.GetBlobClient(blob.Blob.Name);
                        Azure.Response<BlobDownloadInfo> downloadInfo = await blobClient.DownloadAsync(req.HttpContext.RequestAborted);
                        WriteInboxItemHeader(blob.Blob, downloadInfo.Value);
                        await downloadInfo.Value.Content.CopyToAsync(responseWriter, req.HttpContext.RequestAborted);
                        downloadInfo.Value.Dispose();

                        // Do not outrun the client that is reading us.
                        await responseWriter.FlushAsync(req.HttpContext.RequestAborted);
                    }
                }
            }

            // Don't try to transmit anything more.
            return new EmptyResult();
        }

        /// <summary>
        /// Retrieves an individual mailbox item.
        /// </summary>
        [FunctionName("GET-inbox-name-number")]
        public async Task<IActionResult> GetInboxItemAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "inbox/{name}/{*entry}")] HttpRequest req,
            string name,
            string entry)
        {
            (IActionResult? FailedResult, Mailbox? Mailbox) input = await this.CheckInboxAuthenticationAsync(name, req.Headers);
            if (input.FailedResult is object)
            {
                return input.FailedResult;
            }

            BlobClient blobClient = this.azureStorage.InboxItemContainer.GetBlobClient($"{input.Mailbox.Name}/{Uri.EscapeUriString(entry)}");
            Stream blobStream = await blobClient.OpenReadAsync(cancellationToken: req.HttpContext.RequestAborted);

            return new FileStreamResult(blobStream, InboxItemContentType);
        }

        /// <summary>
        /// Retrieves an individual mailbox item.
        /// </summary>
        [FunctionName("DELETE-inbox-name-number")]
        public async Task<IActionResult> DeleteInboxItemAsync(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "inbox/{name}/{*entry}")] HttpRequest req,
            string name,
            string entry)
        {
            (IActionResult? FailedResult, Mailbox? Mailbox) input = await this.CheckInboxAuthenticationAsync(name, req.Headers);
            if (input.FailedResult is object)
            {
                return input.FailedResult;
            }

            BlobClient blobClient = this.azureStorage.InboxItemContainer.GetBlobClient($"{input.Mailbox.Name}/{Uri.EscapeUriString(entry)}");
            try
            {
                await blobClient.DeleteAsync(cancellationToken: req.HttpContext.RequestAborted);
                return new OkResult();
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return new NotFoundResult();
            }
        }

        /// <summary>
        /// Looks up and authenticates against a mailbox.
        /// </summary>
        /// <param name="name">The name of the mailbox.</param>
        /// <param name="headers">The HTTP request headers, from which to obtain the <see cref="Mailbox.OwnerCode"/>.</param>
        /// <param name="allowInactive"><c>true</c> to allow a match against an inactive mailbox; <c>false</c> to return a 410 Gone response for inactive mailboxes..</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>
        /// A failure result to return, or the mailbox that was found and successfully authenticated against.
        /// If the mailbox is inactive, both a failure result and the mailbox are provided.
        /// If the mailbox is tagged for deletion, a 404 is returned and no mailbox entity.
        /// </returns>
        private async Task<(IActionResult? FailedResult, Mailbox? Mailbox)> CheckInboxAuthenticationAsync(string name, IHeaderDictionary headers, bool allowInactive = false, CancellationToken cancellationToken = default)
        {
            Microsoft.Extensions.Primitives.StringValues authorization = headers["Authorization"];
            if (authorization.Count != 1)
            {
                return (new UnauthorizedResult(), null);
            }

            if (!authorization[0].StartsWith(BearerTokenPrefix, StringComparison.Ordinal))
            {
                return (new UnauthorizedResult(), null);
            }

            string ownerCode = authorization[0].Substring(BearerTokenPrefix.Length);
            Mailbox? mailbox = await this.azureStorage.LookupMailboxAsync(name, cancellationToken);
            if (mailbox is null)
            {
                return (new NotFoundResult(), null);
            }

            if (mailbox.OwnerCode != ownerCode)
            {
                return (new UnauthorizedResult(), null);
            }

            if (mailbox.Inactive && !allowInactive)
            {
                return (new StatusCodeResult(StatusCodes.Status410Gone), mailbox);
            }

            // Never return mailboxes marked for deletion.
            if (mailbox.Deleted)
            {
                return (new NotFoundResult(), null);
            }

            // Update last authenticated access timestamp.
            mailbox.LastAuthenticatedInteractionUtc = DateTime.UtcNow;
            await this.azureStorage.InboxTable.ExecuteAsync(TableOperation.Merge(mailbox), null, null, cancellationToken);

            return (null, mailbox);
        }
    }
}
