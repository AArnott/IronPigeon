// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.Diagnostics;
    using System.Runtime.Serialization;
    using MessagePack;
    using Microsoft;
    using PCLCrypto;

    /// <summary>
    /// A self-signed description of an endpoint including public signing and encryption keys.
    /// </summary>
    [DataContract]
    public class AddressBookEntry
    {
        /// <summary>
        /// The Content-Type that identifies a blob containing a serialized instance of this type.
        /// </summary>
        public const string ContentType = "ironpigeon/addressbookentry";

        /// <summary>
        /// Initializes a new instance of the <see cref="AddressBookEntry"/> class.
        /// </summary>
        /// <param name="serializedEndpoint">The serialized <see cref="Endpoint"/>.</param>
        /// <param name="signature">The signature of the <paramref name="serializedEndpoint"/>, as signed by the private counterpart to the public key stored in <see cref="Endpoint.AuthenticatingKeyInputs"/>.</param>
        public AddressBookEntry(ReadOnlyMemory<byte> serializedEndpoint, ReadOnlyMemory<byte> signature)
        {
            this.SerializedEndpoint = serializedEndpoint;
            this.Signature = signature;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AddressBookEntry"/> class
        /// with the data and signing key described in a <see cref="OwnEndpoint.PublicEndpoint"/>.
        /// </summary>
        /// <param name="endpoint">The endpoint to create an address book entry for.</param>
        /// <returns>The address book entry.</returns>
        public AddressBookEntry(OwnEndpoint endpoint)
        {
            Requires.NotNull(endpoint, nameof(endpoint));

            byte[] serializedEndpoint = MessagePackSerializer.Serialize(endpoint.PublicEndpoint, Utilities.MessagePackSerializerOptions);
            this.SerializedEndpoint = serializedEndpoint;
            using ICryptographicKey signingKey = endpoint.SigningKeyInputs.CreateKey();
            this.Signature = WinRTCrypto.CryptographicEngine.Sign(signingKey, serializedEndpoint);
        }

        /// <summary>
        /// Gets the serialized <see cref="Endpoint"/>.
        /// </summary>
        [DataMember(Order = 0)]
        public ReadOnlyMemory<byte> SerializedEndpoint { get; }

        /// <summary>
        /// Gets the signature of the <see cref="SerializedEndpoint"/> bytes,
        /// as signed by the private counterpart to the
        /// public key stored in <see cref="Endpoint.AuthenticatingKeyInputs"/>.
        /// </summary>
        /// <remarks>
        /// The point of this signature is to prove that the owner (the signer)
        /// approves of the public encryption key that is also included in the endpoint
        /// metadata. This mitigates a rogue address book entry that claims to
        /// be someone (a victim) by using their public signing key, but with an
        /// encryption key that the attacker controls the private key to.
        /// </remarks>
        [DataMember(Order = 1)]
        public ReadOnlyMemory<byte> Signature { get; }

        /// <summary>
        /// Gets the thumbprint for this instance.
        /// </summary>
        /// <remarks>
        /// A thumbprint can be passed around in the <see cref="Uri.Fragment"/> of the URL where an <see cref="AddressBookEntry"/> is stored
        /// to give clients a way to detect whether the <see cref="AddressBookEntry"/> they get is the one intended.
        /// </remarks>
        [IgnoreDataMember]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public string Thumbprint => Utilities.ToBase64WebSafe(this.Signature.AsOrCreateArray());

        /// <summary>
        /// Gets the deserialized <see cref="Endpoint"/> after verifying the <see cref="Signature"/>.
        /// </summary>
        /// <returns>The deserialized endpoint.</returns>
        /// <exception cref="BadAddressBookEntryException">Thrown if deserialization fails or the signature is invalid.</exception>
        public Endpoint ExtractEndpoint()
        {
            Endpoint endpoint;
            try
            {
                endpoint = MessagePackSerializer.Deserialize<Endpoint>(this.SerializedEndpoint, Utilities.MessagePackSerializerOptions);
            }
            catch (MessagePackSerializationException ex)
            {
                throw new BadAddressBookEntryException(ex.Message, ex);
            }

            using ICryptographicKey signingKey = endpoint.AuthenticatingKeyInputs.CreateKey();
            if (!WinRTCrypto.CryptographicEngine.VerifySignature(signingKey, this.SerializedEndpoint.AsOrCreateArray(), this.Signature.AsOrCreateArray()))
            {
                throw new BadAddressBookEntryException(Strings.AddressBookEntrySignatureDoesNotMatch);
            }

            return endpoint;
        }
    }
}
