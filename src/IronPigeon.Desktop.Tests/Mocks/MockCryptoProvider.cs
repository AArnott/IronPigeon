namespace IronPigeon.Tests.Mocks {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using NUnit.Framework;

	internal class MockCryptoProvider : ICryptoProvider {
		internal const int KeyLengthInBytes = 5;

		#region ICryptoProvider Members

		public string SymmetricHashAlgorithmName {
			get { return "mock"; }
			set { throw new NotSupportedException(); }
		}

		public string AsymmetricHashAlgorithmName {
			get { return "mock"; }
			set { throw new NotSupportedException(); }
		}

		EncryptionConfiguration ICryptoProvider.SymmetricEncryptionConfiguration {
			get { return new EncryptionConfiguration("mock", "mock", "mock"); }
			set { throw new NotSupportedException(); }
		}

		public int SignatureAsymmetricKeySize {
			get { return KeyLengthInBytes; }
			set { throw new NotSupportedException(); }
		}

		public int EncryptionAsymmetricKeySize {
			get { return KeyLengthInBytes; }
			set { throw new NotSupportedException(); }
		}

		public int SymmetricEncryptionKeySize {
			get { return KeyLengthInBytes; }
			set { throw new NotSupportedException(); }
		}

		/// <summary>
		/// Gets the length of the symmetric encryption cipher block.
		/// </summary>
		public int SymmetricEncryptionBlockSize {
			get { return 5; }
		}

		public byte[] Sign(byte[] data, byte[] signingPrivateKey) {
			return data;
		}

		public bool VerifySignature(byte[] signingPublicKey, byte[] data, byte[] signature, string hashAlgorithm) {
			return true;
		}

		public SymmetricEncryptionResult Encrypt(byte[] data, SymmetricEncryptionVariables encryptionVariables) {
			var rng = new Random();

			byte[] key, iv;
			if (encryptionVariables != null) {
				key = encryptionVariables.Key;
				iv = encryptionVariables.IV;
			} else {
				key = new byte[KeyLengthInBytes];
				rng.NextBytes(key);

				iv = new byte[KeyLengthInBytes];
				rng.NextBytes(iv);
			}

			var ciphertext = new byte[key.Length + iv.Length + data.Length];
			Array.Copy(key, ciphertext, key.Length);
			Array.Copy(iv, 0, ciphertext, key.Length, iv.Length);
			Array.Copy(data, 0, ciphertext, key.Length + iv.Length, data.Length);
			return new SymmetricEncryptionResult(key, iv, ciphertext);
		}

		public Task<SymmetricEncryptionVariables> EncryptAsync(System.IO.Stream plaintext, System.IO.Stream ciphertext, SymmetricEncryptionVariables encryptionVariables = null) {
			throw new NotImplementedException();
		}

		public Task DecryptAsync(System.IO.Stream ciphertext, System.IO.Stream plaintext, SymmetricEncryptionVariables encryptionVariables) {
			throw new NotImplementedException();
		}

		public byte[] Decrypt(SymmetricEncryptionResult data) {
			for (int i = 0; i < data.Key.Length; i++) {
				Assert.That(data.Ciphertext[i], Is.EqualTo(data.Key[i]));
			}

			for (int i = 0; i < data.IV.Length; i++) {
				Assert.That(data.Ciphertext[data.Key.Length + i], Is.EqualTo(data.IV[i]));
			}

			var plaintext = new byte[data.Ciphertext.Length - data.Key.Length - data.IV.Length];
			Array.Copy(data.Ciphertext, data.Key.Length + data.IV.Length, plaintext, 0, plaintext.Length);
			return plaintext;
		}

		public byte[] Encrypt(byte[] encryptionPublicKey, byte[] data) {
			var buffer = new byte[encryptionPublicKey.Length + data.Length];
			encryptionPublicKey.CopyTo(buffer, 0);
			data.CopyTo(buffer, encryptionPublicKey.Length);
			return buffer;
		}

		public byte[] Decrypt(byte[] decryptionPrivateKey, byte[] data) {
			for (int i = 0; i < decryptionPrivateKey.Length; i++) {
				Assert.That((byte)(data[i] ^ 0xff), Is.EqualTo(decryptionPrivateKey[i]), "Data corruption detected.");
			}

			var buffer = new byte[data.Length - decryptionPrivateKey.Length];
			Array.Copy(data, decryptionPrivateKey.Length, buffer, 0, data.Length - decryptionPrivateKey.Length);
			return buffer;
		}

		public byte[] Hash(byte[] data, string hashAlgorithmName) {
			int hash = 22;
			for (int i = 0; i < data.Length; i++) {
				unchecked {
					hash += data[i];
				}
			}

			return BitConverter.GetBytes(hash);
		}

		public void GenerateSigningKeyPair(out byte[] keyPair, out byte[] publicKey) {
			GenerateKeyPair(out keyPair, out publicKey);
		}

		public void GenerateEncryptionKeyPair(out byte[] keyPair, out byte[] publicKey) {
			GenerateKeyPair(out keyPair, out publicKey);
		}

		#endregion

		private static void GenerateKeyPair(out byte[] privateKey, out byte[] publicKey) {
			var rng = new Random();
			privateKey = new byte[KeyLengthInBytes];
			rng.NextBytes(privateKey);
			publicKey = new byte[KeyLengthInBytes];
			for (int i = 0; i < KeyLengthInBytes; i++) {
				publicKey[i] = (byte)(privateKey[i] ^ 0xff);
			}
		}
	}
}
