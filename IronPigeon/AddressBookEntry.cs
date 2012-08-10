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
		/// Gets or sets the serialized <see cref="Endpoint"/>.
		/// </summary>
		[DataMember]
		public byte[] SerializedEndpoint { get; set; }

		/// <summary>
		/// Gets or sets the signature of the <see cref="SerializedEndpoint"/> bytes,
		/// as signed by the private counterpart to the 
		/// public key stored in <see cref="Endpoint.SigningKeyPublicMaterial"/>.
		/// </summary>
		[DataMember]
		public byte[] Signature { get; set; }
	}
}
