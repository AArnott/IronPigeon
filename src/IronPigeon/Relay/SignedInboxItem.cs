// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// A wrapper around a serialized <see cref="InboxItem"/> and its signature.
    /// </summary>
    [DataContract]
    public class SignedInboxItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SignedInboxItem"/> class.
        /// </summary>
        /// <param name="serializedInboxItem">The serialized form of an <see cref="InboxItem"/>.</param>
        /// <param name="signature">The signature for the <paramref name="serializedInboxItem"/>.</param>
        public SignedInboxItem(ReadOnlyMemory<byte> serializedInboxItem, ReadOnlyMemory<byte> signature)
        {
            this.SerializedInboxItem = serializedInboxItem;
            this.Signature = signature;
        }

        /// <summary>
        /// Gets the serialized form of an <see cref="InboxItem"/>.
        /// </summary>
        [DataMember]
        public ReadOnlyMemory<byte> SerializedInboxItem { get; }

        /// <summary>
        /// Gets the signature for the <see cref="SerializedInboxItem"/>.
        /// </summary>
        /// <remarks>
        /// Verifying this signature should be done with the <see cref="InboxItem.Author"/>'s <see cref="Endpoint.AuthenticatingKeyInputs"/>.
        /// </remarks>
        [DataMember]
        public ReadOnlyMemory<byte> Signature { get; }
    }
}
