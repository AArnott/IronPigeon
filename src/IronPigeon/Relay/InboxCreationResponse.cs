// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay
{
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
        public InboxCreationResponse(string messageReceivingEndpoint, string inboxOwnerCode)
        {
            Requires.NotNullOrEmpty(messageReceivingEndpoint, nameof(messageReceivingEndpoint));
            Requires.NotNullOrEmpty(inboxOwnerCode, nameof(inboxOwnerCode));

            this.MessageReceivingEndpoint = messageReceivingEndpoint;
            this.InboxOwnerCode = inboxOwnerCode;
        }

        /// <summary>
        /// Gets the message receiving endpoint assigned to the newly created inbox.
        /// </summary>
        /// <value>
        /// The message receiving endpoint.
        /// </value>
        [DataMember]
        public string MessageReceivingEndpoint { get; }

        /// <summary>
        /// Gets the base64 representation of a secret meant only for the owner of the inbox.
        /// </summary>
        [DataMember]
        public string InboxOwnerCode { get; }
    }
}
