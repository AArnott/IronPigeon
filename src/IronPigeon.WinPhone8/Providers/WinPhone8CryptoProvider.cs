namespace IronPigeon.WinPhone8.Providers {
	using System;
	using System.Collections.Generic;
	using System.Composition;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using System.Threading.Tasks;

	using Org.BouncyCastle.Asn1;

	using Validation;

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
			throw new NotImplementedException();
			//using (var alg = SymmetricAlgorithm.Create(this.SymmetricAlgorithmName)) {
			//	alg.KeySize = this.BlobSymmetricKeySize;
			//	using (var encryptor = alg.CreateEncryptor()) {
			//		using (var memoryStream = new MemoryStream()) {
			//			using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write)) {
			//				cryptoStream.Write(data, 0, data.Length);
			//				cryptoStream.FlushFinalBlock();
			//				return new SymmetricEncryptionResult(alg.Key, alg.IV, memoryStream.ToArray());
			//			}
			//		}
			//	}
			//}
		}

		/// <summary>
		/// Symmetrically decrypts a buffer using the specified key.
		/// </summary>
		/// <param name="data">The encrypted data and the key and IV used to encrypt it.</param>
		/// <returns>
		/// The decrypted buffer.
		/// </returns>
		public override byte[] Decrypt(SymmetricEncryptionResult data) {
			throw new NotImplementedException();
			//using (var alg = SymmetricAlgorithm.Create(this.SymmetricAlgorithmName)) {
			//	using (var decryptor = alg.CreateDecryptor(data.Key, data.IV)) {
			//		using (var plaintextStream = new MemoryStream()) {
			//			using (var cryptoStream = new CryptoStream(plaintextStream, decryptor, CryptoStreamMode.Write)) {
			//				cryptoStream.Write(data.Ciphertext, 0, data.Ciphertext.Length);
			//			}

			//			return plaintextStream.ToArray();
			//		}
			//	}
			//}
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
			return Org.BouncyCastle.Security.DigestUtilities.CalculateDigest(this.HashAlgorithmName, data);
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
	}
}
