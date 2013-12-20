namespace IronPigeon.Providers {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Android.App;
	using Android.Content;
	using Android.Media;
	using Android.OS;
	using Android.Runtime;
	using Android.Views;
	using Android.Widget;
	using Java.Security;
	using Javax.Crypto;
	using Javax.Crypto.Spec;
	using Validation;
	using Stream = System.IO.Stream;

	/// <summary>
	/// An Android implementation of <see cref="ICryptoProvider"/>.
	/// </summary>
	public class AndroidCryptoProvider : CryptoProviderBase {
		/// <inheritdoc/>
		public override int SymmetricEncryptionBlockSize {
			get {
				using (var alg = SymmetricAlgorithm.Create(this.SymmetricEncryptionConfiguration.AlgorithmName)) {
					return alg.BlockSize;
				}
			}
		}

		/// <inheritdoc/>
		public override void FillCryptoRandomBuffer(byte[] buffer) {
			var sr = SecureRandom.GetInstance("SHA1PRNG");
			sr.NextBytes(buffer);
		}

		/// <inheritdoc/>
		public override byte[] DeriveKeyFromPassword(string password, byte[] salt, int iterations, int keySizeInBytes) {
			Requires.NotNullOrEmpty(password, "password");
			Requires.NotNull(salt, "salt");
			Requires.Range(iterations > 0, "iterations");
			Requires.Range(keySizeInBytes > 0, "keySizeInBytes");

			var keyStrengthening = new Rfc2898DeriveBytes(password, salt, iterations);
			return keyStrengthening.GetBytes(keySizeInBytes);
		}

		/// <inheritdoc/>
		public override async Task<byte[]> ComputeAuthenticationCodeAsync(Stream data, byte[] key, string hashAlgorithmName, CancellationToken cancellationToken) {
			Requires.NotNull(data, "data");
			Requires.NotNull(key, "key");
			Requires.NotNullOrEmpty(hashAlgorithmName, "hashAlgorithmName");

			var hmac = this.GetHmacAlgorithm(hashAlgorithmName);
			hmac.Key = key;
			using (var cryptoStream = new CryptoStream(Stream.Null, hmac, CryptoStreamMode.Write)) {
				await data.CopyToAsync(cryptoStream, 4096, cancellationToken);
				cryptoStream.FlushFinalBlock();
			}

			return hmac.Hash;
		}

		/// <inheritdoc/>
		public override byte[] Sign(byte[] data, byte[] signingPrivateKey) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(signingPrivateKey);
				return rsa.SignData(data, this.AsymmetricHashAlgorithmName);
			}
		}

		/// <inheritdoc/>
		public override byte[] SignHash(byte[] hash, byte[] signingPrivateKey, string hashAlgorithmName) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(signingPrivateKey);
				return rsa.SignHash(hash, this.AsymmetricHashAlgorithmName);
			}
		}

		/// <inheritdoc/>
		public override bool VerifySignature(byte[] signingPublicKey, byte[] data, byte[] signature, string hashAlgorithm) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(signingPublicKey);
				return rsa.VerifyData(data, hashAlgorithm, signature);
			}
		}

		/// <inheritdoc/>
		public override bool VerifyHash(byte[] signingPublicKey, byte[] hash, byte[] signature, string hashAlgorithm) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(signingPublicKey);
				return rsa.VerifyHash(hash, hashAlgorithm, signature);
			}
		}

		/// <inheritdoc/>
		public override async Task<SymmetricEncryptionVariables> EncryptAsync(Stream plaintext, Stream ciphertext, SymmetricEncryptionVariables encryptionVariables, CancellationToken cancellationToken) {
			Requires.NotNull(plaintext, "plaintext");
			Requires.NotNull(ciphertext, "ciphertext");

			cancellationToken.ThrowIfCancellationRequested();
			if (encryptionVariables == null) {
				var sr = SecureRandom.GetInstance("SHA1PRNG");
				var iv = new byte[this.SymmetricEncryptionBlockSize];
				sr.NextBytes(iv);
				var keyGen = KeyGenerator.GetInstance("AES");
				keyGen.Init(this.SymmetricEncryptionKeySize * 8);
				ISecretKey key = keyGen.GenerateKey();
				encryptionVariables = new SymmetricEncryptionVariables(key.GetEncoded(), iv);
			} else {
				Requires.Argument(encryptionVariables.Key.Length == this.SymmetricEncryptionKeySize / 8, "key", "Incorrect length.");
				Requires.Argument(encryptionVariables.IV.Length == this.SymmetricEncryptionBlockSize / 8, "iv", "Incorrect length.");
			}

			var keySpec = new SecretKeySpec(encryptionVariables.Key, "AES");
			Cipher cipher = Cipher.GetInstance("AES");
			cipher.Init(Javax.Crypto.CipherMode.EncryptMode, keySpec);

			byte[] plainTextBuffer = new byte[this.SymmetricEncryptionBlockSize];
			byte[] cipherTextBuffer = new byte[this.SymmetricEncryptionBlockSize];
			int bytesRead, bytesWritten;
			do {
				cancellationToken.ThrowIfCancellationRequested();
				bytesRead = await plaintext.ReadAsync(plainTextBuffer, 0, plainTextBuffer.Length, cancellationToken);
				bytesWritten = cipher.Update(plainTextBuffer, 0, bytesRead, cipherTextBuffer, 0);
				await ciphertext.WriteAsync(cipherTextBuffer, 0, bytesWritten, cancellationToken);
			} while (bytesRead > 0);
			bytesWritten = cipher.DoFinal(cipherTextBuffer, 0);
			await ciphertext.WriteAsync(cipherTextBuffer, 0, bytesWritten);

			return encryptionVariables;
		}

		/// <inheritdoc/>
		public override async Task DecryptAsync(Stream ciphertext, Stream plaintext, SymmetricEncryptionVariables encryptionVariables, CancellationToken cancellationToken) {
			Requires.NotNull(ciphertext, "ciphertext");
			Requires.NotNull(plaintext, "plaintext");
			Requires.NotNull(encryptionVariables, "encryptionVariables");

			var keySpec = new SecretKeySpec(encryptionVariables.Key, "AES");
			Cipher cipher = Cipher.GetInstance("AES");
			cipher.Init(Javax.Crypto.CipherMode.DecryptMode, keySpec);

			byte[] plainTextBuffer = new byte[this.SymmetricEncryptionBlockSize];
			byte[] cipherTextBuffer = new byte[this.SymmetricEncryptionBlockSize];
			int bytesRead, bytesWritten;
			do {
				cancellationToken.ThrowIfCancellationRequested();
				bytesRead = await ciphertext.ReadAsync(cipherTextBuffer, 0, cipherTextBuffer.Length, cancellationToken);
				bytesWritten = cipher.Update(cipherTextBuffer, 0, bytesRead, plainTextBuffer, 0);
				await plaintext.WriteAsync(plainTextBuffer, 0, bytesWritten, cancellationToken);
			} while (bytesRead > 0);
			bytesWritten = cipher.DoFinal(plainTextBuffer, 0);
			await plaintext.WriteAsync(plainTextBuffer, 0, bytesWritten);
		}

		/// <inheritdoc/>
		public override byte[] Encrypt(byte[] encryptionPublicKey, byte[] data) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(encryptionPublicKey);
				return rsa.Encrypt(data, true);
			}
		}

		/// <inheritdoc/>
		public override byte[] Decrypt(byte[] decryptionPrivateKey, byte[] data) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(decryptionPrivateKey);
				return rsa.Decrypt(data, true);
			}
		}

		/// <inheritdoc/>
		public override byte[] Hash(byte[] data, string hashAlgorithmName) {
			using (var hasher = HashAlgorithm.Create(hashAlgorithmName)) {
				if (hasher == null) {
					throw new NotSupportedException();
				}

				return hasher.ComputeHash(data);
			}
		}

		/// <inheritdoc/>
		public override async Task<byte[]> HashAsync(Stream source, string hashAlgorithmName, CancellationToken cancellationToken) {
			using (var hasher = HashAlgorithm.Create(hashAlgorithmName)) {
				if (hasher == null) {
					throw new NotSupportedException();
				}

				using (var cryptoStream = new CryptoStream(Stream.Null, hasher, CryptoStreamMode.Write)) {
					await source.CopyToAsync(cryptoStream, 4096, cancellationToken);
					cryptoStream.FlushFinalBlock();
				}

				return hasher.Hash;
			}
		}

		/// <inheritdoc/>
		public override void GenerateSigningKeyPair(out byte[] keyPair, out byte[] publicKey) {
			using (var rsa = new RSACryptoServiceProvider(this.SignatureAsymmetricKeySize)) {
				keyPair = rsa.ExportCspBlob(true);
				publicKey = rsa.ExportCspBlob(false);
			}
		}

		/// <inheritdoc/>
		public override void GenerateEncryptionKeyPair(out byte[] keyPair, out byte[] publicKey) {
			using (var rsa = new RSACryptoServiceProvider(this.EncryptionAsymmetricKeySize)) {
				keyPair = rsa.ExportCspBlob(true);
				publicKey = rsa.ExportCspBlob(false);
			}
		}

		/// <summary>
		/// Gets the HMAC algorithm to use.
		/// </summary>
		/// <param name="hashAlgorithm">The hash algorithm used to hash the data (SHA1, SHA256).</param>
		/// <returns>The hash algorithm.</returns>
		/// <exception cref="System.NotSupportedException">Thrown when the hash algorithm is not recognized or supported.</exception>
		protected virtual HMAC GetHmacAlgorithm(string hashAlgorithm) {
			switch (hashAlgorithm) {
				case "SHA1":
					return new HMACSHA1();
				case "SHA256":
					return new HMACSHA256();
				default:
					throw new NotSupportedException();
			}
		}
	}
}