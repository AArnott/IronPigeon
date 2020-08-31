// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.Diagnostics;
    using System.Runtime.Serialization;
    using Microsoft;

    /// <summary>
    /// An entity that is capable of receiving messages via the IronPigeon protocol.
    /// </summary>
    [DataContract]
    [DebuggerDisplay("{" + nameof(MessageReceivingEndpoint) + "}")]
    public class Endpoint : IEquatable<Endpoint>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Endpoint"/> class.
        /// </summary>
        /// <param name="messageReceivingEndpoint">The URL where notification messages to this recipient may be posted.</param>
        /// <param name="signingKeyInputs">Instructions for signing messages directed to this endpoint.</param>
        /// <param name="encryptionKeyInputs">Instructions for encrypting messages directed to this endpoint.</param>
        public Endpoint(Uri messageReceivingEndpoint, AsymmetricKeyInputs signingKeyInputs, AsymmetricKeyInputs encryptionKeyInputs)
        {
            this.MessageReceivingEndpoint = messageReceivingEndpoint ?? throw new ArgumentNullException(nameof(messageReceivingEndpoint));
            this.AuthenticatingKeyInputs = signingKeyInputs ?? throw new ArgumentNullException(nameof(signingKeyInputs));
            this.EncryptionKeyInputs = encryptionKeyInputs ?? throw new ArgumentNullException(nameof(encryptionKeyInputs));

            // Defend against accidentally divulging private key data in a class that should be fit for public sharing.
            Requires.Argument(!signingKeyInputs.HasPrivateKey, nameof(signingKeyInputs), Strings.PrivateKeyDataNotAllowed);
            Requires.Argument(!encryptionKeyInputs.HasPrivateKey, nameof(encryptionKeyInputs), Strings.PrivateKeyDataNotAllowed);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Endpoint"/> class.
        /// </summary>
        /// <param name="copyFrom">An existing instance to copy properties from.</param>
        public Endpoint(Endpoint copyFrom)
        {
            Requires.NotNull(copyFrom, nameof(copyFrom));

            this.MessageReceivingEndpoint = copyFrom.MessageReceivingEndpoint;
            this.AuthenticatingKeyInputs = copyFrom.AuthenticatingKeyInputs;
            this.EncryptionKeyInputs = copyFrom.EncryptionKeyInputs;
        }

        /// <summary>
        /// Gets the URL where notification messages to this recipient may be posted.
        /// </summary>
        [DataMember(Order = 0)]
        public Uri MessageReceivingEndpoint { get; }

        /// <summary>
        /// Gets the key material to use when verifying signatures of messages sent from this <see cref="Endpoint"/>.
        /// </summary>
        /// <remarks>
        /// This signing key may also be used to sign a serialized version of this very <see cref="Endpoint"/>.
        /// </remarks>
        [DataMember(Order = 1)]
        public AsymmetricKeyInputs AuthenticatingKeyInputs { get; }

        /// <summary>
        /// Gets the key material for the public key used to encrypt messages for this contact.
        /// </summary>
        [DataMember(Order = 2)]
        public AsymmetricKeyInputs EncryptionKeyInputs { get; }

        /// <inheritdoc />
        public override bool Equals(object? obj) => this.Equals(obj as Endpoint);

        /// <inheritdoc />
        public override int GetHashCode() => this.MessageReceivingEndpoint != null ? this.MessageReceivingEndpoint.GetHashCode() : 0;

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        public bool Equals(Endpoint? other)
        {
            return other is object
                && this.MessageReceivingEndpoint == other.MessageReceivingEndpoint
                && this.AuthenticatingKeyInputs.Equals(other.AuthenticatingKeyInputs)
                && this.EncryptionKeyInputs.Equals(other.EncryptionKeyInputs);
        }
    }
}
