namespace IronPigeon.MonoTouch.Providers {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	////using MonoTouch.Foundation;
	////using MonoTouch.UIKit;

	/// <summary>
	/// An Apple iOS implementation of <see cref="ICryptoProvider"/>.
	/// </summary>
	public class AppleCryptoProvider : CryptoProviderBase {
		/// <inheritdoc/>
		public override int SymmetricEncryptionBlockSize {
			get { throw new NotImplementedException(); }
		}

		/// <inheritdoc/>
		public override void FillCryptoRandomBuffer(byte[] buffer) {
			throw new NotImplementedException();
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
		public override Task<SymmetricEncryptionVariables> EncryptAsync(Stream plaintext, Stream ciphertext, SymmetricEncryptionVariables encryptionVariables, CancellationToken cancellationToken) {
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override Task DecryptAsync(Stream ciphertext, Stream plaintext, SymmetricEncryptionVariables encryptionVariables, CancellationToken cancellationToken) {
			throw new NotImplementedException();
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