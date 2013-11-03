namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
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

		/// <summary>
		/// Gets the length (in bits) of the symmetric encryption cipher block.
		/// </summary>
		public abstract int SymmetricEncryptionBlockSize { get; }

		/// <summary>
		/// Fills the specified buffer with cryptographically strong random generated data.
		/// </summary>
		/// <param name="buffer">The buffer to fill.</param>
		public abstract void FillCryptoRandomBuffer(byte[] buffer);

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
		/// Asymmetrically signs a data blob.
		/// </summary>
		/// <param name="data">The data to sign.</param>
		/// <param name="signingPrivateKey">The private key used to sign the data.</param>
		/// <returns>
		/// The signature.
		/// </returns>
		public abstract byte[] Sign(byte[] data, byte[] signingPrivateKey);

		/// <summary>
		/// Asymmetrically signs the hash of data.
		/// </summary>
		/// <param name="hash">The hash to sign.</param>
		/// <param name="signingPrivateKey">The private key used to sign the data.</param>
		/// <param name="hashAlgorithmName">The hash algorithm name.</param>
		/// <returns>
		/// The signature.
		/// </returns>
		public abstract byte[] SignHash(byte[] hash, byte[] signingPrivateKey, string hashAlgorithmName);

		/// <summary>
		/// Verifies the asymmetric signature of some data blob.
		/// </summary>
		/// <param name="signingPublicKey">The public key used to verify the signature.</param>
		/// <param name="data">The data that was signed.</param>
		/// <param name="signature">The signature.</param>
		/// <param name="hashAlgorithm">The hash algorithm used to hash the data.</param>
		/// <returns>
		/// A value indicating whether the signature is valid.
		/// </returns>
		public abstract bool VerifySignature(byte[] signingPublicKey, byte[] data, byte[] signature, string hashAlgorithm);

		/// <summary>
		/// Verifies the asymmetric signature of the hash of some data blob.
		/// </summary>
		/// <param name="signingPublicKey">The public key used to verify the signature.</param>
		/// <param name="hash">The hash of the data that was signed.</param>
		/// <param name="signature">The signature.</param>
		/// <param name="hashAlgorithm">The hash algorithm used to hash the data.</param>
		/// <returns>
		/// A value indicating whether the signature is valid.
		/// </returns>
		public abstract bool VerifyHash(byte[] signingPublicKey, byte[] hash, byte[] signature, string hashAlgorithm);

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

		/// <summary>
		/// Asymmetrically encrypts the specified buffer using the provided public key.
		/// </summary>
		/// <param name="encryptionPublicKey">The public key used to encrypt the buffer.</param>
		/// <param name="data">The buffer to encrypt.</param>
		/// <returns>
		/// The ciphertext.
		/// </returns>
		public abstract byte[] Encrypt(byte[] encryptionPublicKey, byte[] data);

		/// <summary>
		/// Asymmetrically decrypts the specified buffer using the provided private key.
		/// </summary>
		/// <param name="decryptionPrivateKey">The private key used to decrypt the buffer.</param>
		/// <param name="data">The buffer to decrypt.</param>
		/// <returns>
		/// The plaintext.
		/// </returns>
		public abstract byte[] Decrypt(byte[] decryptionPrivateKey, byte[] data);

		/// <summary>
		/// Computes the hash of the specified buffer.
		/// </summary>
		/// <param name="data">The data to hash.</param>
		/// <param name="hashAlgorithmName">Name of the hash algorithm.</param>
		/// <returns>
		/// The computed hash.
		/// </returns>
		public abstract byte[] Hash(byte[] data, string hashAlgorithmName);

		/// <summary>
		/// Hashes the contents of a stream.
		/// </summary>
		/// <param name="source">The stream to hash.</param>
		/// <param name="hashAlgorithmName">The hash algorithm to use.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>A task whose result is the hash.</returns>
		public abstract Task<byte[]> HashAsync(Stream source, string hashAlgorithmName, CancellationToken cancellationToken);

		/// <summary>
		/// Generates a key pair for asymmetric cryptography.
		/// </summary>
		/// <param name="keyPair">Receives the serialized key pair (includes private key).</param>
		/// <param name="publicKey">Receives the public key.</param>
		public abstract void GenerateSigningKeyPair(out byte[] keyPair, out byte[] publicKey);

		/// <summary>
		/// Generates a key pair for asymmetric cryptography.
		/// </summary>
		/// <param name="keyPair">Receives the serialized key pair (includes private key).</param>
		/// <param name="publicKey">Receives the public key.</param>
		public abstract void GenerateEncryptionKeyPair(out byte[] keyPair, out byte[] publicKey);
	}
}
