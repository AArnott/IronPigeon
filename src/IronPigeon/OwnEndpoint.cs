namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Runtime.Serialization;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using PCLCrypto;
	using Validation;
	using TaskEx = System.Threading.Tasks.Task;

	/// <summary>
	/// The personal contact information for receiving one's own messages.
	/// </summary>
	[DataContract]
	public class OwnEndpoint {
		/// <summary>
		/// The signing key material
		/// </summary>
		private byte[] signingKeyMaterial;

		/// <summary>
		/// The signing key
		/// </summary>
		private ICryptographicKey signingKey;

		/// <summary>
		/// The encryption key material.
		/// </summary>
		private byte[] encryptionKeyMaterial;

		/// <summary>
		/// The encryption key
		/// </summary>
		private ICryptographicKey encryptionKey;

		/// <summary>
		/// Initializes a new instance of the <see cref="OwnEndpoint"/> class.
		/// </summary>
		public OwnEndpoint() {
			// This default is required for backward compat.
			this.PrivateKeyFormat = CryptographicPrivateKeyBlobType.Capi1PrivateKey;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="OwnEndpoint" /> class.
		/// </summary>
		/// <param name="signingKey">The signing key.</param>
		/// <param name="encryptionKey">The encryption key.</param>
		/// <param name="inboxOwnerCode">The secret that proves ownership of the inbox at the <see cref="Endpoint.MessageReceivingEndpoint" />.</param>
		public OwnEndpoint(ICryptographicKey signingKey, ICryptographicKey encryptionKey, string inboxOwnerCode = null)
			: this() {
			Requires.NotNull(signingKey, "signingKey");
			Requires.NotNull(encryptionKey, "encryptionKey");

			this.PublicEndpoint = new Endpoint {
				SigningKeyPublicMaterial = signingKey.ExportPublicKey(CryptoSettings.PublicKeyFormat),
				EncryptionKeyPublicMaterial = encryptionKey.ExportPublicKey(CryptoSettings.PublicKeyFormat),
			};

			// We could preserve the key instances, but that could make
			// our behavior a little less repeatable if we had problems
			// with key serialization.
			////this.signingKey = signingKey;
			////this.encryptionKey = encryptionKey;

			// Since this is a new endpoint we can choose a more modern format for the private keys.
			this.PrivateKeyFormat = CryptographicPrivateKeyBlobType.Pkcs8RawPrivateKeyInfo;
			this.SigningKeyPrivateMaterial = signingKey.Export(this.PrivateKeyFormat);
			this.EncryptionKeyPrivateMaterial = encryptionKey.Export(this.PrivateKeyFormat);
			this.InboxOwnerCode = inboxOwnerCode;
		}

		/// <summary>
		/// Gets or sets the public information associated with this endpoint.
		/// </summary>
		[DataMember]
		public Endpoint PublicEndpoint { get; set; }

		/// <summary>
		/// Gets or sets the private key format used.
		/// </summary>
		[DataMember]
		public CryptographicPrivateKeyBlobType PrivateKeyFormat { get; set; }

		/// <summary>
		/// Gets or sets the key material for the private key this personality uses for signing messages.
		/// </summary>
		[DataMember]
		public byte[] SigningKeyPrivateMaterial {
			get {
				return this.signingKeyMaterial;
			}

			set {
				this.signingKeyMaterial = value;
				this.signingKey = null;
			}
		}

		/// <summary>
		/// Gets or sets the key material for the private key used to decrypt messages.
		/// </summary>
		[DataMember]
		public byte[] EncryptionKeyPrivateMaterial {
			get {
				return this.encryptionKeyMaterial;
			}

			set {
				this.encryptionKeyMaterial = value;
				this.encryptionKey = null;
			}
		}

		/// <summary>
		/// Gets the encryption key.
		/// </summary>
		public ICryptographicKey EncryptionKey {
			get {
				if (this.encryptionKey == null && this.EncryptionKeyPrivateMaterial != null) {
					this.encryptionKey = CryptoSettings.EncryptionAlgorithm.ImportKeyPair(
						this.EncryptionKeyPrivateMaterial,
						this.PrivateKeyFormat);
				}

				return this.encryptionKey;
			}
		}

		/// <summary>
		/// Gets the signing key.
		/// </summary>
		public ICryptographicKey SigningKey {
			get {
				if (this.signingKey == null && this.SigningKeyPrivateMaterial != null) {
					this.signingKey = CryptoSettings.SigningAlgorithm.ImportKeyPair(
						this.SigningKeyPrivateMaterial,
						this.PrivateKeyFormat);
				}

				return this.signingKey;
			}
		}

		/// <summary>
		/// Gets or sets the secret that proves ownership of the inbox at the <see cref="Endpoint.MessageReceivingEndpoint"/>.
		/// </summary>
		[DataMember]
		public string InboxOwnerCode { get; set; }

		/// <summary>
		/// Loads endpoint information including private data from the specified stream.
		/// </summary>
		/// <param name="stream">A stream, previously serialized to using <see cref="SaveAsync"/>.</param>
		/// <returns>A task whose result is the deserialized instance of <see cref="OwnEndpoint"/>.</returns>
		public static async Task<OwnEndpoint> OpenAsync(Stream stream) {
			Requires.NotNull(stream, "stream");

			var ms = new MemoryStream();
			await stream.CopyToAsync(ms);	// relies on the input stream containing only the endpoint.
			ms.Position = 0;
			using (var reader = new BinaryReader(ms)) {
				return reader.DeserializeDataContract<OwnEndpoint>();
			}
		}

		/// <summary>
		/// Creates a signed address book entry that describes the public information in this endpoint.
		/// </summary>
		/// <param name="cryptoServices">The crypto services to use for signing the address book entry.</param>
		/// <returns>The address book entry.</returns>
		public AddressBookEntry CreateAddressBookEntry(CryptoSettings cryptoServices) {
			Requires.NotNull(cryptoServices, "cryptoServices");

			var ms = new MemoryStream();
			var writer = new BinaryWriter(ms);
			var entry = new AddressBookEntry();
			writer.SerializeDataContract(this.PublicEndpoint);
			writer.Flush();
			entry.SerializedEndpoint = ms.ToArray();
			entry.Signature = WinRTCrypto.CryptographicEngine.Sign(this.SigningKey, entry.SerializedEndpoint);
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
				ms.Position = 0;
				return ms.CopyToAsync(target, 4096, cancellationToken);
			}
		}
	}
}
