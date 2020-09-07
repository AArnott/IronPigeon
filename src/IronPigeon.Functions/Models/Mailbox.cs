// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Functions.Models
{
    using System;
    using Microsoft.WindowsAzure.Storage.Table;

    /// <summary>
    /// An Azure Table Storage entity that represents a user's mailbox.
    /// </summary>
    public class Mailbox : TableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Mailbox"/> class.
        /// </summary>
        [Obsolete("The default constructor should only be used to deserialize entities.")]
        public Mailbox()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Mailbox"/> class.
        /// </summary>
        /// <param name="name">The simple name for the mailbox (which gets appended to a base Uri that represents the inbox function.)</param>
        /// <param name="ownerCode">A bearer token that proves ownership of the mailbox.</param>
        public Mailbox(string name, string ownerCode)
        {
            this.PartitionKey = name;
            this.RowKey = name;
            this.OwnerCode = ownerCode;
            this.CreationTimestampUtc = DateTime.UtcNow;
            this.LastAccessedUtc = this.CreationTimestampUtc;
        }

        /// <summary>
        /// Gets the simple name of the mailbox.
        /// </summary>
        public string Name => this.RowKey;

        /// <summary>
        /// Gets or sets the UTC time this mailbox was created.
        /// </summary>
        public DateTime CreationTimestampUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC time this mailbox was last accessed.
        /// </summary>
        public DateTime LastAccessedUtc { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this mailbox has been purged of any items and is rejecting posts
        /// (till it is reactivated by its owner).
        /// </summary>
        public bool Inactive { get; set; }

        /// <summary>
        /// Gets or sets the bearer token that proves ownership of the mailbox.
        /// </summary>
        public string OwnerCode { get; set; } = null!; // non-null guaranteed by constructor and that this was in the original version.
    }
}
