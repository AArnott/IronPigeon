namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Runtime.Serialization;
	using System.Text;
	using System.Threading.Tasks;
	using Microsoft;

	/// <summary>
	/// A self-signed description of an endpoint including public signing and encryption keys.
	/// </summary>
	[DataContract]
	public class AddressBookEntry {
		/// <summary>
		/// The Content-Type that identifies a blob containing a serialized instance of this type.
		/// </summary>
		public const string ContentType = "ironpigeon/addressbookentry";

		/// <summary>
		/// Gets or sets the serialized <see cref="Endpoint"/>.
		/// </summary>
		[DataMember]
		public byte[] SerializedEndpoint { get; set; }

		/// <summary>
		/// Gets or sets the signature of the <see cref="SerializedEndpoint"/> bytes,
		/// as signed by the private counterpart to the 
		/// public key stored in <see cref="Endpoint.SigningKeyPublicMaterial"/>.
		/// </summary>
		/// <remarks>
		/// The point of this signature is to prove that the owner (the signer) 
		/// has approved of the encryption key that is also included in the endpoint
		/// metadata.  This mitigates a rogue address book entry that claims to
		/// be someone (a victim) by using their public signing key, but with an
		/// encryption key that the attacker controls the private key to.
		/// </remarks>
		[DataMember]
		public byte[] Signature { get; set; }

		/// <summary>
		/// Deserializes an endpoint from an address book entry and validates that the signatures are correct.
		/// </summary>
		/// <param name="cryptoProvider">The cryptographic provider that will be used to verify the signature.</param>
		/// <returns>The deserialized endpoint.</returns>
		/// <exception cref="BadAddressBookEntryException">Thrown if the signatures are invalid.</exception>
		public Endpoint ExtractEndpoint(ICryptoProvider cryptoProvider) {
			Requires.NotNull(cryptoProvider, "cryptoProvider");

			var reader = new BinaryReader(new MemoryStream(this.SerializedEndpoint));
			Endpoint endpoint;
			try {
				endpoint = reader.DeserializeDataContract<Endpoint>();
			} catch (SerializationException ex) {
				throw new BadAddressBookEntryException(ex.Message, ex);
			}

			try {
				if (!cryptoProvider.VerifySignature(endpoint.SigningKeyPublicMaterial, this.SerializedEndpoint, this.Signature)) {
					throw new BadAddressBookEntryException(Strings.AddressBookEntrySignatureDoesNotMatch);
				}
			} catch (Exception ex) { // all those platform-specific exceptions that aren't available to portable libraries.
				throw new BadAddressBookEntryException(Strings.AddressBookEntrySignatureDoesNotMatch, ex);
			}

			return endpoint;
		}
	}
}
