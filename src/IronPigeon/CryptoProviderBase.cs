namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using PCLCrypto;
	using Validation;

	/// <summary>
	/// A common base class for implementations of the <see cref="ICryptoProvider" /> interface.
	/// </summary>
	public abstract class CryptoProviderBase : ICryptoProvider {
		/// <summary>
		/// Backing field for the <see cref="SymmetricHashAlgorithmName"/> property.
		/// </summary>
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private string symmetricHashAlgorithmName = SecurityLevel.Maximum.SymmetricHashAlgorithmName;

		/// <summary>
		/// Backing field for the <see cref="AsymmetricHashAlgorithmName"/> property.
		/// </summary>
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private string asymmetricHashAlgorithmName = SecurityLevel.Maximum.AsymmetricHashAlgorithmName;

		/// <summary>
		/// Backing field for the <see cref="SymmetricEncryptionConfiguration"/> property.
		/// </summary>
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private EncryptionConfiguration symmetricEncryptionConfiguration = SecurityLevel.Maximum.SymmetricEncryptionConfiguration;

		/// <summary>
		/// Backing field for the <see cref="EncryptionAsymmetricKeySize"/> property.
		/// </summary>
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private int encryptionAsymmetricKeySize = SecurityLevel.Maximum.EncryptionAsymmetricKeySize;

		/// <summary>
		/// Backing field for the <see cref="SignatureAsymmetricKeySize"/> property.
		/// </summary>
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private int signatureAsymmetricKeySize = SecurityLevel.Maximum.SignatureAsymmetricKeySize;

		/// <summary>
		/// Backing field for the <see cref="SymmetricEncryptionKeySize"/> property.
		/// </summary>
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private int blobSymmetricKeySize = SecurityLevel.Maximum.BlobSymmetricKeySize;

		/// <summary>
		/// Backing field for the <see cref="ECDiffieHellmanKeySize"/> property.
		/// </summary>
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private int ecdiffieHellmanKeySize = SecurityLevel.Maximum.ECDiffieHellmanKeySize;

		/// <summary>
		/// Backing field for the <see cref="ECDsaKeySize"/> property.
		/// </summary>
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private int ecdsaKeySize = SecurityLevel.Maximum.ECDsaKeySize;

		protected CryptoProviderBase() {
			this.SigningAlgorithm = AsymmetricAlgorithm.RsaSignPkcs1Sha256;
			this.EncryptionAlgorithm = AsymmetricAlgorithm.RsaOaepSha1;
        }

		/// <summary>
		/// Gets or sets the name of the hash algorithm to use for symmetric signatures.
		/// </summary>
		public string SymmetricHashAlgorithmName {
			get { return this.symmetricHashAlgorithmName; }
			set { this.symmetricHashAlgorithmName = value; }
		}

		/// <summary>
		/// Gets or sets the name of the algorithm to use for asymmetric signatures.
		/// </summary>
		public string AsymmetricHashAlgorithmName {
			get { return this.asymmetricHashAlgorithmName; }
			set { this.asymmetricHashAlgorithmName = value; }
		}

		public AsymmetricAlgorithm SigningAlgorithm { get;set; }

		/// <summary>
		/// Gets or sets the configuration to use for symmetric encryption.
		/// </summary>
		public EncryptionConfiguration SymmetricEncryptionConfiguration {
			get { return this.symmetricEncryptionConfiguration; }
			set { this.symmetricEncryptionConfiguration = value; }
		}

		/// <summary>
		/// Gets or sets the size of the key (in bits) used for asymmetric signatures.
		/// </summary>
		public int SignatureAsymmetricKeySize {
			get { return this.signatureAsymmetricKeySize; }
			set { this.signatureAsymmetricKeySize = value; }
		}

		/// <summary>
		/// Gets or sets the size of the key (in bits) used for asymmetric encryption.
		/// </summary>
		public int EncryptionAsymmetricKeySize {
			get { return this.encryptionAsymmetricKeySize; }
			set { this.encryptionAsymmetricKeySize = value; }
		}

		/// <summary>
		/// Gets or sets the size of the key (in bits) used for symmetric blob encryption.
		/// </summary>
		public int SymmetricEncryptionKeySize {
			get { return this.blobSymmetricKeySize; }
			set { this.blobSymmetricKeySize = value; }
		}

		/// <inheritdoc/>
		public int ECDiffieHellmanKeySize {
			get { return this.ecdiffieHellmanKeySize; }
			set { this.ecdiffieHellmanKeySize = value; }
		}

		/// <inheritdoc/>
		public int ECDsaKeySize {
			get { return this.ecdsaKeySize; }
			set { this.ecdsaKeySize = value; }
		}

		public AsymmetricAlgorithm EncryptionAlgorithm { get; set; }

		/// <summary>
		/// Gets the length (in bits) of the symmetric encryption cipher block.
		/// </summary>
		public abstract int SymmetricEncryptionBlockSize { get; }

		/// <summary>
		/// Derives a cryptographically strong key from the specified password.
		/// </summary>
		/// <param name="password">The user-supplied password.</param>
		/// <param name="salt">The salt.</param>
		/// <param name="iterations">The rounds of computation to use in deriving a stronger key. The larger this is, the longer attacks will take.</param>
		/// <param name="keySizeInBytes">The desired key size in bytes.</param>
		/// <returns>The generated key.</returns>
		public abstract byte[] DeriveKeyFromPassword(string password, byte[] salt, int iterations, int keySizeInBytes);

		/// <summary>
		/// Computes the authentication code for the contents of a stream given the specified symmetric key.
		/// </summary>
		/// <param name="data">The data to compute the HMAC for.</param>
		/// <param name="key">The key to use in hashing.</param>
		/// <param name="hashAlgorithmName">The hash algorithm to use.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The authentication code.</returns>
		public abstract Task<byte[]> ComputeAuthenticationCodeAsync(Stream data, byte[] key, string hashAlgorithmName, CancellationToken cancellationToken);

		/// <summary>
		/// Symmetrically encrypts the specified buffer using a randomly generated key.
		/// </summary>
		/// <param name="data">The data to encrypt.</param>
		/// <param name="encryptionVariables">Optional encryption variables to use; or <c>null</c> to use randomly generated ones.</param>
		/// <returns>
		/// The result of the encryption.
		/// </returns>
		public virtual SymmetricEncryptionResult Encrypt(byte[] data, SymmetricEncryptionVariables encryptionVariables) {
			Requires.NotNull(data, "data");

			var plaintext = new MemoryStream(data);
			var ciphertext = new MemoryStream();
			var result = this.EncryptAsync(plaintext, ciphertext, encryptionVariables, CancellationToken.None).Result;
			return new SymmetricEncryptionResult(result, ciphertext.ToArray());
		}

		/// <summary>
		/// Symmetrically decrypts a buffer using the specified key.
		/// </summary>
		/// <param name="data">The encrypted data and the key and IV used to encrypt it.</param>
		/// <returns>
		/// The decrypted buffer.
		/// </returns>
		public virtual byte[] Decrypt(SymmetricEncryptionResult data) {
			Requires.NotNull(data, "data");

			var plaintext = new MemoryStream();
			var ciphertext = new MemoryStream(data.Ciphertext);
			this.DecryptAsync(ciphertext, plaintext, data, CancellationToken.None).Wait();
			return plaintext.ToArray();
		}

		/// <summary>
		/// Symmetrically encrypts a stream.
		/// </summary>
		/// <param name="plaintext">The stream of plaintext to encrypt.</param>
		/// <param name="ciphertext">The stream to receive the ciphertext.</param>
		/// <param name="encryptionVariables">An optional key and IV to use. May be <c>null</c> to use randomly generated values.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>A task that completes when encryption has completed, whose result is the key and IV to use to decrypt the ciphertext.</returns>
		public abstract Task<SymmetricEncryptionVariables> EncryptAsync(Stream plaintext, Stream ciphertext, SymmetricEncryptionVariables encryptionVariables, CancellationToken cancellationToken);

		/// <summary>
		/// Symmetrically decrypts a stream.
		/// </summary>
		/// <param name="ciphertext">The stream of ciphertext to decrypt.</param>
		/// <param name="plaintext">The stream to receive the plaintext.</param>
		/// <param name="encryptionVariables">The key and IV to use.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>A task that represents the asynchronous operation.</returns>
		public abstract Task DecryptAsync(Stream ciphertext, Stream plaintext, SymmetricEncryptionVariables encryptionVariables, CancellationToken cancellationToken);
	}
}
