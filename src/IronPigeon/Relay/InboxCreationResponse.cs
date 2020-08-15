// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// The response from a relay server to a request for a new inbox.
    /// </summary>
    [DataContract]
    public class InboxCreationResponse
    {
        /// <summary>
        /// Gets or sets the message receiving endpoint assigned to the newly created inbox.
        /// </summary>
        /// <value>
        /// The message receiving endpoint.
        /// </value>
        [DataMember]
        public string? MessageReceivingEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the base64 representation of a secret meant only for the owner of the inbox.
        /// </summary>
        [DataMember]
        public string? InboxOwnerCode { get; set; }
    }
}
