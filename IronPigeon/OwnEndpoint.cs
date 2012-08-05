namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Runtime.Serialization;
	using System.Text;
	using System.Threading.Tasks;
	using Microsoft;

	/// <summary>
	/// The personal contact information for receiving one's own messages.
	/// </summary>
	[DataContract]
	public class OwnEndpoint {
		/// <summary>
		/// Initializes a new instance of the <see cref="OwnEndpoint"/> class.
		/// </summary>
		/// <param name="contact">The public information for this contact.</param>
		/// <param name="signingPrivateKeyMaterial">The private signing key.</param>
		/// <param name="encryptionPrivateKeyMaterial">The private encryption key.</param>
		public OwnEndpoint(Endpoint contact, byte[] signingPrivateKeyMaterial, byte[] encryptionPrivateKeyMaterial) {
			Requires.NotNull(contact, "contact");
			Requires.NotNull(signingPrivateKeyMaterial, "signingPrivateKeyMaterial");
			Requires.NotNull(encryptionPrivateKeyMaterial, "encryptionPrivateKeyMaterial");

			this.PublicEndpoint = contact;
			this.SigningKeyPrivateMaterial = signingPrivateKeyMaterial;
			this.EncryptionKeyPrivateMaterial = encryptionPrivateKeyMaterial;
		}

		/// <summary>
		/// Gets or sets the public information associated with this endpoint.
		/// </summary>
		[DataMember]
		public Endpoint PublicEndpoint { get; private set; }

		/// <summary>
		/// Gets the key material for the private key this personality uses for signing messages.
		/// </summary>
		[DataMember]
		public byte[] SigningKeyPrivateMaterial { get; private set; }

		/// <summary>
		/// Gets the key material for the private key used to decrypt messages.
		/// </summary>
		[DataMember]
		public byte[] EncryptionKeyPrivateMaterial { get; private set; }
	}
}
