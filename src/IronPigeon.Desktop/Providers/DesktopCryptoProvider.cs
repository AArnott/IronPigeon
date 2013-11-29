namespace IronPigeon.Providers {
	using System;
	using System.Collections.Generic;
	using System.Composition;
	using System.IO;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Validation;

	/// <summary>
	/// The (full) .NET Framework implementation of cryptography.
	/// </summary>
	[Export(typeof(ICryptoProvider))]
	[Shared]
	public class DesktopCryptoProvider : CryptoProviderBase {
		/// <summary>
		/// Initializes a new instance of the <see cref="DesktopCryptoProvider" /> class
		/// with the default security level.
		/// </summary>
		public DesktopCryptoProvider()
			: this(SecurityLevel.Maximum) {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DesktopCryptoProvider" /> class.
		/// </summary>
		/// <param name="securityLevel">The security level to apply to this instance.  The default is <see cref="SecurityLevel.Maximum"/>.</param>
		public DesktopCryptoProvider(SecurityLevel securityLevel) {
			Requires.NotNull(securityLevel, "securityLevel");
			securityLevel.Apply(this);
		}

		/// <summary>
		/// Gets the length (in bits) of the symmetric encryption cipher block.
		/// </summary>
		public override int SymmetricEncryptionBlockSize {
			get {
				using (var alg = SymmetricAlgorithm.Create(this.SymmetricEncryptionConfiguration.AlgorithmName)) {
					return alg.BlockSize;
				}
			}
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
				return rsa.SignData(data, this.AsymmetricHashAlgorithmName);
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
				return rsa.VerifyData(data, hashAlgorithm, signature);
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
			using (var hasher = HashAlgorithm.Create(hashAlgorithmName)) {
				if (hasher == null) {
					throw new NotSupportedException();
				}

				return hasher.ComputeHash(data);
			}
		}

		/// <summary>
		/// Hashes the contents of a stream.
		/// </summary>
		/// <param name="source">The stream to hash.</param>
		/// <param name="hashAlgorithmName">The hash algorithm to use.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>A task whose result is the hash.</returns>
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

		/// <inheritdoc/>
		public override void BeginNegotiateSharedSecret(out byte[] privateKey, out byte[] publicKey) {
			var keyParameters = new CngKeyCreationParameters {
				ExportPolicy = CngExportPolicies.AllowPlaintextExport,
			};
			var cngKey = CngKey.Create(GetECDiffieHellmanAlgorithm(this.ECDiffieHellmanKeySize), null, keyParameters);
			using (var ec = new ECDiffieHellmanCng(cngKey)) {
				privateKey = ec.Key.Export(CngKeyBlobFormat.GenericPrivateBlob);
				publicKey = ec.PublicKey.ToByteArray();
			}
		}

		/// <inheritdoc/>
		public override void RespondNegotiateSharedSecret(byte[] remotePublicKey, out byte[] ownPublicKey, out byte[] sharedSecret) {
			using (var ec = new ECDiffieHellmanCng(this.ECDiffieHellmanKeySize)) {
				var remoteECPublicKey = ECDiffieHellmanCngPublicKey.FromByteArray(remotePublicKey, CngKeyBlobFormat.EccPublicBlob);
				ownPublicKey = ec.PublicKey.ToByteArray();
				sharedSecret = ec.DeriveKeyMaterial(remoteECPublicKey);
			}
		}

		/// <inheritdoc/>
		public override void EndNegotiateSharedSecret(byte[] ownPrivateKey, byte[] remotePublicKey, out byte[] sharedSecret) {
			CngKey key = CngKey.Import(ownPrivateKey, CngKeyBlobFormat.EccPrivateBlob);
			using (var ec = new ECDiffieHellmanCng(key)) {
				var remoteECPublicKey = CngKey.Import(remotePublicKey, CngKeyBlobFormat.EccPublicBlob);
				sharedSecret = ec.DeriveKeyMaterial(remoteECPublicKey);
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
		/// Gets the ECDH algorithm that matches the specified size.
		/// </summary>
		/// <param name="keySizeInBits">The size of the key, in bits.</param>
		/// <returns>The algorithm.</returns>
		private static CngAlgorithm GetECDiffieHellmanAlgorithm(int keySizeInBits) {
			switch (keySizeInBits) {
				case 256: return CngAlgorithm.ECDiffieHellmanP256;
				case 384: return CngAlgorithm.ECDiffieHellmanP384;
				case 521: return CngAlgorithm.ECDiffieHellmanP521;
				default: throw new NotSupportedException();
			}
		}
	}
}
