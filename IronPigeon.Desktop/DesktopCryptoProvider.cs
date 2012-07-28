namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using System.Threading.Tasks;

	public class DesktopCryptoProvider : CryptoProviderBase {
		public override byte[] Sign(byte[] data, byte[] signingPrivateKey) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(signingPrivateKey);
				return rsa.SignData(data, HashAlgorithmName);
			}
		}

		public override bool VerifySignature(byte[] signingPublicKey, byte[] data, byte[] signature) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(signingPublicKey);
				return rsa.VerifyData(data, HashAlgorithmName, signature);
			}
		}

		public override SymmetricEncryptionResult Encrypt(byte[] data) {
			using (var alg = SymmetricAlgorithm.Create()) {
				alg.KeySize = BlobSymmetricKeySize;
				using (var encryptor = alg.CreateEncryptor()) {
					using (var memoryStream = new MemoryStream()) {
						using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write)) {
							cryptoStream.Write(data, 0, data.Length);
							cryptoStream.FlushFinalBlock();
							return new SymmetricEncryptionResult(alg.Key, alg.IV, memoryStream.ToArray());
						}
					}
				}
			}
		}

		public override byte[] Decrypt(SymmetricEncryptionResult data) {
			using (var alg = SymmetricAlgorithm.Create()) {
				using (var decryptor = alg.CreateDecryptor(data.Key, data.IV)) {
					using (var plaintextStream = new MemoryStream()) {
						using (var cryptoStream = new CryptoStream(plaintextStream, decryptor, CryptoStreamMode.Write)) {
							cryptoStream.Write(data.Ciphertext, 0, data.Ciphertext.Length);
						}

						return plaintextStream.ToArray();
					}
				}
			}
		}

		public override byte[] Encrypt(byte[] encryptionPublicKey, byte[] data) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(encryptionPublicKey);
				return rsa.Encrypt(data, true);
			}
		}

		public override byte[] Decrypt(byte[] decryptionPrivateKey, byte[] data) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(decryptionPrivateKey);
				return rsa.Decrypt(data, true);
			}
		}

		public override byte[] Hash(byte[] data) {
			using (var hasher = HashAlgorithm.Create(HashAlgorithmName)) {
				return hasher.ComputeHash(data);
			}
		}

		public override void GenerateSigningKeyPair(out byte[] keyPair, out byte[] publicKey) {
			using (var rsa = new RSACryptoServiceProvider(SignatureAsymmetricKeySize)) {
				keyPair = rsa.ExportCspBlob(true);
				publicKey = rsa.ExportCspBlob(false);
			}
		}

		public override void GenerateEncryptionKeyPair(out byte[] keyPair, out byte[] publicKey) {
			using (var rsa = new RSACryptoServiceProvider(EncryptionAsymmetricKeySize)) {
				keyPair = rsa.ExportCspBlob(true);
				publicKey = rsa.ExportCspBlob(false);
			}
		}
	}
}
