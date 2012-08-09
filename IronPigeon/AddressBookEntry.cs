namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Runtime.Serialization;
	using System.Text;
	using System.Threading.Tasks;
	using Microsoft;

	[DataContract]
	public class AddressBookEntry {
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
