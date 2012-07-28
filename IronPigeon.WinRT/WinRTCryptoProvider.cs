namespace IronPigeon.WinRT {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using Windows.Security.Cryptography;
	using Windows.Security.Cryptography.Core;
	using Windows.Storage.Streams;

	public class WinRTCryptoProvider : ICryptoProvider {
		protected const string HashAlgorithmName = Recommended.HashAlgorithmName;

		protected const int AsymmetricKeySize = Recommended.AsymmetricKeySize;

		protected const int SymmetricKeySize = Recommended.SymmetricKeySize;

		protected static readonly AsymmetricKeyAlgorithmProvider EncryptionProvider = AsymmetricKeyAlgorithmProvider.OpenAlgorithm(AsymmetricAlgorithmNames.RsaOaepSha1);

		protected static readonly AsymmetricKeyAlgorithmProvider SignatureProvider = AsymmetricKeyAlgorithmProvider.OpenAlgorithm(AsymmetricAlgorithmNames.RsaSignPkcs1Sha1);

		protected static readonly HashAlgorithmProvider HashProvider = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha1);

		protected static readonly SymmetricKeyAlgorithmProvider SymmetricAlgorithm = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesCbcPkcs7);

		public byte[] Sign(byte[] data, byte[] signingPrivateKey) {
			var key = SignatureProvider.ImportKeyPair(signingPrivateKey.ToBuffer());
			var signatureBuffer = CryptographicEngine.Sign(key, data.ToBuffer());
			return signatureBuffer.ToArray();
		}

		public bool VerifySignature(byte[] signingPublicKey, byte[] data, byte[] signature) {
			var key = SignatureProvider.ImportPublicKey(signingPublicKey.ToBuffer());
			return CryptographicEngine.VerifySignature(key, data.ToBuffer(), signature.ToBuffer());
		}

		public SymmetricEncryptionResult Encrypt(byte[] data) {
			IBuffer plainTextBuffer = CryptographicBuffer.CreateFromByteArray(data);
			IBuffer symmetricKeyMaterial = CryptographicBuffer.GenerateRandom(SymmetricKeySize / 8);
			var symmetricKey = SymmetricAlgorithm.CreateSymmetricKey(symmetricKeyMaterial);
			IBuffer ivBuffer = CryptographicBuffer.GenerateRandom(SymmetricAlgorithm.BlockLength);

			var cipherTextBuffer = CryptographicEngine.Encrypt(symmetricKey, plainTextBuffer, ivBuffer);
			return new SymmetricEncryptionResult(
				symmetricKeyMaterial.ToArray(),
				ivBuffer.ToArray(),
				cipherTextBuffer.ToArray());
		}

		public byte[] Decrypt(SymmetricEncryptionResult data) {
			var symmetricKey = SymmetricAlgorithm.CreateSymmetricKey(data.Key.ToBuffer());
			return CryptographicEngine.Decrypt(symmetricKey, data.Ciphertext.ToBuffer(), data.IV.ToBuffer()).ToArray();
		}

		public byte[] Encrypt(byte[] encryptionPublicKey, byte[] data) {
			var key = EncryptionProvider.ImportPublicKey(encryptionPublicKey.ToBuffer());
			return CryptographicEngine.Encrypt(key, data.ToBuffer(), null).ToArray();
		}

		public byte[] Decrypt(byte[] decryptionPrivateKey, byte[] data) {
			var key = EncryptionProvider.ImportKeyPair(decryptionPrivateKey.ToBuffer());
			return CryptographicEngine.Decrypt(key, data.ToBuffer(), null).ToArray();
		}

		public byte[] Hash(byte[] data) {
			return HashProvider.HashData(data.ToBuffer()).ToArray();
		}

		public void GenerateSigningKeyPair(out byte[] keyPair, out byte[] publicKey) {
			var key = SignatureProvider.CreateKeyPair(AsymmetricKeySize);
			keyPair = key.Export().ToArray();
			publicKey = key.ExportPublicKey(CryptographicPublicKeyBlobType.Capi1PublicKey).ToArray();
		}

		public void GenerateEncryptionKeyPair(out byte[] keyPair, out byte[] publicKey) {
			var key = EncryptionProvider.CreateKeyPair(AsymmetricKeySize);
			keyPair = key.Export().ToArray();
			publicKey = key.ExportPublicKey(CryptographicPublicKeyBlobType.Capi1PublicKey).ToArray();
		}
	}
}
