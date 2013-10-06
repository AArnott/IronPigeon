namespace IronPigeon.Providers {
	using System;
	using System.Collections.Generic;
	using System.Composition;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using Validation;
	using Windows.Security.Cryptography;
	using Windows.Security.Cryptography.Core;
	using Windows.Storage.Streams;

	/// <summary>
	/// A WinRT implementation of cryptography.
	/// </summary>
	[Export(typeof(ICryptoProvider))]
	[Shared]
	public class WinRTCryptoProvider : CryptoProviderBase {
		/// <summary>
		/// The asymmetric encryption algorithm provider to use.
		/// </summary>
		protected static readonly AsymmetricKeyAlgorithmProvider EncryptionProvider = AsymmetricKeyAlgorithmProvider.OpenAlgorithm(AsymmetricAlgorithmNames.RsaOaepSha1);

		/// <summary>
		/// The symmetric encryption algorithm provider to use.
		/// </summary>
		protected static readonly SymmetricKeyAlgorithmProvider SymmetricAlgorithm = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesCbcPkcs7);

		/// <summary>
		/// Gets the length (in bits) of the symmetric encryption cipher block.
		/// </summary>
		public override int SymmetricEncryptionBlockSize {
			get { return (int)SymmetricAlgorithm.BlockLength * 8; }
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
			var signer = this.GetSignatureProvider(this.AsymmetricHashAlgorithmName);
			var key = signer.ImportKeyPair(signingPrivateKey.ToBuffer());
			var signatureBuffer = CryptographicEngine.Sign(key, data.ToBuffer());
			return signatureBuffer.ToArray();
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
			var signer = this.GetSignatureProvider(hashAlgorithm);
			var key = signer.ImportPublicKey(signingPublicKey.ToBuffer(), CryptographicPublicKeyBlobType.Capi1PublicKey);
			return CryptographicEngine.VerifySignature(key, data.ToBuffer(), signature.ToBuffer());
		}

		/// <summary>
		/// Symmetrically encrypts the specified buffer using a randomly generated key.
		/// </summary>
		/// <param name="data">The data to encrypt.</param>
		/// <param name="key">The key used to encrypt the data. May be <c>null</c> to automatically generate a cryptographically strong random key.</param>
		/// <param name="iv">The initialization vector to use when encrypting the first block. May be <c>null</c> to automatically generate one.</param>
		/// <returns>
		/// The result of the encryption.
		/// </returns>
		public override SymmetricEncryptionResult Encrypt(byte[] data, byte[] key, byte[] iv) {
			Requires.NotNull(data, "data");

			IBuffer plainTextBuffer = CryptographicBuffer.CreateFromByteArray(data);
			IBuffer symmetricKeyMaterial = key != null
				? CryptographicBuffer.CreateFromByteArray(key)
				: CryptographicBuffer.GenerateRandom((uint)this.SymmetricEncryptionKeySize / 8);
			var symmetricKey = SymmetricAlgorithm.CreateSymmetricKey(symmetricKeyMaterial);
			IBuffer ivBuffer = iv != null
				? CryptographicBuffer.CreateFromByteArray(iv)
				: CryptographicBuffer.GenerateRandom(SymmetricAlgorithm.BlockLength);

			var cipherTextBuffer = CryptographicEngine.Encrypt(symmetricKey, plainTextBuffer, ivBuffer);
			return new SymmetricEncryptionResult(
				symmetricKeyMaterial.ToArray(),
				ivBuffer.ToArray(),
				cipherTextBuffer.ToArray());
		}

		/// <summary>
		/// Symmetrically decrypts a buffer using the specified key.
		/// </summary>
		/// <param name="data">The encrypted data and the key and IV used to encrypt it.</param>
		/// <returns>
		/// The decrypted buffer.
		/// </returns>
		public override byte[] Decrypt(SymmetricEncryptionResult data) {
			var symmetricKey = SymmetricAlgorithm.CreateSymmetricKey(data.Key.ToBuffer());
			return CryptographicEngine.Decrypt(symmetricKey, data.Ciphertext.ToBuffer(), data.IV.ToBuffer()).ToArray();
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
			var key = EncryptionProvider.ImportPublicKey(encryptionPublicKey.ToBuffer(), CryptographicPublicKeyBlobType.Capi1PublicKey);
			return CryptographicEngine.Encrypt(key, data.ToBuffer(), null).ToArray();
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
			var key = EncryptionProvider.ImportKeyPair(decryptionPrivateKey.ToBuffer());
			return CryptographicEngine.Decrypt(key, data.ToBuffer(), null).ToArray();
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
			var hashAlgorithm = HashAlgorithmProvider.OpenAlgorithm(hashAlgorithmName);
			var hash = hashAlgorithm.HashData(data.ToBuffer()).ToArray();
			return hash;
		}

		/// <summary>
		/// Generates a key pair for asymmetric cryptography.
		/// </summary>
		/// <param name="keyPair">Receives the serialized key pair (includes private key).</param>
		/// <param name="publicKey">Receives the public key.</param>
		public override void GenerateSigningKeyPair(out byte[] keyPair, out byte[] publicKey) {
			var signer = this.GetSignatureProvider(this.AsymmetricHashAlgorithmName);
			var key = signer.CreateKeyPair((uint)this.SignatureAsymmetricKeySize);
			keyPair = key.Export().ToArray();
			publicKey = key.ExportPublicKey(CryptographicPublicKeyBlobType.Capi1PublicKey).ToArray();
		}

		/// <summary>
		/// Generates a key pair for asymmetric cryptography.
		/// </summary>
		/// <param name="keyPair">Receives the serialized key pair (includes private key).</param>
		/// <param name="publicKey">Receives the public key.</param>
		public override void GenerateEncryptionKeyPair(out byte[] keyPair, out byte[] publicKey) {
			var key = EncryptionProvider.CreateKeyPair((uint)this.EncryptionAsymmetricKeySize);
			keyPair = key.Export().ToArray();
			publicKey = key.ExportPublicKey(CryptographicPublicKeyBlobType.Capi1PublicKey).ToArray();
		}

		/// <summary>
		/// Gets the signature provider.
		/// </summary>
		/// <param name="hashAlgorithm">The hash algorithm to use.</param>
		/// <returns>The asymmetric key provider.</returns>
		/// <exception cref="System.NotSupportedException">Thrown if the arguments are not supported.</exception>
		protected virtual AsymmetricKeyAlgorithmProvider GetSignatureProvider(string hashAlgorithm) {
			switch (hashAlgorithm) {
				case "SHA1":
					return AsymmetricKeyAlgorithmProvider.OpenAlgorithm(AsymmetricAlgorithmNames.RsaSignPkcs1Sha1);
				case "SHA256":
					return AsymmetricKeyAlgorithmProvider.OpenAlgorithm(AsymmetricAlgorithmNames.RsaSignPkcs1Sha256);
				case "SHA384":
					return AsymmetricKeyAlgorithmProvider.OpenAlgorithm(AsymmetricAlgorithmNames.RsaSignPkcs1Sha384);
				case "SHA512":
					return AsymmetricKeyAlgorithmProvider.OpenAlgorithm(AsymmetricAlgorithmNames.RsaSignPkcs1Sha512);
				default:
					throw new NotSupportedException();
			}
		}
	}
}
