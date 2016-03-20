// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;
    using Validation;

    /// <summary>
    /// The response from a message relay service to the query for new incoming messages.
    /// </summary>
    [DataContract]
    public class IncomingList
    {
        /// <summary>
        /// Gets or sets the list of incoming items.
        /// </summary>
        [DataMember]
        public List<IncomingItem> Items { get; set; }

        /// <summary>
        /// Describes an individual incoming message.
        /// </summary>
        [DataContract]
        public class IncomingItem
        {
            /// <summary>
            /// Field backing the <see cref="DatePostedUtc"/> property.
            /// </summary>
            [IgnoreDataMember]
            private DateTime datePostedUtc;

            /// <summary>
            /// Gets or sets the location from which the incoming <see cref="PayloadReference"/> may be downloaded.
            /// </summary>
            [DataMember]
            public Uri Location { get; set; }

            /// <summary>
            /// Gets or sets the date that this item was posted to this inbox.
            /// </summary>
            /// <value>
            /// A DateTime value in UTC.
            /// </value>
            [DataMember]
            public DateTime DatePostedUtc
            {
                get
                {
                    return this.datePostedUtc;
                }

                set
                {
                    Requires.That(value.Kind != DateTimeKind.Local, "value", "UTC required.");

                    // Convert Unspecified to UTC.
                    // For some reason this isn't required on Microsoft platforms but it is on Mono.
                    this.datePostedUtc = value.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(value, DateTimeKind.Utc) // JSON deserialization leaves it unspecified, so we assume it is UTC.
                        : value;
                }
            }
        }
    }
}
