// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data.Services.Client;
    using System.Linq;
    using System.Runtime.ExceptionServices;
    using System.Threading.Tasks;
    using System.Web;
    using Microsoft.WindowsAzure.Storage.Table;
    using Microsoft.WindowsAzure.Storage.Table.DataServices;
    using Microsoft.WindowsAzure.StorageClient;
    using Validation;

    public class InboxContext : TableServiceContext
    {
        public InboxContext(CloudTableClient client, string tableName)
            : base(client)
        {
            Requires.NotNullOrEmpty(tableName, "tableName");
            this.TableName = tableName;
        }

        public string TableName { get; private set; }

        public TableServiceQuery<InboxEntity> Get(string rowKey)
        {
            return (from inbox in this.CreateQuery<InboxEntity>(this.TableName)
                    where inbox.RowKey == rowKey
                    select inbox).AsTableServiceQuery(this);
        }

        public void AddObject(InboxEntity entity)
        {
            this.AddObject(this.TableName, entity);
        }

        public async Task SaveChangesWithMergeAsync(InboxEntity inboxEntity)
        {
            const int MaxRetries = 5;
            Exception lastError = null;
            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    if (i > 0)
                    {
                        // Attempt to sync up our inboxEntity with the cloud before saving local changes again.
                        // We can drop the result. Just requerying is enough to solve the problem.
                        await this.Get(inboxEntity.RowKey).ExecuteSegmentedAsync(null);
                    }

                    await this.SaveChangesAsync();
                    return;
                }
                catch (DataServiceRequestException ex)
                {
                    lastError = ex;
                }
            }

            // Rethrow exception. We've failed too many times.
            ExceptionDispatchInfo.Capture(lastError).Throw();
        }
    }
}