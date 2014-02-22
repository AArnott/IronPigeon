namespace IronPigeon.Providers {
	using System;
	using System.Collections.Generic;
	using System.Composition;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Org.BouncyCastle.Asn1;
	using Org.BouncyCastle.Asn1.X509;
	using Org.BouncyCastle.Crypto;
	using Org.BouncyCastle.Crypto.Engines;
	using Org.BouncyCastle.Crypto.Modes;
	using Org.BouncyCastle.Crypto.Paddings;
	using Org.BouncyCastle.Crypto.Parameters;
	using Org.BouncyCastle.Security;
	using Validation;

	/// <summary>
	/// The Windows Phone 8 implementation of the IronPigeon crypto provider.
	/// </summary>
	[Export(typeof(ICryptoProvider))]
	[Shared]
	public class WinPhone8CryptoProvider : CryptoProviderBase {
		/// <summary>
		/// Initializes a new instance of the <see cref="WinPhone8CryptoProvider" /> class
		/// with the default security level.
		/// </summary>
		public WinPhone8CryptoProvider()
			: this(SecurityLevel.Maximum) {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WinPhone8CryptoProvider" /> class.
		/// </summary>
		/// <param name="securityLevel">The security level to apply to this instance.  The default is <see cref="SecurityLevel.Maximum"/>.</param>
		public WinPhone8CryptoProvider(SecurityLevel securityLevel) {
			Requires.NotNull(securityLevel, "securityLevel");
			securityLevel.Apply(this);
		}

		/// <summary>
		/// Gets the length (in bits) of the symmetric encryption cipher block.
		/// </summary>
		public override int SymmetricEncryptionBlockSize {
			get { return this.GetCipher().GetBlockSize() * 8; }
		}

		/// <summary>
		/// Fills the specified buffer with cryptographically strong random generated data.
		/// </summary>
		/// <param name="buffer">The buffer to fill.</param>
		public override void FillCryptoRandomBuffer(byte[] buffer) {
			var rng = new RNGCryptoServiceProvider();
			rng.GetBytes(buffer);
		}

		/// <summary>
		/// Derives a cryptographically strong key from the specified password.
		/// </summary>
		/// <param name="password">The user-supplied password.</param>
		/// <param name="salt">The salt.</param>
		/// <param name="iterations">The rounds of computation to use in deriving a stronger key. The larger this is, the longer attacks will take.</param>
		/// <param name="keySizeInBytes">The desired key size in bytes.</param>
		/// <returns>The generated key.</returns>
		public override byte[] DeriveKeyFromPassword(string password, byte[] salt, int iterations, int keySizeInBytes) {
			Requires.NotNullOrEmpty(password, "password");
			Requires.NotNull(salt, "salt");
			Requires.Range(iterations > 0, "iterations");
			Requires.Range(keySizeInBytes > 0, "keySizeInBytes");

			var keyStrengthening = new Rfc2898DeriveBytes(password, salt, iterations);
			return keyStrengthening.GetBytes(keySizeInBytes);
		}

		/// <summary>
		/// Computes the authentication code for the contents of a stream given the specified symmetric key.
		/// </summary>
		/// <param name="data">The data to compute the HMAC for.</param>
		/// <param name="key">The key to use in hashing.</param>
		/// <param name="hashAlgorithmName">The hash algorithm to use.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The authentication code.</returns>
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

		/// <summary>
		/// Asymmetrically signs a data blob.
		/// </summary>
		/// <param name="data">The data to sign.</param>
		/// <param name="signingPrivateKey">The private key used to sign the data.</param>
		/// <returns>
		/// The signature.
		/// </returns>
		public override byte[] Sign(byte[] data, byte[] signingPrivateKey) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(signingPrivateKey);
				return rsa.SignData(data, this.GetHashAlgorithm(this.AsymmetricHashAlgorithmName));
			}
		}

		/// <summary>
		/// Asymmetrically signs the hash of data.
		/// </summary>
		/// <param name="hash">The hash to sign.</param>
		/// <param name="signingPrivateKey">The private key used to sign the data.</param>
		/// <param name="hashAlgorithmName">The hash algorithm name.</param>
		/// <returns>
		/// The signature.
		/// </returns>
		public override byte[] SignHash(byte[] hash, byte[] signingPrivateKey, string hashAlgorithmName) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(signingPrivateKey);
				return rsa.SignHash(hash, this.AsymmetricHashAlgorithmName);
			}
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
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(signingPublicKey);
				return rsa.VerifyData(data, this.GetHashAlgorithm(hashAlgorithm), signature);
			}
		}

		/// <summary>
		/// Verifies the asymmetric signature of the hash of some data blob.
		/// </summary>
		/// <param name="signingPublicKey">The public key used to verify the signature.</param>
		/// <param name="hash">The hash of the data that was signed.</param>
		/// <param name="signature">The signature.</param>
		/// <param name="hashAlgorithm">The hash algorithm used to hash the data.</param>
		/// <returns>
		/// A value indicating whether the signature is valid.
		/// </returns>
		public override bool VerifyHash(byte[] signingPublicKey, byte[] hash, byte[] signature, string hashAlgorithm) {
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(signingPublicKey);
				return rsa.VerifyHash(hash, hashAlgorithm, signature);
			}
		}

		/// <summary>
		/// Symmetrically encrypts a stream.
		/// </summary>
		/// <param name="plaintext">The stream of plaintext to encrypt.</param>
		/// <param name="ciphertext">The stream to receive the ciphertext.</param>
		/// <param name="encryptionVariables">An optional key and IV to use. May be <c>null</c> to use randomly generated values.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>A task that completes when encryption has completed, whose result is the key and IV to use to decrypt the ciphertext.</returns>
		public override async Task<SymmetricEncryptionVariables> EncryptAsync(Stream plaintext, Stream ciphertext, SymmetricEncryptionVariables encryptionVariables, CancellationToken cancellationToken) {
			var encryptor = this.GetCipher();

			if (encryptionVariables == null) {
				var secureRandom = new SecureRandom();
				byte[] key = new byte[this.SymmetricEncryptionKeySize / 8];
				secureRandom.NextBytes(key);

				var random = new Random();
				byte[] iv = new byte[encryptor.GetBlockSize()];
				random.NextBytes(iv);

				encryptionVariables = new SymmetricEncryptionVariables(key, iv);
			} else {
				Requires.Argument(encryptionVariables.Key.Length == this.SymmetricEncryptionKeySize / 8, "key", "Incorrect length.");
				Requires.Argument(encryptionVariables.IV.Length == encryptor.GetBlockSize(), "iv", "Incorrect length.");
			}

			var parameters = new ParametersWithIV(new KeyParameter(encryptionVariables.Key), encryptionVariables.IV);
			encryptor.Init(true, parameters);
			await CipherStreamCopyAsync(plaintext, ciphertext, encryptor, cancellationToken);
			return encryptionVariables;
		}

		/// <summary>
		/// Symmetrically decrypts a stream.
		/// </summary>
		/// <param name="ciphertext">The stream of ciphertext to decrypt.</param>
		/// <param name="plaintext">The stream to receive the plaintext.</param>
		/// <param name="encryptionVariables">The key and IV to use.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>A task that represents the asynchronous operation.</returns>
		public override async Task DecryptAsync(Stream ciphertext, Stream plaintext, SymmetricEncryptionVariables encryptionVariables, CancellationToken cancellationToken) {
			Requires.NotNull(ciphertext, "ciphertext");
			Requires.NotNull(plaintext, "plaintext");
			Requires.NotNull(encryptionVariables, "encryptionVariables");

			var parameters = new ParametersWithIV(new KeyParameter(encryptionVariables.Key), encryptionVariables.IV);
			var decryptor = this.GetCipher();
			decryptor.Init(false, parameters);
			await CipherStreamCopyAsync(ciphertext, plaintext, decryptor, cancellationToken);
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
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(encryptionPublicKey);
				return rsa.Encrypt(data, true);
			}
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
			using (var rsa = new RSACryptoServiceProvider()) {
				rsa.ImportCspBlob(decryptionPrivateKey);
				return rsa.Decrypt(data, true);
			}
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
			return DigestUtilities.CalculateDigest(hashAlgorithmName, data);
		}

		/// <summary>
		/// Hashes the contents of a stream.
		/// </summary>
		/// <param name="source">The stream to hash.</param>
		/// <param name="hashAlgorithmName">The hash algorithm to use.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>A task whose result is the hash.</returns>
		public override async Task<byte[]> HashAsync(Stream source, string hashAlgorithmName, CancellationToken cancellationToken) {
			Requires.NotNull(source, "source");
			Requires.NotNullOrEmpty(hashAlgorithmName, "hashAlgorithmName");

			var digest = DigestUtilities.GetDigest(hashAlgorithmName);
			byte[] buffer = new byte[digest.GetDigestSize()]; // no idea what the optimal block size is for hashing
			while (source.Position < source.Length) {
				int bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
				digest.BlockUpdate(buffer, 0, bytesRead);
			}

			int hashLength = digest.DoFinal(buffer, 0);
			Assumes.True(hashLength == buffer.Length); // we created the buffer to be the digest size exactly.
			return buffer;
		}

		/// <summary>
		/// Generates a key pair for asymmetric cryptography.
		/// </summary>
		/// <param name="keyPair">Receives the serialized key pair (includes private key).</param>
		/// <param name="publicKey">Receives the public key.</param>
		public override void GenerateSigningKeyPair(out byte[] keyPair, out byte[] publicKey) {
			using (var rsa = new RSACryptoServiceProvider(this.SignatureAsymmetricKeySize)) {
				keyPair = rsa.ExportCspBlob(true);
				publicKey = rsa.ExportCspBlob(false);
			}
		}

		/// <summary>
		/// Generates a key pair for asymmetric cryptography.
		/// </summary>
		/// <param name="keyPair">Receives the serialized key pair (includes private key).</param>
		/// <param name="publicKey">Receives the public key.</param>
		public override void GenerateEncryptionKeyPair(out byte[] keyPair, out byte[] publicKey) {
			using (var rsa = new RSACryptoServiceProvider(this.EncryptionAsymmetricKeySize)) {
				keyPair = rsa.ExportCspBlob(true);
				publicKey = rsa.ExportCspBlob(false);
			}
		}

		/// <summary>
		/// Gets the block cipher.
		/// </summary>
		/// <returns>An instance of a buffered, padded cipher.</returns>
		protected virtual IBufferedCipher GetCipher() {
			return
				CipherUtilities.GetCipher(
					string.Format(
						CultureInfo.InvariantCulture,
						"{0}/{1}/{2}",
						this.SymmetricEncryptionConfiguration.AlgorithmName,
						this.SymmetricEncryptionConfiguration.BlockMode,
						this.SymmetricEncryptionConfiguration.Padding));
		}

		/// <summary>
		/// Gets the hash algorithm to use.
		/// </summary>
		/// <param name="hashAlgorithm">The hash algorithm used to hash the data.</param>
		/// <returns>The hash algorithm.</returns>
		/// <exception cref="System.NotSupportedException">Thrown when the hash algorithm is not recognized or supported.</exception>
		protected virtual HashAlgorithm GetHashAlgorithm(string hashAlgorithm) {
			switch (hashAlgorithm) {
				case "SHA1":
					return new SHA1Managed();
				case "SHA256":
					return new SHA256Managed();
				default:
					throw new NotSupportedException();
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

		/// <summary>
		/// Copies the contents of one stream to another, transforming it with the specified cipher.
		/// </summary>
		/// <param name="source">The source stream.</param>
		/// <param name="destination">The destination stream.</param>
		/// <param name="cipher">The cipher to use.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>A task that completes with the completion of the async work.</returns>
		private static async Task CipherStreamCopyAsync(Stream source, Stream destination, IBufferedCipher cipher, CancellationToken cancellationToken) {
			Requires.NotNull(source, "source");
			Requires.NotNull(destination, "destination");
			Requires.NotNull(cipher, "cipher");

			byte[] sourceBuffer = new byte[cipher.GetBlockSize()];
			byte[] destinationBuffer = new byte[cipher.GetBlockSize() * 2];
			while (true) {
				cancellationToken.ThrowIfCancellationRequested();
				int bytesRead = await source.ReadAsync(sourceBuffer, 0, sourceBuffer.Length, cancellationToken);
				if (bytesRead == 0) {
					break;
				}

				int bytesWritten = cipher.ProcessBytes(sourceBuffer, 0, bytesRead, destinationBuffer, 0);
				await destination.WriteAsync(destinationBuffer, 0, bytesWritten, cancellationToken);
			}

			int finalBytes = cipher.DoFinal(destinationBuffer, 0);
			await destination.WriteAsync(destinationBuffer, 0, finalBytes);
		}
	}
}
