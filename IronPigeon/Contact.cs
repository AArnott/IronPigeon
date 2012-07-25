namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics.Contracts;
	using System.IO;
	using System.Linq;
	using System.Runtime.Serialization;
	using System.Text;
	using System.Threading.Tasks;

	/// <summary>
	/// An entity that is capable of receiving messages via the IronPigeon protocol.
	/// </summary>
	[DataContract]
	public class Contact {
		/// <summary>
		/// Initializes a new instance of the <see cref="Contact"/> class.
		/// </summary>
		public Contact() {
		}

		/// <summary>
		/// Gets or sets the URL where notification messages to this recipient may be posted.
		/// </summary>
		[DataMember]
		public Uri MessageReceivingEndpoint { get; set; }

		/// <summary>
		/// Gets or sets the identifier on which discovery resulted in this instance.
		/// </summary>
		/// <value>A human-recognizable identifier (typically an email address or public key thumbprint).</value>
		[DataMember]
		public string Identifier { get; set; }

		/// <summary>
		/// Gets or sets the key material for the public key this contact uses for signing messages.
		/// </summary>
		[DataMember]
		public byte[] SigningKeyPublicMaterial { get; set; }

		/// <summary>
		/// Gets or sets the key material for the public key used to encrypt messages for this contact.
		/// </summary>
		[DataMember]
		public byte[] EncryptionKeyPublicMaterial { get; set; }

		/// <summary>
		/// Gets or sets the signing public key's thumbprint.
		/// </summary>
		[DataMember]
		public byte[] SigningKeyThumbprint { get; set; }
	}
}
