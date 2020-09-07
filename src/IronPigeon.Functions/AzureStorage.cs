// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Functions
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;

    internal static class AzureStorage
    {
        /// <summary>
        /// Gets the Azure Storage account to use.
        /// </summary>
        internal static CloudStorageAccount StorageAccount { get; } = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));

        /// <summary>
        /// Gets an Azure Table client to use.
        /// </summary>
        internal static CloudTableClient TableClient { get; } = StorageAccount.CreateCloudTableClient();

        /// <summary>
        /// Gets the inboxes table.
        /// </summary>
        internal static CloudTable InboxTable { get; } = TableClient.GetTableReference("Inboxes");
    }
}
