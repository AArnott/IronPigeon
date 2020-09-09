// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay.Controllers
{
    using System;
    using System.IO;
    using System.IO.Pipelines;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using IronPigeon.Providers;
    using IronPigeon.Relay;
    using IronPigeon.Relay.Models;
    using MessagePack;
    using Microsoft;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Extensions.Logging;

    [ApiController]
    [Route("[controller]")]
    [DenyHttp]
    public class InboxController : ControllerBase
    {
        /// <summary>
        /// The maximum allowable size for a notification.
        /// </summary>
        public const int MaxNotificationSize = 10 * 1024;

        /// <summary>
        /// The length in bytes of a cryptographically strong random byte buffer whose base64 (web safe) encoding becomes the bearer token to access an inbox.
        /// </summary>
        private const int CodeLength = 16;

        private const string BearerTokenPrefix = "Bearer ";

        private const string InboxContentType = "ironpigeon/inbox";

        private const string InboxItemContentType = "ironpigeon/inbox-item";

        /// <summary>
        /// The maximum lifetime an inbox will retain a posted message.
        /// </summary>
        private static readonly TimeSpan MaxLifetimeCeiling = TimeSpan.FromDays(14);

        private readonly ILogger<InboxController> logger;

        private readonly AzureStorage azureStorage;

        public InboxController(AzureStorage azureStorage, ILogger<InboxController> logger)
        {
            this.logger = logger;
            this.azureStorage = azureStorage;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateInboxAsync(CancellationToken cancellationToken)
        {
            using RandomNumberGenerator? rng = RandomNumberGenerator.Create();
            var inboxOwnerCode = new byte[CodeLength];
            rng.GetBytes(inboxOwnerCode);

            string name = Guid.NewGuid().ToString();
            var mailbox = new Models.Mailbox(name, Utilities.ToBase64WebSafe(inboxOwnerCode));
            var operation = TableOperation.Insert(mailbox);
            TableResult result;
            bool retriedOnceAlready = false;
retry:
            try
            {
                result = await this.azureStorage.InboxTable.ExecuteAsync(operation, cancellationToken);
            }
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == 404)
            {
                // They caught us uninitialized. Ask them to try again after we mitigate the problem.
                this.logger.LogInformation("JIT creating inbox table.");
                if (retriedOnceAlready)
                {
                    return this.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }

                await this.azureStorage.InboxTable.CreateIfNotExistsAsync(cancellationToken);
                retriedOnceAlready = true;
                goto retry;
            }

            UriBuilder mailboxUri = this.CreateSelfReferencingUriBuilder();
            mailboxUri.Path = $"/inbox/{mailbox.Name}";

            this.logger.LogInformation("Created inbox: {0}", mailbox.Name);
            var response = new InboxCreationResponse(mailboxUri.Uri, mailbox.OwnerCode);
            return this.Created(mailboxUri.Uri, response);
        }

        [HttpDelete("{name:guid}")]
        public async Task<IActionResult> DeleteInboxAsync(string name, CancellationToken cancellationToken)
        {
            (IActionResult? FailedResult, Mailbox? Mailbox) input = await this.CheckInboxAuthenticationAsync(name, allowInactive: true);
            if (input.FailedResult is object)
            {
                return input.FailedResult;
            }

            // Mark the mailbox as clear for deletion immediately.
            // We'll actually purge its contents and remove the mailbox entity itself later in a CRON job.
            // This ensures we respond quickly to the client and that the work completes even if it takes multiple tries.
            input.Mailbox.Deleted = true;
            await this.azureStorage.InboxTable.ExecuteAsync(TableOperation.Merge(input.Mailbox), cancellationToken);

            this.logger.LogInformation($"Deleted mailbox: {input.Mailbox.Name}");
            return this.Ok();
        }

        [HttpDelete("{name:guid}/{*item}")]
        public async Task<IActionResult> DeleteInboxItemAsync(string name, string item, CancellationToken cancellationToken)
        {
            (IActionResult? FailedResult, Mailbox? Mailbox) input = await this.CheckInboxAuthenticationAsync(name);
            if (input.FailedResult is object)
            {
                return input.FailedResult;
            }

            BlobClient blobClient = this.azureStorage.InboxItemContainer.GetBlobClient($"{input.Mailbox.Name}/{item}");
            try
            {
                await blobClient.DeleteAsync(cancellationToken: cancellationToken);
                return this.Ok();
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return this.NotFound();
            }
        }

        [HttpPost("{name:guid}")]
        public async Task<IActionResult> PostInboxItemAsync(string name, [FromQuery(Name = "lifetime")] int lifetimeInMinutes, CancellationToken cancellationToken)
        {
            if (!(this.Request.ContentLength is long contentLength))
            {
                return this.StatusCode(StatusCodes.Status411LengthRequired);
            }

            if (lifetimeInMinutes == 0)
            {
                return this.BadRequest("lifetime query parameter required with a positive integer value.");
            }

            Mailbox? mailbox = await this.azureStorage.LookupMailboxAsync(name, cancellationToken);
            if (mailbox is null || mailbox.Deleted)
            {
                return this.NotFound();
            }

            if (mailbox.Inactive)
            {
                return this.StatusCode(StatusCodes.Status410Gone);
            }

            if (contentLength > MaxNotificationSize)
            {
                return this.BadRequest("Notification too large.");
            }

            TimeSpan lifetime = TimeSpan.FromMinutes(lifetimeInMinutes);
            if (lifetime > MaxLifetimeCeiling)
            {
                return this.BadRequest($"\"lifetime\" query parameter cannot exceed {MaxLifetimeCeiling.TotalMinutes}.");
            }

            DateTime expiresUtc = DateTime.UtcNow + lifetime;

            var blobStorage = new AzureBlobStorage(this.azureStorage.InboxItemContainer, mailbox.Name);
            bool retriedOnceAlready = false;
retry:
            try
            {
                await blobStorage.UploadMessageAsync(this.Request.Body, expiresUtc, cancellationToken: cancellationToken);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                // They caught us uninitialized. Ask them to try again after we mitigate the problem.
                this.logger.LogInformation("JIT creating inbox item container.");
                if (retriedOnceAlready)
                {
                    return this.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }

                await blobStorage.CreateContainerIfNotExistAsync(cancellationToken);
                retriedOnceAlready = true;
                goto retry;
            }

            this.logger.LogInformation($"Saved notification ({contentLength} bytes) to mailbox: {mailbox.Name}");
            return this.Ok();
        }

        [HttpGet("{name:guid}")]
        public async Task<IActionResult> GetInboxContentAsync(string name, CancellationToken cancellationToken)
        {
            (IActionResult? FailedResult, Mailbox? Mailbox) input = await this.CheckInboxAuthenticationAsync(name);
            if (input.FailedResult is object)
            {
                return input.FailedResult;
            }

            // Arrange to write to the live stream as the client downloads it.
            // If the client disconnects, we abort the work and save ourselves trouble.
            this.Response.StatusCode = StatusCodes.Status200OK;
            this.Response.ContentType = InboxContentType;
            PipeWriter responseWriter = PipeWriter.Create(this.Response.Body);
            try
            {
                void WriteInboxItemHeader(BlobItem blob, BlobDownloadInfo blobContent, Uri identity)
                {
                    Verify.Operation(blob.Properties.CreatedOn.HasValue, "Cannot determine blob's creation date.");

                    var writer = new MessagePackWriter(responseWriter);
                    writer.WriteMapHeader(3);
                    writer.Write(nameof(IncomingInboxItem.Identity));
                    writer.Write(identity.AbsoluteUri);

                    writer.Write(nameof(IncomingInboxItem.DatePostedUtc));
                    writer.Write(blob.Properties.CreatedOn.Value.UtcDateTime);

                    writer.Write(nameof(IncomingInboxItem.Envelope));
                    writer.WriteBinHeader(checked((int)blobContent.ContentLength));

                    writer.Flush();
                }

                UriBuilder identityBuilder = this.CreateSelfReferencingUriBuilder();

                await foreach (BlobHierarchyItem blob in this.azureStorage.InboxItemContainer.GetBlobsByHierarchyAsync(prefix: input.Mailbox.Name + "/", cancellationToken: cancellationToken))
                {
                    if (blob.IsBlob)
                    {
                        BlobClient blobClient = this.azureStorage.InboxItemContainer.GetBlobClient(blob.Blob.Name);
                        Azure.Response<BlobDownloadInfo> downloadInfo = await blobClient.DownloadAsync(cancellationToken);
                        identityBuilder.Path = $"{this.Url.Action()}/{blob.Blob.Name.Substring(name.Length + 1)}";
                        WriteInboxItemHeader(blob.Blob, downloadInfo.Value, identityBuilder.Uri);
                        await downloadInfo.Value.Content.CopyToAsync(responseWriter, cancellationToken);
                        downloadInfo.Value.Dispose();

                        // Do not outrun the client that is reading us.
                        await responseWriter.FlushAsync(cancellationToken);
                    }
                }

                await responseWriter.CompleteAsync();
            }
            catch (Exception ex)
            {
                await responseWriter.CompleteAsync(ex);
                throw;
            }

            // Don't try to transmit anything more.
            return new EmptyResult();
        }

        [HttpGet("{name:guid}/{*item}")]
        public async Task<IActionResult> GetInboxItemAsync(string name, string item, CancellationToken cancellationToken)
        {
            (IActionResult? FailedResult, Mailbox? Mailbox) input = await this.CheckInboxAuthenticationAsync(name);
            if (input.FailedResult is object)
            {
                return input.FailedResult;
            }

            BlobClient blobClient = this.azureStorage.InboxItemContainer.GetBlobClient($"{input.Mailbox.Name}/{item}");
            Stream blobStream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);

            return this.File(blobStream, InboxItemContentType);
        }

        private UriBuilder CreateSelfReferencingUriBuilder()
        {
            return new UriBuilder
            {
                Scheme = this.Request.Scheme,
                Host = this.Request.Host.Host,
                Port = this.Request.Host.Port ?? (this.Request.Scheme == "https" ? 443 : 80),
                Path = this.Url.Action(),
            };
        }

        /// <summary>
        /// Looks up and authenticates against a mailbox.
        /// </summary>
        /// <param name="name">The name of the mailbox.</param>
        /// <param name="allowInactive"><c>true</c> to allow a match against an inactive mailbox; <c>false</c> to return a 410 Gone response for inactive mailboxes..</param>
        /// <returns>
        /// A failure result to return, or the mailbox that was found and successfully authenticated against.
        /// If the mailbox is inactive, both a failure result and the mailbox are provided.
        /// If the mailbox is tagged for deletion, a 404 is returned and no mailbox entity.
        /// </returns>
        private async Task<(IActionResult? FailedResult, Mailbox? Mailbox)> CheckInboxAuthenticationAsync(string name, bool allowInactive = false)
        {
            CancellationToken cancellationToken = this.Request.HttpContext.RequestAborted;
            Microsoft.Extensions.Primitives.StringValues authorization = this.Request.Headers["Authorization"];
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
