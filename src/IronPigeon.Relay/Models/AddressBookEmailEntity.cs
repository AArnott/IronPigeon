// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;
    using System.Web;

    public class AddressBookEmailEntity : TableStorageEntity
    {
        /// <summary>
        /// The default partition that address books are filed under.
        /// </summary>
        private const string DefaultPartition = "AddressBook";

        /// <summary>
        /// Initializes a new instance of the <see cref="AddressBookEmailEntity" /> class.
        /// </summary>
        public AddressBookEmailEntity()
        {
            this.PartitionKey = DefaultPartition;
        }

        /// <summary>
        /// Gets or sets the row key of the address book to which this email entity belongs.
        /// </summary>
        public string AddressBookEntityRowKey { get; set; }

        /// <summary>
        /// Gets or sets the email address.
        /// </summary>
        [NotMapped]
        public string Email
        {
            get { return this.RowKey; }
            set { this.RowKey = value.ToLowerInvariant(); }
        }

        /// <summary>
        /// Gets or sets the Microsoft Live calculated hash for the email address.
        /// </summary>
        public string MicrosoftEmailHash { get; set; }
    }
}