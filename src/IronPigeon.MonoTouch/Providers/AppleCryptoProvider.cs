namespace IronPigeon.Providers {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Validation;

	/// <summary>
	/// An Apple iOS implementation of <see cref="ICryptoProvider"/>.
	/// </summary>
	public class AppleCryptoProvider : CryptoProviderBase {
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
			var rng = new RNGCryptoServiceProvider();
			rng.GetBytes(buffer);
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

			using (var alg = SymmetricAlgorithm.Create(this.SymmetricEncryptionConfiguration.AlgorithmName)) {
				alg.Mode = (CipherMode)Enum.Parse(typeof(CipherMode), this.SymmetricEncryptionConfiguration.BlockMode);
				alg.Padding = (PaddingMode)Enum.Parse(typeof(PaddingMode), this.SymmetricEncryptionConfiguration.Padding);
				alg.KeySize = this.SymmetricEncryptionKeySize;

				if (encryptionVariables != null) {
					Requires.Argument(encryptionVariables.Key.Length == this.SymmetricEncryptionKeySize / 8, "key", "Incorrect length.");
					Requires.Argument(encryptionVariables.IV.Length == this.SymmetricEncryptionBlockSize / 8, "iv", "Incorrect length.");
					alg.Key = encryptionVariables.Key;
					alg.IV = encryptionVariables.IV;
				} else {
					encryptionVariables = new SymmetricEncryptionVariables(alg.Key, alg.IV);
				}

				using (var encryptor = alg.CreateEncryptor()) {
					var cryptoStream = new CryptoStream(ciphertext, encryptor, CryptoStreamMode.Write); // DON'T dispose this, or it disposes of the ciphertext stream.
					await plaintext.CopyToAsync(cryptoStream, alg.BlockSize, cancellationToken);
					cryptoStream.FlushFinalBlock();
					return encryptionVariables;
				}
			}
		}

		/// <inheritdoc/>
		public override async Task DecryptAsync(Stream ciphertext, Stream plaintext, SymmetricEncryptionVariables encryptionVariables, CancellationToken cancellationToken) {
			Requires.NotNull(ciphertext, "ciphertext");
			Requires.NotNull(plaintext, "plaintext");
			Requires.NotNull(encryptionVariables, "encryptionVariables");

			using (var alg = SymmetricAlgorithm.Create(this.SymmetricEncryptionConfiguration.AlgorithmName)) {
				alg.Mode = (CipherMode)Enum.Parse(typeof(CipherMode), this.SymmetricEncryptionConfiguration.BlockMode);
				alg.Padding = (PaddingMode)Enum.Parse(typeof(PaddingMode), this.SymmetricEncryptionConfiguration.Padding);
				using (var decryptor = alg.CreateDecryptor(encryptionVariables.Key, encryptionVariables.IV)) {
					var cryptoStream = new CryptoStream(plaintext, decryptor, CryptoStreamMode.Write); // don't dispose this or it disposes the target stream.
					await ciphertext.CopyToAsync(cryptoStream, 4096, cancellationToken);
					cryptoStream.FlushFinalBlock();
				}
			}
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