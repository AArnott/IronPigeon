// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Contains the serialized form of a <see cref="SignedInboxItem"/> and the instructions to decrypt it.
    /// </summary>
    [DataContract]
    public class InboxItemEnvelope
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InboxItemEnvelope"/> class.
        /// </summary>
        /// <param name="decryptionKey">The instructions for decrypting the <see cref="SignedInboxItem"/> stored in <paramref name="serializedInboxItem"/>.</param>
        /// <param name="serializedInboxItem">The encrypted, serialized <see cref="SignedInboxItem"/>.</param>
        public InboxItemEnvelope(SymmetricEncryptionInputs decryptionKey, ReadOnlyMemory<byte> serializedInboxItem)
        {
            this.DecryptionKey = decryptionKey ?? throw new ArgumentNullException(nameof(decryptionKey));
            this.SerializedInboxItem = serializedInboxItem;
        }

        /// <summary>
        /// Gets the instructions for decrypting the <see cref="SignedInboxItem"/>  stored in <see cref="SerializedInboxItem"/>.
        /// </summary>
        /// <remarks>
        /// The <see cref="CryptoKeyInputs.KeyMaterial"/> is asymmetrically encrypted using the recipient's <see cref="Endpoint.EncryptionKeyInputs"/>.
        /// </remarks>
        [DataMember(Order = 0)]
        public SymmetricEncryptionInputs DecryptionKey { get; }

        /// <summary>
        /// Gets the encrypted, serialized <see cref="SignedInboxItem"/>.
        /// </summary>
        [DataMember(Order = 1)]
        public ReadOnlyMemory<byte> SerializedInboxItem { get; }
    }
}
