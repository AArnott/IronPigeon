namespace IronPigeon.WinRT {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using Windows.Security.Cryptography;
	using Windows.Security.Cryptography.Core;
	using Windows.Storage.Streams;

	public class WinRTCryptoProvider : CryptoProviderBase {
		protected static readonly AsymmetricKeyAlgorithmProvider EncryptionProvider = AsymmetricKeyAlgorithmProvider.OpenAlgorithm(AsymmetricAlgorithmNames.RsaOaepSha1);

		protected static readonly AsymmetricKeyAlgorithmProvider SignatureProvider = AsymmetricKeyAlgorithmProvider.OpenAlgorithm(AsymmetricAlgorithmNames.RsaSignPkcs1Sha1);

		protected static readonly HashAlgorithmProvider HashProvider = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha1);

		protected static readonly SymmetricKeyAlgorithmProvider SymmetricAlgorithm = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesCbcPkcs7);

		public override byte[] Sign(byte[] data, byte[] signingPrivateKey) {
			var key = SignatureProvider.ImportKeyPair(signingPrivateKey.ToBuffer());
			var signatureBuffer = CryptographicEngine.Sign(key, data.ToBuffer());
			return signatureBuffer.ToArray();
		}

		public override bool VerifySignature(byte[] signingPublicKey, byte[] data, byte[] signature) {
			var key = SignatureProvider.ImportPublicKey(signingPublicKey.ToBuffer());
			return CryptographicEngine.VerifySignature(key, data.ToBuffer(), signature.ToBuffer());
		}

		public override SymmetricEncryptionResult Encrypt(byte[] data) {
			IBuffer plainTextBuffer = CryptographicBuffer.CreateFromByteArray(data);
			IBuffer symmetricKeyMaterial = CryptographicBuffer.GenerateRandom((uint)BlobSymmetricKeySize / 8);
			var symmetricKey = SymmetricAlgorithm.CreateSymmetricKey(symmetricKeyMaterial);
			IBuffer ivBuffer = CryptographicBuffer.GenerateRandom(SymmetricAlgorithm.BlockLength);

			var cipherTextBuffer = CryptographicEngine.Encrypt(symmetricKey, plainTextBuffer, ivBuffer);
			return new SymmetricEncryptionResult(
				symmetricKeyMaterial.ToArray(),
				ivBuffer.ToArray(),
				cipherTextBuffer.ToArray());
		}

		public override byte[] Decrypt(SymmetricEncryptionResult data) {
			var symmetricKey = SymmetricAlgorithm.CreateSymmetricKey(data.Key.ToBuffer());
			return CryptographicEngine.Decrypt(symmetricKey, data.Ciphertext.ToBuffer(), data.IV.ToBuffer()).ToArray();
		}

		public override byte[] Encrypt(byte[] encryptionPublicKey, byte[] data) {
			var key = EncryptionProvider.ImportPublicKey(encryptionPublicKey.ToBuffer());
			return CryptographicEngine.Encrypt(key, data.ToBuffer(), null).ToArray();
		}

		public override byte[] Decrypt(byte[] decryptionPrivateKey, byte[] data) {
			var key = EncryptionProvider.ImportKeyPair(decryptionPrivateKey.ToBuffer());
			return CryptographicEngine.Decrypt(key, data.ToBuffer(), null).ToArray();
		}

		public override byte[] Hash(byte[] data) {
			return HashProvider.HashData(data.ToBuffer()).ToArray();
		}

		public override void GenerateSigningKeyPair(out byte[] keyPair, out byte[] publicKey) {
			var key = SignatureProvider.CreateKeyPair((uint)this.SignatureAsymmetricKeySize);
			keyPair = key.Export().ToArray();
			publicKey = key.ExportPublicKey(CryptographicPublicKeyBlobType.Capi1PublicKey).ToArray();
		}

		public override void GenerateEncryptionKeyPair(out byte[] keyPair, out byte[] publicKey) {
			var key = EncryptionProvider.CreateKeyPair((uint)this.EncryptionAsymmetricKeySize);
			keyPair = key.Export().ToArray();
			publicKey = key.ExportPublicKey(CryptographicPublicKeyBlobType.Capi1PublicKey).ToArray();
		}
	}
}
