// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay.Models
{
    using System;
    using Microsoft.Azure.Cosmos.Table;

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
            : base(name, name)
        {
            this.OwnerCode = ownerCode;
            this.CreationTimestampUtc = DateTime.UtcNow;
            this.LastAuthenticatedInteractionUtc = this.CreationTimestampUtc;
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
        public DateTime LastAuthenticatedInteractionUtc { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this mailbox has been purged of any items and is rejecting posts
        /// (till it is reactivated by its owner).
        /// </summary>
        public bool Inactive { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this mailbox has been explicitly marked for deletion.
        /// </summary>
        /// <remarks>
        /// Deleting a mailbox should include purging all the inbox items, so while we tag a mailbox for deletion immediately, this entity is only actually deleted
        /// after its entries have also been removed.
        /// </remarks>
        public bool Deleted { get; set; }

        /// <summary>
        /// Gets or sets the bearer token that proves ownership of the mailbox.
        /// </summary>
        public string OwnerCode { get; set; } = null!; // non-null guaranteed by constructor and that this was in the original version.
    }
}
