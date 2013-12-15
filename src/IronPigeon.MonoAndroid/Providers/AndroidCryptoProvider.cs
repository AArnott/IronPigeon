namespace IronPigeon.MonoAndroid.Providers {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
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
			get { throw new NotImplementedException(); }
		}

		/// <inheritdoc/>
		public override void FillCryptoRandomBuffer(byte[] buffer) {
			var sr = SecureRandom.GetInstance("SHA1PRNG");
			sr.NextBytes(buffer);
		}

		/// <inheritdoc/>
		public override byte[] DeriveKeyFromPassword(string password, byte[] salt, int iterations, int keySizeInBytes) {
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override Task<byte[]> ComputeAuthenticationCodeAsync(Stream data, byte[] key, string hashAlgorithmName, CancellationToken cancellationToken) {
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override byte[] Sign(byte[] data, byte[] signingPrivateKey) {
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override byte[] SignHash(byte[] hash, byte[] signingPrivateKey, string hashAlgorithmName) {
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override bool VerifySignature(byte[] signingPublicKey, byte[] data, byte[] signature, string hashAlgorithm) {
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override bool VerifyHash(byte[] signingPublicKey, byte[] hash, byte[] signature, string hashAlgorithm) {
			throw new NotImplementedException();
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
			cipher.Init(CipherMode.EncryptMode, keySpec);

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
			cipher.Init(CipherMode.DecryptMode, keySpec);

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
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override byte[] Decrypt(byte[] decryptionPrivateKey, byte[] data) {
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override byte[] Hash(byte[] data, string hashAlgorithmName) {
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override Task<byte[]> HashAsync(Stream source, string hashAlgorithmName, CancellationToken cancellationToken) {
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override void GenerateSigningKeyPair(out byte[] keyPair, out byte[] publicKey) {
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override void GenerateEncryptionKeyPair(out byte[] keyPair, out byte[] publicKey) {
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override void BeginNegotiateSharedSecret(out byte[] privateKey, out byte[] publicKey) {
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override void RespondNegotiateSharedSecret(byte[] remotePublicKey, out byte[] ownPublicKey, out byte[] sharedSecret) {
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override void EndNegotiateSharedSecret(byte[] ownPrivateKey, byte[] remotePublicKey, out byte[] sharedSecret) {
			throw new NotImplementedException();
		}
	}
}