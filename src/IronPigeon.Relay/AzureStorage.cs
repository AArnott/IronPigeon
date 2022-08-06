// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Storage.Blobs;
    using IronPigeon.Relay.Models;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Extensions.Configuration;

    public class AzureStorage
    {
        public AzureStorage(IConfiguration configuration)
        {
            this.AzureStorageConnectionString = configuration.GetValue<string>("AzureStorageConnectionString") ?? throw new InvalidOperationException("Missing AzureStorageConnectionString in configuration.");
            this.CosmosDBConnectionString = configuration.GetValue<string>("CosmosDBConnectionString") ?? throw new InvalidOperationException("Missing CosmosDBConnectionString in configuration.");
            this.TableCloudStorageAccount = CloudStorageAccount.Parse(this.CosmosDBConnectionString);
            this.TableClient = this.TableCloudStorageAccount.CreateCloudTableClient(new TableClientConfiguration
            {
                UseRestExecutorForCosmosEndpoint = true,
            });

            // We want to isolate tests for a particular run, particularly in case they're using a shared resource like a test environment in Azure.
            string? tableNameSuffix = Environment.GetEnvironmentVariable("BUILD_BUILDID");

            this.InboxTable = this.TableClient.GetTableReference("Inboxes" + tableNameSuffix);
            this.InboxItemContainer = new BlobContainerClient(this.AzureStorageConnectionString, "inbox-items" + tableNameSuffix);
            this.PayloadBlobsContainer = new BlobContainerClient(this.AzureStorageConnectionString, "payloads" + tableNameSuffix);
        }

        /// <summary>
        /// Gets the Azure Storage account connection string.
        /// </summary>
        public string AzureStorageConnectionString { get; }

        /// <summary>
        /// Gets the Cosmos DB account connection string.
        /// </summary>
        public string CosmosDBConnectionString { get; }

        /// <summary>
        /// Gets the table storage account.
        /// </summary>
        public CloudStorageAccount TableCloudStorageAccount { get; }

        /// <summary>
        /// Gets an Azure Table client.
        /// </summary>
        public CloudTableClient TableClient { get; }

        /// <summary>
        /// Gets the inboxes table.
        /// </summary>
        public CloudTable InboxTable { get; }

        /// <summary>
        /// Gets the blob container to use for inbox items.
        /// </summary>
        public BlobContainerClient InboxItemContainer { get; }

        /// <summary>
        /// Gets the blob container to use for payloads.
        /// </summary>
        public BlobContainerClient PayloadBlobsContainer { get; }

        /// <summary>
        /// Retrieves a mailbox with a given name.
        /// </summary>
        /// <remarks>
        /// This method <em>always</em> returns the <see cref="Mailbox"/> entity if it exists in storage,
        /// even if it is marked as <see cref="Mailbox.Inactive"/> or <see cref="Mailbox.Deleted"/>.
        /// </remarks>
        internal async Task<Mailbox?> LookupMailboxAsync(string name, CancellationToken cancellationToken)
        {
            TableQuery<Mailbox> query = new TableQuery<Mailbox>()
                .Where(TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition(nameof(Mailbox.PartitionKey), QueryComparisons.Equal, name),
                    TableOperators.And,
                    TableQuery.GenerateFilterCondition(nameof(Mailbox.RowKey), QueryComparisons.Equal, name)));

            TableContinuationToken? continuationToken = null;
            do
            {
                TableQuerySegment<Mailbox> result = await this.InboxTable.ExecuteQuerySegmentedAsync(query, continuationToken, null, null, cancellationToken);
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
