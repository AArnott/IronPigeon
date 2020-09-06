// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay
{
    using System;
    using System.Buffers;
    using System.Runtime.Serialization;
    using Microsoft;

    /// <summary>
    /// A relay server's wrapper around an <see cref="InboxItem"/>.
    /// </summary>
    [DataContract]
    public class IncomingInboxItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IncomingInboxItem"/> class.
        /// </summary>
        /// <param name="identity">The URL that represents this entry.</param>
        /// <param name="envelope">The envelope left for the user.</param>
        /// <param name="datePostedUtc">The date that this item was posted to this inbox.</param>
        public IncomingInboxItem(Uri identity, ReadOnlySequence<byte> envelope, DateTime datePostedUtc)
        {
            Requires.Argument(datePostedUtc.Kind == DateTimeKind.Utc, nameof(datePostedUtc), Strings.UTCTimeRequired);

            this.Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            this.Envelope = envelope;
            this.DatePostedUtc = datePostedUtc;
        }

        /// <summary>
        /// Gets the URL that represents this entry.
        /// </summary>
        [DataMember]
        public Uri Identity { get; }

        /// <summary>
        /// Gets the serialized form of <see cref="InboxItemEnvelope"/> that was previously posted to a <see cref="Endpoint.MessageReceivingEndpoint"/>.
        /// </summary>
        /// <remarks>
        /// This buffer must be decrypted with the <see cref="OwnEndpoint.DecryptionKeyInputs"/> of the receiver.
        /// </remarks>
        [DataMember]
        public ReadOnlySequence<byte> Envelope { get; }

        /// <summary>
        /// Gets the date that the relay claims to have received the <see cref="Envelope"/>.
        /// </summary>
        [DataMember]
        public DateTime DatePostedUtc { get; }
    }
}
