namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Runtime.Serialization;
	using System.Text;
	using System.Threading;
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
		public OwnEndpoint() {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="OwnEndpoint" /> class.
		/// </summary>
		/// <param name="contact">The public information for this contact.</param>
		/// <param name="signingPrivateKeyMaterial">The private signing key.</param>
		/// <param name="encryptionPrivateKeyMaterial">The private encryption key.</param>
		/// <param name="inboxOwnerCode">The secret that proves ownership of the inbox at the <see cref="Endpoint.MessageReceivingEndpoint"/>.</param>
		public OwnEndpoint(Endpoint contact, byte[] signingPrivateKeyMaterial, byte[] encryptionPrivateKeyMaterial, string inboxOwnerCode = null) {
			Requires.NotNull(contact, "contact");
			Requires.NotNull(signingPrivateKeyMaterial, "signingPrivateKeyMaterial");
			Requires.NotNull(encryptionPrivateKeyMaterial, "encryptionPrivateKeyMaterial");

			this.PublicEndpoint = contact;
			this.SigningKeyPrivateMaterial = signingPrivateKeyMaterial;
			this.EncryptionKeyPrivateMaterial = encryptionPrivateKeyMaterial;
			this.InboxOwnerCode = inboxOwnerCode;
		}

		/// <summary>
		/// Gets or sets the public information associated with this endpoint.
		/// </summary>
		[DataMember]
		public Endpoint PublicEndpoint { get; set; }

		/// <summary>
		/// Gets or sets the key material for the private key this personality uses for signing messages.
		/// </summary>
		[DataMember]
		public byte[] SigningKeyPrivateMaterial { get; set; }

		/// <summary>
		/// Gets or sets the key material for the private key used to decrypt messages.
		/// </summary>
		[DataMember]
		public byte[] EncryptionKeyPrivateMaterial { get; set; }

		/// <summary>
		/// Gets or sets the secret that proves ownership of the inbox at the <see cref="Endpoint.MessageReceivingEndpoint"/>.
		/// </summary>
		[DataMember]
		public string InboxOwnerCode { get; set; }

		/// <summary>
		/// Generates a new receiving endpoint.
		/// </summary>
		/// <param name="cryptoProvider">The crypto provider.</param>
		/// <returns>The newly generated endpoint.</returns>
		/// <remarks>
		/// Depending on the length of the keys set in the provider and the amount of buffered entropy in the operating system,
		/// this method can take an extended period (several seconds) to complete.
		/// </remarks>
		public static OwnEndpoint Create(ICryptoProvider cryptoProvider) {
			Requires.NotNull(cryptoProvider, "cryptoProvider");

			byte[] privateEncryptionKey, publicEncryptionKey;
			byte[] privateSigningKey, publicSigningKey;

			cryptoProvider.GenerateEncryptionKeyPair(out privateEncryptionKey, out publicEncryptionKey);
			cryptoProvider.GenerateSigningKeyPair(out privateSigningKey, out publicSigningKey);

			var contact = new Endpoint() {
				EncryptionKeyPublicMaterial = publicEncryptionKey,
				SigningKeyPublicMaterial = publicSigningKey,
			};

			var ownContact = new OwnEndpoint(contact, privateSigningKey, privateEncryptionKey);
			return ownContact;
		}

		/// <summary>
		/// Creates a signed address book entry that describes the public information in this endpoint.
		/// </summary>
		/// <param name="cryptoServices">The crypto services to use for signing the address book entry.</param>
		/// <returns>The address book entry.</returns>
		public AddressBookEntry CreateAddressBookEntry(ICryptoProvider cryptoServices) {
			Requires.NotNull(cryptoServices, "cryptoServices");

			var ms = new MemoryStream();
			var writer = new BinaryWriter(ms);
			var entry = new AddressBookEntry();
			writer.SerializeDataContract(this.PublicEndpoint);
			writer.Flush();
			entry.SerializedEndpoint = ms.ToArray();
			entry.Signature = cryptoServices.Sign(entry.SerializedEndpoint, this.SigningKeyPrivateMaterial);
			return entry;
		}

		/// <summary>
		/// Saves the receiving endpoint including private data to the specified stream.
		/// </summary>
		/// <param name="target">The stream to write to.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>A task whose completion signals the save is complete.</returns>
		public Task SaveAsync(Stream target, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(target, "target");

			var ms = new MemoryStream();
			using (var writer = new BinaryWriter(ms)) {
				writer.SerializeDataContract(this);
			}

			ms.Position = 0;
			return ms.CopyToAsync(target, 4096, cancellationToken);
		}
	}
}
