// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Functions
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Storage.Blobs;
    using IronPigeon.Functions.Models;
    using IronPigeon.Providers;
    using Microsoft.Azure.Cosmos.Table;

    internal static class AzureStorage
    {
        /// <summary>
        /// Gets the Azure Storage account.
        /// </summary>
        internal static string ConnectionString { get; } = Environment.GetEnvironmentVariable("AzureWebJobsStorage") ?? throw new InvalidOperationException("Missing configuration.");

        /// <summary>
        /// Gets the table storage account.
        /// </summary>
        internal static CloudStorageAccount TableCloudStorageAccount { get; } = CloudStorageAccount.Parse(ConnectionString);

        /// <summary>
        /// Gets an Azure Table client.
        /// </summary>
        internal static CloudTableClient TableClient { get; } = TableCloudStorageAccount.CreateCloudTableClient();

        /// <summary>
        /// Gets the inboxes table.
        /// </summary>
        internal static CloudTable InboxTable { get; } = TableClient.GetTableReference("Inboxes");

        /// <summary>
        /// Gets the blob container to use for inbox items.
        /// </summary>
        internal static BlobContainerClient InboxItemContainer { get; } = new BlobContainerClient(ConnectionString, "inbox-items");

        /// <summary>
        /// Retrieves a mailbox with a given name.
        /// </summary>
        /// <remarks>
        /// This method <em>always</em> returns the <see cref="Mailbox"/> entity if it exists in storage,
        /// even if it is marked as <see cref="Mailbox.Inactive"/> or <see cref="Mailbox.Deleted"/>.
        /// </remarks>
        internal static async Task<Mailbox?> LookupMailboxAsync(string name, CancellationToken cancellationToken)
        {
            TableQuery<Mailbox> query = new TableQuery<Mailbox>()
                .Where(TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition(nameof(Mailbox.PartitionKey), QueryComparisons.Equal, name),
                    TableOperators.And,
                    TableQuery.GenerateFilterCondition(nameof(Mailbox.RowKey), QueryComparisons.Equal, name)));

            TableContinuationToken? continuationToken = null;
            do
            {
                TableQuerySegment<Mailbox> result = await InboxTable.ExecuteQuerySegmentedAsync(query, continuationToken, null, null, cancellationToken);
                if (result.Results.Count > 0)
                {
                    return result.Results.Single();
                }
            }
            while (continuationToken is object);

            return null;
        }
    }
}
