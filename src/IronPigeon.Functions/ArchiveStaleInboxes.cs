// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Functions
{
    using System;
    using System.Threading.Tasks;
    using IronPigeon.Functions.Models;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Storage.Table;

    public static class ArchiveStaleInboxes
    {
        /// <summary>
        /// The CRON schedule for running this function.
        /// </summary>
        private const string CronSchedule = "15 14 * * 2"; // At 14:15 on Tuesday

        private static readonly TimeSpan InboxesStaleAfter = TimeSpan.FromDays(365);

        [FunctionName("ArchiveStaleInboxes")]
        public static async Task RunAsync([TimerTrigger(CronSchedule)] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"Archiving stale inboxes...");

            DateTimeOffset inboxStaleIfNotUsedSince = DateTimeOffset.UtcNow - InboxesStaleAfter;
            TableQuery<Mailbox> query = new TableQuery<Mailbox>()
                .Where(TableQuery.CombineFilters(
                    TableQuery.GenerateFilterConditionForDate(nameof(Mailbox.LastAccessedUtc), QueryComparisons.LessThan, inboxStaleIfNotUsedSince),
                    TableOperators.And,
                    TableQuery.GenerateFilterConditionForBool(nameof(Mailbox.Inactive), QueryComparisons.NotEqual, true)));

            long purgedInboxes = 0;
            TableContinuationToken? continuationToken = null;
            do
            {
                TableQuerySegment<Mailbox> result = await AzureStorage.InboxTable.ExecuteQuerySegmentedAsync(query, continuationToken);
                TableBatchOperation edits = new TableBatchOperation();
                foreach (Mailbox inbox in result.Results)
                {
                    inbox.Inactive = true;
                    edits.Add(TableOperation.Merge(inbox));

                    // Also purge all contents of the inbox.
                    // TODO: code here.
                }

                log.LogInformation($"{result.Results.Count} inboxes identified as stale.");
                purgedInboxes += edits.Count;

                if (edits.Count > 0)
                {
                    await AzureStorage.InboxTable.ExecuteBatchAsync(edits);
                }

                continuationToken = result.ContinuationToken;
            }
            while (continuationToken is object);

            log.LogInformation($"Finished identifying stale inboxes. Total new stale inboxes found: {purgedInboxes}. Next run will be at: {myTimer.ScheduleStatus.Next}");
        }
    }
}
