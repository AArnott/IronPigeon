// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay
{
    using System;
    using System.Runtime.Serialization;
    using Microsoft;

    /// <summary>
    /// The response from a relay server to a request for a new inbox.
    /// </summary>
    [DataContract]
    public class InboxCreationResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InboxCreationResponse"/> class.
        /// </summary>
        /// <param name="messageReceivingEndpoint">The message receiving endpoint assigned to the newly created inbox.</param>
        /// <param name="inboxOwnerCode">The base64 representation of a secret meant only for the owner of the inbox.</param>
        public InboxCreationResponse(Uri messageReceivingEndpoint, string inboxOwnerCode)
        {
            Requires.NotNullOrEmpty(inboxOwnerCode, nameof(inboxOwnerCode));

            this.MessageReceivingEndpoint = messageReceivingEndpoint ?? throw new ArgumentNullException(nameof(messageReceivingEndpoint));
            this.InboxOwnerCode = inboxOwnerCode;
        }

        /// <summary>
        /// Gets the message receiving endpoint assigned to the newly created inbox.
        /// </summary>
        /// <value>
        /// The message receiving endpoint.
        /// </value>
        [DataMember]
        public Uri MessageReceivingEndpoint { get; }

        /// <summary>
        /// Gets a secret that can be used to pick up messages from the <see cref="MessageReceivingEndpoint"/>.
        /// </summary>
        [DataMember]
        public string InboxOwnerCode { get; }
    }
}
