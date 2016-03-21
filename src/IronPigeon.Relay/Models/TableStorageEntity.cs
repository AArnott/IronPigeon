// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data.Services.Common;
    using System.Linq;
    using System.Web;
    using Microsoft.WindowsAzure.Storage.Table.DataServices;

    /// <summary>
    /// A base class for Azure Table storage entities.
    /// </summary>
    [DataServiceKey("PartitionKey", "RowKey")]
    public abstract class TableStorageEntity : TableServiceEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TableStorageEntity" /> class.
        /// </summary>
        protected TableStorageEntity()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TableStorageEntity" /> class.
        /// </summary>
        /// <param name="partitionKey">The partition key.</param>
        /// <param name="rowKey">The row key.</param>
        protected TableStorageEntity(string partitionKey, string rowKey)
            : base(partitionKey, rowKey)
        {
        }
    }
}