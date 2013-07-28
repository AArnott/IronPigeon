namespace IronPigeon.WinPhone8.Providers {
	using System;
	using System.Collections.Generic;
	using System.Composition;
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
				return rsa.SignData(data, this.HashAlgorithmName);
			}
		}

		/// <summary>
		/// Verifies the asymmetric signature of some data blob.
		/// </summary>
		/// <param name="signingPublicKey">The public key used to verify the signature.</param>
		/// <param name="data">The data that was signed.</param>
		/// <param name="signature">The signature.</param>
		/// <returns>
		/// A value indicating whether the signature is valid.
		/// </returns>
		public override bool VerifySignature(byte[] signingPublicKey, byte[] data, byte[] signature) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(signingPublicKey);
				return rsa.VerifyData(data, this.HashAlgorithmName, signature);
			}
		}

		/// <summary>
		/// Symmetrically encrypts the specified buffer using a randomly generated key.
		/// </summary>
		/// <param name="data">The data to encrypt.</param>
		/// <returns>
		/// The result of the encryption.
		/// </returns>
		public override SymmetricEncryptionResult Encrypt(byte[] data) {
			var encryptor = this.GetCipher();

			var secureRandom = new SecureRandom();
			byte[] key = new byte[this.BlobSymmetricKeySize / 8];
			secureRandom.NextBytes(key);

			var random = new Random();
			byte[] iv = new byte[encryptor.GetBlockSize()];
			random.NextBytes(iv);

			var parameters = new ParametersWithIV(new KeyParameter(key), iv);
			encryptor.Init(true, parameters);
			byte[] ciphertext = encryptor.DoFinal(data);
			return new SymmetricEncryptionResult(key, iv, ciphertext);
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
		/// <returns>
		/// The computed hash.
		/// </returns>
		public override byte[] Hash(byte[] data) {
			return DigestUtilities.CalculateDigest(this.HashAlgorithmName, data);
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
		protected virtual IBufferedCipher GetCipher() {
			IBlockCipher cipher;
			switch (this.SymmetricAlgorithmName) {
				case "Rijndael":
					cipher = new RijndaelEngine();
					break;
				default:
					throw new NotSupportedException();
			}

			var cbcCipher = new CbcBlockCipher(cipher);
			var padding = new Pkcs7Padding();
			var result = new PaddedBufferedBlockCipher(cbcCipher, padding);
			return result;
		}
	}
}
