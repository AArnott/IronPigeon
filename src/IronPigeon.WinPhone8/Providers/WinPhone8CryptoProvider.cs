namespace IronPigeon.WinPhone8.Providers {
	using System;
	using System.Collections.Generic;
	using System.Composition;
	using System.Globalization;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using System.Threading.Tasks;
	using Org.BouncyCastle.Asn1;
	using Org.BouncyCastle.Asn1.X509;
	using Org.BouncyCastle.Crypto;
	using Org.BouncyCastle.Crypto.Engines;
	using Org.BouncyCastle.Crypto.Modes;
	using Org.BouncyCastle.Crypto.Paddings;
	using Org.BouncyCastle.Crypto.Parameters;
	using Org.BouncyCastle.Security;

	using Validation;
	using System.IO;

	/// <summary>
	/// The Windows Phone 8 implementation of the IronPigeon crypto provider.
	/// </summary>
	[Export(typeof(ICryptoProvider))]
	[Shared]
	public class WinPhone8CryptoProvider : CryptoProviderBase {
		/// <summary>
		/// Initializes a new instance of the <see cref="WinPhone8CryptoProvider" /> class
		/// with the default security level.
		/// </summary>
		public WinPhone8CryptoProvider()
			: this(SecurityLevel.Maximum) {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WinPhone8CryptoProvider" /> class.
		/// </summary>
		/// <param name="securityLevel">The security level to apply to this instance.  The default is <see cref="SecurityLevel.Maximum"/>.</param>
		public WinPhone8CryptoProvider(SecurityLevel securityLevel) {
			Requires.NotNull(securityLevel, "securityLevel");
			securityLevel.Apply(this);
		}

		/// <summary>
		/// Gets the length (in bits) of the symmetric encryption cipher block.
		/// </summary>
		public override int SymmetricEncryptionBlockSize {
			get { return this.GetCipher().GetBlockSize() * 8; }
		}

		/// <summary>
		/// Asymmetrically signs a data blob.
		/// </summary>
		/// <param name="data">The data to sign.</param>
		/// <param name="signingPrivateKey">The private key used to sign the data.</param>
		/// <returns>
		/// The signature.
		/// </returns>
		public override byte[] Sign(byte[] data, byte[] signingPrivateKey) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(signingPrivateKey);
				return rsa.SignData(data, this.GetHashAlgorithm(this.AsymmetricHashAlgorithmName));
			}
		}

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
		public override bool VerifySignature(byte[] signingPublicKey, byte[] data, byte[] signature, string hashAlgorithm) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(signingPublicKey);
				return rsa.VerifyData(data, this.GetHashAlgorithm(hashAlgorithm), signature);
			}
		}

		/// <summary>
		/// Symmetrically encrypts the specified buffer using a randomly generated key.
		/// </summary>
		/// <param name="data">The data to encrypt.</param>
		/// <param name="encryptionVariables">Optional encryption variables to use; or <c>null</c> to use randomly generated ones.</param>
		/// <returns>
		/// The result of the encryption.
		/// </returns>
		public override SymmetricEncryptionResult Encrypt(byte[] data, SymmetricEncryptionVariables encryptionVariables) {
			var encryptor = this.GetCipher();

			if (encryptionVariables == null) {
				var secureRandom = new SecureRandom();
				byte[] key = new byte[this.SymmetricEncryptionKeySize / 8];
				secureRandom.NextBytes(key);

				var random = new Random();
				byte[] iv = new byte[encryptor.GetBlockSize()];
				random.NextBytes(iv);

				encryptionVariables = new SymmetricEncryptionVariables(key, iv);
			} else {
				Requires.Argument(encryptionVariables.Key.Length == this.SymmetricEncryptionKeySize / 8, "key", "Incorrect length.");
				Requires.Argument(encryptionVariables.IV.Length == encryptor.GetBlockSize(), "iv", "Incorrect length.");
			}

			var parameters = new ParametersWithIV(new KeyParameter(encryptionVariables.Key), encryptionVariables.IV);
			encryptor.Init(true, parameters);
			byte[] ciphertext = encryptor.DoFinal(data);
			return new SymmetricEncryptionResult(encryptionVariables.Key, encryptionVariables.IV, ciphertext);
		}

		/// <summary>
		/// Symmetrically encrypts a stream.
		/// </summary>
		/// <param name="plaintext">The stream of plaintext to encrypt.</param>
		/// <param name="ciphertext">The stream to receive the ciphertext.</param>
		/// <param name="encryptionVariables">An optional key and IV to use. May be <c>null</c> to use randomly generated values.</param>
		/// <returns>A task that completes when encryption has completed, whose result is the key and IV to use to decrypt the ciphertext.</returns>
		public override Task<SymmetricEncryptionVariables> EncryptAsync(Stream plaintext, Stream ciphertext, SymmetricEncryptionVariables encryptionVariables) {
			throw new NotImplementedException();
		}

		/// <summary>
		/// Symmetrically decrypts a stream.
		/// </summary>
		/// <param name="ciphertext">The stream of ciphertext to decrypt.</param>
		/// <param name="plaintext">The stream to receive the plaintext.</param>
		/// <param name="encryptionVariables">The key and IV to use.</param>
		public override Task DecryptAsync(Stream ciphertext, Stream plaintext, SymmetricEncryptionVariables encryptionVariables) {
			throw new NotImplementedException();
		}

		/// <summary>
		/// Symmetrically decrypts a buffer using the specified key.
		/// </summary>
		/// <param name="data">The encrypted data and the key and IV used to encrypt it.</param>
		/// <returns>
		/// The decrypted buffer.
		/// </returns>
		public override byte[] Decrypt(SymmetricEncryptionResult data) {
			var parameters = new ParametersWithIV(new KeyParameter(data.Key), data.IV);
			var decryptor = this.GetCipher();
			decryptor.Init(false, parameters);
			byte[] plaintext = decryptor.DoFinal(data.Ciphertext);
			return plaintext;
		}

		/// <summary>
		/// Asymmetrically encrypts the specified buffer using the provided public key.
		/// </summary>
		/// <param name="encryptionPublicKey">The public key used to encrypt the buffer.</param>
		/// <param name="data">The buffer to encrypt.</param>
		/// <returns>
		/// The ciphertext.
		/// </returns>
		public override byte[] Encrypt(byte[] encryptionPublicKey, byte[] data) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(encryptionPublicKey);
				return rsa.Encrypt(data, true);
			}
		}

		/// <summary>
		/// Asymmetrically decrypts the specified buffer using the provided private key.
		/// </summary>
		/// <param name="decryptionPrivateKey">The private key used to decrypt the buffer.</param>
		/// <param name="data">The buffer to decrypt.</param>
		/// <returns>
		/// The plaintext.
		/// </returns>
		public override byte[] Decrypt(byte[] decryptionPrivateKey, byte[] data) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(decryptionPrivateKey);
				return rsa.Decrypt(data, true);
			}
		}

		/// <summary>
		/// Computes the hash of the specified buffer.
		/// </summary>
		/// <param name="data">The data to hash.</param>
		/// <param name="hashAlgorithmName">Name of the hash algorithm.</param>
		/// <returns>
		/// The computed hash.
		/// </returns>
		public override byte[] Hash(byte[] data, string hashAlgorithmName) {
			return DigestUtilities.CalculateDigest(hashAlgorithmName, data);
		}

		/// <summary>
		/// Generates a key pair for asymmetric cryptography.
		/// </summary>
		/// <param name="keyPair">Receives the serialized key pair (includes private key).</param>
		/// <param name="publicKey">Receives the public key.</param>
		public override void GenerateSigningKeyPair(out byte[] keyPair, out byte[] publicKey) {
			using (var rsa = new RSACryptoServiceProvider(this.SignatureAsymmetricKeySize)) {
				keyPair = rsa.ExportCspBlob(true);
				publicKey = rsa.ExportCspBlob(false);
			}
		}

		/// <summary>
		/// Generates a key pair for asymmetric cryptography.
		/// </summary>
		/// <param name="keyPair">Receives the serialized key pair (includes private key).</param>
		/// <param name="publicKey">Receives the public key.</param>
		public override void GenerateEncryptionKeyPair(out byte[] keyPair, out byte[] publicKey) {
			using (var rsa = new RSACryptoServiceProvider(this.EncryptionAsymmetricKeySize)) {
				keyPair = rsa.ExportCspBlob(true);
				publicKey = rsa.ExportCspBlob(false);
			}
		}

		/// <summary>
		/// Gets the block cipher.
		/// </summary>
		/// <returns>An instance of a buffered, padded cipher.</returns>
		protected virtual IBufferedCipher GetCipher() {
			return
				CipherUtilities.GetCipher(
					string.Format(
						CultureInfo.InvariantCulture,
						"{0}/{1}/{2}",
						this.SymmetricEncryptionConfiguration.AlgorithmName,
						this.SymmetricEncryptionConfiguration.BlockMode,
						this.SymmetricEncryptionConfiguration.Padding));
		}

		/// <summary>
		/// Gets the hash algorithm to use.
		/// </summary>
		/// <param name="hashAlgorithm">The hash algorithm used to hash the data.</param>
		/// <returns>The hash algorithm.</returns>
		/// <exception cref="System.NotSupportedException">Thrown when the hash algorithm is not recognized or supported.</exception>
		protected virtual HashAlgorithm GetHashAlgorithm(string hashAlgorithm) {
			switch (hashAlgorithm) {
				case "SHA1":
					return new SHA1Managed();
				case "SHA256":
					return new SHA256Managed();
				default:
					throw new NotSupportedException();
			}
		}
	}
}
