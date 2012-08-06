namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Runtime.Serialization;
	using System.Text;
	using System.Threading.Tasks;

	[DataContract]
	public class AddressBookEntry {
		/// <summary>
		/// Gets or sets the identifier on which discovery resulted in this instance.
		/// </summary>
		/// <value>A human-recognizable identifier (typically an email address or public key thumbprint).</value>
		[DataMember]
		public string Identifier { get; set; }

		/// <summary>
		/// The serialized <see cref="Endpoint"/>.
		/// </summary>
		[DataMember]
		public byte[] SerializedEndpoint { get; set; }

		/// <summary>
		/// The signature of the <see cref="SerializedEndpoint"/> bytes,
		/// as signed by the private counterpart to the 
		/// public key stored in <see cref="Endpoint.SigningKeyPublicMaterial"/>.
		/// </summary>
		[DataMember]
		public byte[] Signature { get; set; }
	}
}
