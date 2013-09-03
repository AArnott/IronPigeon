namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Runtime.Serialization;
	using System.Text;
	using System.Threading.Tasks;
	using Validation;

	/// <summary>
	/// Describes where some encrypted payload is found and how to decrypt it.
	/// </summary>
	[DataContract]
	public class PayloadReference {
		/// <summary>
		/// Initializes a new instance of the <see cref="PayloadReference" /> class.
		/// </summary>
		/// <param name="location">The URL where the payload of the message may be downloaded.</param>
		/// <param name="hash">The hash of the encrypted bytes of the payload.</param>
		/// <param name="hashAlgorithmName">Name of the hash algorithm.</param>
		/// <param name="key">The symmetric key used to encrypt the payload.</param>
		/// <param name="iv">The initialization vector used to encrypt the payload.</param>
		/// <param name="expiresUtc">The time beyond which the payload is expected to be deleted.</param>
		public PayloadReference(Uri location, byte[] hash, string hashAlgorithmName, byte[] key, byte[] iv, DateTime expiresUtc) {
			Requires.NotNull(location, "location");
			Requires.NotNullOrEmpty(hash, "hash");
			Requires.NotNullOrEmpty(hashAlgorithmName, "hashAlgorithmName");
			Requires.NotNullOrEmpty(key, "key");
			Requires.NotNullOrEmpty(iv, "iv");
			Requires.That(expiresUtc.Kind == DateTimeKind.Utc, "expiresUtc", Strings.UTCTimeRequired);

			this.Location = location;
			this.Hash = hash;
			this.HashAlgorithmName = hashAlgorithmName;
			this.Key = key;
			this.IV = iv;
			this.ExpiresUtc = expiresUtc;
		}

		/// <summary>
		/// Gets or sets the Internet location from which the payload can be downloaded.
		/// </summary>
		[DataMember]
		public Uri Location { get; set; }

		/// <summary>
		/// Gets or sets the hash of the message's encrypted bytes.
		/// </summary>
		/// <remarks>
		/// This value can be used by the recipient to verify that the actual message, when downloaded,
		/// has not be altered from the author's original version.
		/// </remarks>
		[DataMember]
		public byte[] Hash { get; set; }

		/// <summary>
		/// Gets or sets the name of the hash algorithm used to sign the message's encrypted bytes.
		/// </summary>
		/// <value>May be <c>null</c> for older remote parties.</value>
		[DataMember]
		public string HashAlgorithmName { get; set; }

		/// <summary>
		/// Gets or sets the material to reconstruct the symmetric key to decrypt the referenced message.
		/// </summary>
		/// <value>The symmetric key data, or <c>null</c> if this information is not disclosed.</value>
		/// <remarks>
		/// This may be <c>null</c> when a user means to refer to some message in a conversation with
		/// another user, without inadvertently disclosing the decryption key for that message in case
		/// permission for that message had not been granted to the other user.
		/// </remarks>
		[DataMember]
		public byte[] Key { get; set; }

		/// <summary>
		/// Gets or sets the initialization vector used to encrypt the payload.
		/// </summary>
		[DataMember]
		public byte[] IV { get; set; }

		/// <summary>
		/// Gets or sets the time when the message referred to is expected to be deleted.
		/// </summary>
		/// <value>
		/// The expiration date, in UTC.
		/// </value>
		[DataMember]
		public DateTime ExpiresUtc { get; set; }

		/// <summary>
		/// Gets or sets the URI from which this instance was downloaded.
		/// </summary>
		internal Uri ReferenceLocation { get; set; }
	}
}
