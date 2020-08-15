// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.Diagnostics;
    using System.Runtime.Serialization;

    /// <summary>
    /// An entity that is capable of receiving messages via the IronPigeon protocol.
    /// </summary>
    [DataContract]
    [DebuggerDisplay("{MessageReceivingEndpoint}")]
    public class Endpoint : IEquatable<Endpoint>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Endpoint"/> class.
        /// </summary>
        public Endpoint()
        {
            this.CreatedOnUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets or sets the URL where notification messages to this recipient may be posted.
        /// </summary>
        [DataMember]
        public Uri? MessageReceivingEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the key material for the public key this contact uses for signing messages.
        /// </summary>
        [DataMember]
        public byte[]? SigningKeyPublicMaterial { get; set; }

        /// <summary>
        /// Gets or sets the key material for the public key used to encrypt messages for this contact.
        /// </summary>
        [DataMember]
        public byte[]? EncryptionKeyPublicMaterial { get; set; }

        /// <summary>
        /// Gets or sets the date this endpoint was created.
        /// </summary>
        /// <value>
        /// The datetime in UTC.
        /// </value>
        [DataMember]
        public DateTime CreatedOnUtc { get; set; }

        /// <summary>
        /// Gets or sets an array of identifiers authorized to claim this endpoint.
        /// </summary>
        /// <remarks>
        /// The set of identifiers in this array are *not* to be trusted as belonging to this endpoint,
        /// and the endpoint sent from a remote party can claim anything.  The contents must be
        /// verified by the receiving end.
        /// This property is present so that when a message arrives, the receiving end has a list of
        /// identifiers to try to perform discovery on in order to provide the receiving user a human
        /// recognizable and verified idea of who sent the message.
        /// </remarks>
        [DataMember]
        public string[]? AuthorizedIdentifiers { get; set; }

        /// <summary>
        /// Checks equality between this and another instance.
        /// </summary>
        /// <param name="obj">The <see cref="object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as Endpoint);
        }

        /// <summary>
        /// Gets a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
        /// </returns>
        public override int GetHashCode()
        {
            return this.MessageReceivingEndpoint != null ? this.MessageReceivingEndpoint.GetHashCode() : 0;
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        public bool Equals(Endpoint? other)
        {
            if (other == null)
            {
                return false;
            }

            return this.MessageReceivingEndpoint == other.MessageReceivingEndpoint
                && Utilities.AreEquivalent(this.SigningKeyPublicMaterial, other.SigningKeyPublicMaterial)
                && Utilities.AreEquivalent(this.EncryptionKeyPublicMaterial, other.EncryptionKeyPublicMaterial);
        }
    }
}
