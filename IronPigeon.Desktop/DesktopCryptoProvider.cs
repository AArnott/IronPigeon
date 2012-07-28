namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using System.Threading.Tasks;

	public class DesktopCryptoProvider : ICryptoProvider {
		private string hashAlgorithmName = Recommended.HashAlgorithmName;
		private int asymmetricKeySize = Recommended.AsymmetricKeySize;
		private int symmetricKeySize = Recommended.SymmetricKeySize;

		public string HashAlgorithmName {
			get { return this.hashAlgorithmName; }
			set { this.hashAlgorithmName = value; }
		}

		public int AsymmetricKeySize {
			get { return this.asymmetricKeySize; }
			set { this.asymmetricKeySize = value; }
		}

		public int SymmetricKeySize {
			get { return this.symmetricKeySize; }
			set { this.symmetricKeySize = value; }
		}
	
		public byte[] Sign(byte[] data, byte[] signingPrivateKey) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(signingPrivateKey);
				return rsa.SignData(data, HashAlgorithmName);
			}
		}

		public bool VerifySignature(byte[] signingPublicKey, byte[] data, byte[] signature) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(signingPublicKey);
				return rsa.VerifyData(data, HashAlgorithmName, signature);
			}
		}

		public SymmetricEncryptionResult Encrypt(byte[] data) {
			using (var alg = SymmetricAlgorithm.Create()) {
				alg.KeySize = SymmetricKeySize;
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

		public byte[] Decrypt(SymmetricEncryptionResult data) {
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

		public byte[] Encrypt(byte[] encryptionPublicKey, byte[] data) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(encryptionPublicKey);
				return rsa.Encrypt(data, true);
			}
		}

		public byte[] Decrypt(byte[] decryptionPrivateKey, byte[] data) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(decryptionPrivateKey);
				return rsa.Decrypt(data, true);
			}
		}

		public byte[] Hash(byte[] data) {
			using (var hasher = HashAlgorithm.Create(HashAlgorithmName)) {
				return hasher.ComputeHash(data);
			}
		}

		public void GenerateSigningKeyPair(out byte[] keyPair, out byte[] publicKey) {
			using (var rsa = new RSACryptoServiceProvider(AsymmetricKeySize)) {
				keyPair = rsa.ExportCspBlob(true);
				publicKey = rsa.ExportCspBlob(false);
			}
		}

		public void GenerateEncryptionKeyPair(out byte[] keyPair, out byte[] publicKey) {
			using (var rsa = new RSACryptoServiceProvider(AsymmetricKeySize)) {
				keyPair = rsa.ExportCspBlob(true);
				publicKey = rsa.ExportCspBlob(false);
			}
		}
	}
}
