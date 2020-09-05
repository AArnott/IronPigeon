// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay
{
    using System;
    using System.Runtime.Serialization;
    using Microsoft;

    /// <summary>
    /// A structured notification that may be posted to a <see cref="Endpoint.MessageReceivingEndpoint"/>.
    /// </summary>
    [DataContract]
    public class InboxItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InboxItem"/> class.
        /// </summary>
        /// <param name="creationUtc">The timestamp that the sender claims to have created this inbox item.</param>
        /// <param name="author">The author of this inbox item (as opposed to the author of the referenced payload itself.</param>
        /// <param name="recipient">The intended recipient of this message.</param>
        /// <param name="payloadReference">A reference to the payload that the <see cref="Author"/> wishes the <see cref="Recipient"/> to receive.</param>
        [MessagePack.SerializationConstructor]
        public InboxItem(DateTime creationUtc, Endpoint author, Endpoint recipient, PayloadReference payloadReference)
        {
            Requires.Argument(creationUtc.Kind == DateTimeKind.Utc, nameof(creationUtc), Strings.UTCTimeRequired);

            this.CreationUtc = creationUtc;
            this.Author = author ?? throw new ArgumentNullException(nameof(author));
            this.Recipient = recipient ?? throw new ArgumentNullException(nameof(recipient));
            this.PayloadReference = payloadReference ?? throw new ArgumentNullException(nameof(payloadReference));
        }

        /// <summary>
        /// Gets the timestamp that the sender claims to have created this inbox item.
        /// </summary>
        /// <remarks>
        /// This value should presumably be similar to <see cref="IncomingInboxItem.DatePostedUtc"/>, but may be different if one party is lying,
        /// or due to clock skew between the author's device and the relay,
        /// or because the <see cref="InboxItem"/> was created and then in the queue awaiting transmission for a long time.
        /// </remarks>
        [DataMember]
        public DateTime CreationUtc { get; }

        /// <summary>
        /// Gets the author of this inbox item (as opposed to the author of the referenced payload itself.
        /// </summary>
        /// <remarks>
        /// When this <see cref="InboxItem"/> is serialized, it should be signed with the private key associated with this author's <see cref="Endpoint.AuthenticatingKeyInputs"/>.
        /// </remarks>
        [DataMember]
        public Endpoint Author { get; }

        /// <summary>
        /// Gets the intended recipient of this message.
        /// </summary>
        /// <remarks>
        /// The recipient is included in the signed part of the notification to prevent a receiver of this <see cref="InboxItem"/> from "forwarding" the signed <see cref="InboxItem"/>
        /// to someone else, such that the next person believes that the <see cref="Author"/> intended this notification for them.
        /// </remarks>
        [DataMember]
        public Endpoint Recipient { get; }

        /// <summary>
        /// Gets a reference to the payload that the <see cref="Author"/> wishes the <see cref="Recipient"/> to receive.
        /// </summary>
        [DataMember]
        public PayloadReference PayloadReference { get; }

        /// <summary>
        /// Gets or sets the item originally received from the relay server.
        /// </summary>
        [IgnoreDataMember]
        public IncomingInboxItem? RelayServerItem { get; set; }
    }
}
