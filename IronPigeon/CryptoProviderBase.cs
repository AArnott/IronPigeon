namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	/// <summary>
	/// A common base class for implementations of the <see cref="ICryptoProvider" /> interface.
	/// </summary>
	public abstract class CryptoProviderBase : ICryptoProvider {
		/// <summary>
		/// Backing field for the <see cref="HashAlgorithmName"/> property.
		/// </summary>
		private string hashAlgorithmName = SecurityLevel.Recommended.HashAlgorithmName;

		/// <summary>
		/// Backing field for the <see cref="EncryptionAsymmetricKeySize"/> property.
		/// </summary>
		private int encryptionAsymmetricKeySize = SecurityLevel.Recommended.EncryptionAsymmetricKeySize;

		/// <summary>
		/// Backing field for the <see cref="SignatureAsymmetricKeySize"/> property.
		/// </summary>
		private int signatureAsymmetricKeySize = SecurityLevel.Recommended.SignatureAsymmetricKeySize;

		/// <summary>
		/// Backing field for the <see cref="BlobSymmetricKeySize"/> property.
		/// </summary>
		private int blobSymmetricKeySize = SecurityLevel.Recommended.BlobSymmetricKeySize;

		/// <summary>
		/// Gets or sets the name of the hash algorithm to use.
		/// </summary>
		public string HashAlgorithmName {
			get { return this.hashAlgorithmName; }
			set { this.hashAlgorithmName = value; }
		}

		/// <summary>
		/// Gets or sets the size of the key used for asymmetric signatures.
		/// </summary>
		public int SignatureAsymmetricKeySize {
			get { return this.signatureAsymmetricKeySize; }
			set { this.signatureAsymmetricKeySize = value; }
		}

		/// <summary>
		/// Gets or sets the size of the key used for asymmetric encryption.
		/// </summary>
		public int EncryptionAsymmetricKeySize {
			get { return this.encryptionAsymmetricKeySize; }
			set { this.encryptionAsymmetricKeySize = value; }
		}

		/// <summary>
		/// Gets or sets the size of the key used for symmetric blob encryption.
		/// </summary>
		public int BlobSymmetricKeySize {
			get { return this.blobSymmetricKeySize; }
			set { this.blobSymmetricKeySize = value; }
		}

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
		/// Verifies the asymmetric signature of some data blob.
		/// </summary>
		/// <param name="signingPublicKey">The public key used to verify the signature.</param>
		/// <param name="data">The data that was signed.</param>
		/// <param name="signature">The signature.</param>
		/// <returns>
		/// A value indicating whether the signature is valid.
		/// </returns>
		public abstract bool VerifySignature(byte[] signingPublicKey, byte[] data, byte[] signature);

		/// <summary>
		/// Symmetrically encrypts the specified buffer using a randomly generated key.
		/// </summary>
		/// <param name="data">The data to encrypt.</param>
		/// <returns>
		/// The result of the encryption.
		/// </returns>
		public abstract SymmetricEncryptionResult Encrypt(byte[] data);

		/// <summary>
		/// Symmetrically decrypts a buffer using the specified key.
		/// </summary>
		/// <param name="data">The encrypted data and the key and IV used to encrypt it.</param>
		/// <returns>
		/// The decrypted buffer.
		/// </returns>
		public abstract byte[] Decrypt(SymmetricEncryptionResult data);

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
		/// <returns>
		/// The computed hash.
		/// </returns>
		public abstract byte[] Hash(byte[] data);

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
