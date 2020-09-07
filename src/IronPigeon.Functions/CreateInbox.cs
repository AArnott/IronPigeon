// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Functions
{
    using System;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using IronPigeon.Relay;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;

    public class CreateInbox
    {
        /// <summary>
        /// The length in bytes of a cryptographically strong random byte buffer whose base64 (web safe) encoding becomes the bearer token to access an inbox.
        /// </summary>
        private const int CodeLength = 16;

        private readonly AzureStorage azureStorage;

        public CreateInbox(AzureStorage azureStorage)
        {
            this.azureStorage = azureStorage;
        }

        [FunctionName("POST-inbox")]
        public async Task<IActionResult> CreateInboxAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "inbox")] HttpRequest req,
            ILogger log)
        {
            using RandomNumberGenerator? rng = RandomNumberGenerator.Create();
            var inboxOwnerCode = new byte[CodeLength];
            rng.GetBytes(inboxOwnerCode);

            var mailbox = new Models.Mailbox(Guid.NewGuid().ToString(), Utilities.ToBase64WebSafe(inboxOwnerCode));
            var operation = TableOperation.Insert(mailbox);
            TableResult result;
            bool retriedOnceAlready = false;
retry:
            try
            {
                result = await this.azureStorage.InboxTable.ExecuteAsync(operation);
            }
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == 404)
            {
                // They caught us uninitialized. Ask them to try again after we mitigate the problem.
                log.LogInformation("JIT creating inbox table.");
                if (retriedOnceAlready)
                {
                    return new StatusCodeResult(503); // Service Unavailable.
                }

                await this.azureStorage.InboxTable.CreateIfNotExistsAsync();
                retriedOnceAlready = true;
                goto retry;
            }

            UriBuilder mailboxUri = new UriBuilder
            {
                Scheme = req.Scheme,
                Host = req.Host.Host,
                Port = req.Host.Port ?? (req.Scheme == "https" ? 443 : 80),
                Path = $"/api/inbox/{mailbox.Name}",
            };

            log.LogInformation("Created inbox: {0}", mailbox.Name);
            var response = new InboxCreationResponse(mailboxUri.Uri, mailbox.OwnerCode);
            return new OkObjectResult(response);
        }
    }
}
