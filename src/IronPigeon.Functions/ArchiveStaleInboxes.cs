// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Functions
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Storage.Blobs.Models;
    using IronPigeon.Functions.Models;
    using IronPigeon.Providers;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging;

    public class ArchiveStaleInboxes
    {
        /// <summary>
        /// The CRON schedule for running this function.
        /// </summary>
        private const string StaleInboxSchedule = "15 14 * * 2"; // At 14:15 on Tuesday

        private const string DeletedInboxSchedule = "45 0 * * *"; // At 00:45 daily.

        private const string ExpiredInboxItemsSchedule = "30 8 * * *"; // At 8:30 daily.

        private static readonly TimeSpan InboxesStaleAfter = TimeSpan.FromDays(365);

        private readonly AzureStorage azureStorage;

        public ArchiveStaleInboxes(AzureStorage azureStorage)
        {
            this.azureStorage = azureStorage;
        }

        [FunctionName("ArchiveStaleInboxes")]
        public async Task ArchiveStaleInboxesAsync([TimerTrigger(StaleInboxSchedule)] TimerInfo myTimer, ILogger log, CancellationToken cancellationToken)
        {
            log.LogInformation("Archiving stale inboxes...");
            var timer = Stopwatch.StartNew();

            DateTimeOffset inboxStaleIfNotUsedSince = DateTimeOffset.UtcNow - InboxesStaleAfter;
            TableQuery<Mailbox> query = new TableQuery<Mailbox>()
                .Where(TableQuery.CombineFilters(
                    TableQuery.GenerateFilterConditionForDate(nameof(Mailbox.LastAuthenticatedInteractionUtc), QueryComparisons.LessThan, inboxStaleIfNotUsedSince),
                    TableOperators.And,
                    TableQuery.GenerateFilterConditionForBool(nameof(Mailbox.Inactive), QueryComparisons.NotEqual, true)));

            long purgedInboxes = 0;
            TableContinuationToken? continuationToken = null;
            do
            {
                TableQuerySegment<Mailbox> result = await this.azureStorage.InboxTable.ExecuteQuerySegmentedAsync(query, continuationToken);
                TableBatchOperation edits = new TableBatchOperation();
                foreach (Mailbox inbox in result.Results)
                {
                    inbox.Inactive = true;
                    edits.Add(TableOperation.Merge(inbox));

                    // Delete all inbox items.
                    var blobProvider = new AzureBlobStorage(this.azureStorage.InboxItemContainer, inbox.Name);
                    await blobProvider.PurgeBlobsExpiringBeforeAsync(DateTime.MaxValue, cancellationToken);
                }

                log.LogInformation($"{result.Results.Count} inboxes identified as stale.");
                purgedInboxes += edits.Count;

                if (edits.Count > 0)
                {
                    await this.azureStorage.InboxTable.ExecuteBatchAsync(edits, null, null, cancellationToken);
                }

                continuationToken = result.ContinuationToken;
            }
            while (continuationToken is object);

            log.LogInformation($"Finished identifying stale inboxes in {timer.Elapsed}. Total new stale inboxes found: {purgedInboxes}. Next run will be at: {myTimer.ScheduleStatus.Next}");
        }

        [FunctionName("DeleteInboxes")]
        public async Task DeleteInboxesAsync([TimerTrigger(DeletedInboxSchedule)] TimerInfo myTimer, ILogger log, CancellationToken cancellationToken)
        {
            log.LogInformation("Purging mailboxes marked for deletion...");
            var timer = Stopwatch.StartNew();

            TableQuery<Mailbox> query = new TableQuery<Mailbox>()
                .Where(TableQuery.GenerateFilterConditionForBool(nameof(Mailbox.Deleted), QueryComparisons.Equal, true));

            long purgedInboxes = 0;
            TableContinuationToken? continuationToken = null;
            do
            {
                TableQuerySegment<Mailbox> result = await this.azureStorage.InboxTable.ExecuteQuerySegmentedAsync(query, continuationToken, null, null, cancellationToken);
                foreach (Mailbox inbox in result.Results)
                {
                    // Delete all inbox items.
                    var blobProvider = new AzureBlobStorage(this.azureStorage.InboxItemContainer, inbox.Name);
                    await blobProvider.PurgeBlobsExpiringBeforeAsync(DateTime.MaxValue, cancellationToken);

                    // Delete the inbox itself.
                    await this.azureStorage.InboxTable.ExecuteAsync(TableOperation.Delete(inbox), null, null, cancellationToken);

                    purgedInboxes++;
                    log.LogInformation($"Permanently deleted inbox and its contents: {inbox.Name}");
                }

                continuationToken = result.ContinuationToken;
            }
            while (continuationToken is object);

            log.LogInformation($"Finished identifying stale inboxes in {timer.Elapsed}. Total new stale inboxes found: {purgedInboxes}. Next run will be at: {myTimer.ScheduleStatus.Next}");
        }

        [FunctionName("PurgeExpiredInboxItems")]
        public async Task PurgeExpiredInboxItemsAsync([TimerTrigger(ExpiredInboxItemsSchedule)] TimerInfo myTimer, ILogger log, CancellationToken cancellationToken)
        {
            DateTime purgeItemsExpiringBefore = DateTime.UtcNow;
            log.LogInformation($"Purging inbox items that expired at {purgeItemsExpiringBefore}...");
            var timer = Stopwatch.StartNew();

            long mailboxesReviewed = 0;
            await foreach (BlobHierarchyItem mailboxDirectory in this.azureStorage.InboxItemContainer.GetBlobsByHierarchyAsync(delimiter: "/", cancellationToken: cancellationToken))
            {
                if (mailboxDirectory.IsPrefix)
                {
                    mailboxesReviewed++;
                    var azureStorage = new AzureBlobStorage(this.azureStorage.InboxItemContainer, mailboxDirectory.Prefix);
                    await azureStorage.PurgeBlobsExpiringBeforeAsync(purgeItemsExpiringBefore, cancellationToken);
                }
            }

            log.LogInformation($"Reviewed {mailboxesReviewed} mailboxes in {timer.Elapsed}. Next run will be at: {myTimer.ScheduleStatus.Next}");
        }
    }
}
