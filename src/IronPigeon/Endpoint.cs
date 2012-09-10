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
	public class Endpoint {
		/// <summary>
		/// Initializes a new instance of the <see cref="Endpoint"/> class.
		/// </summary>
		public Endpoint() {
			this.CreatedOnUtc = DateTime.UtcNow;
		}

		/// <summary>
		/// Gets or sets the URL where notification messages to this recipient may be posted.
		/// </summary>
		[DataMember]
		public Uri MessageReceivingEndpoint { get; set; }

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
		/// Gets or sets the date this endpoint was created.
		/// </summary>
		/// <value>
		/// The datetime in UTC.
		/// </value>
		[DataMember]
		public DateTime CreatedOnUtc { get; set; }

		/// <summary>
		/// Checks equality between this and another instance.
		/// </summary>
		/// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
		/// <returns>
		///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
		/// </returns>
		public override bool Equals(object obj) {
			var other = obj as Endpoint;
			if (other == null) {
				return false;
			}

			return this.MessageReceivingEndpoint == other.MessageReceivingEndpoint
				&& Utilities.AreEquivalent(this.SigningKeyPublicMaterial, other.SigningKeyPublicMaterial)
				&& Utilities.AreEquivalent(this.EncryptionKeyPublicMaterial, other.EncryptionKeyPublicMaterial);
		}

		/// <summary>
		/// Gets a hash code for this instance.
		/// </summary>
		/// <returns>
		/// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
		/// </returns>
		public override int GetHashCode() {
			return this.MessageReceivingEndpoint != null ? this.MessageReceivingEndpoint.GetHashCode() : 0;
		}
	}
}
