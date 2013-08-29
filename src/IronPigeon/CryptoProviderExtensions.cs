namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	using Validation;

	/// <summary>
	/// Extension methods to the <see cref="ICryptoProvider"/> interface.
	/// </summary>
	public static class CryptoProviderExtensions {
		/// <summary>
		/// Creates a web safe base64 thumbprint of some buffer.
		/// </summary>
		/// <param name="cryptoProvider">The crypto provider.</param>
		/// <param name="buffer">The buffer.</param>
		/// <returns>A string representation of a hash of the <paramref name="buffer"/>.</returns>
		public static string CreateWebSafeBase64Thumbprint(this ICryptoProvider cryptoProvider, byte[] buffer) {
			Requires.NotNull(cryptoProvider, "cryptoProvider");
			Requires.NotNull(buffer, "buffer");

			var hash = cryptoProvider.Hash(buffer, cryptoProvider.HashAlgorithmName);
			return Utilities.ToBase64WebSafe(hash);
		}

		/// <summary>
		/// Determines whether a given thumbprint matches the actual hash of the specified buffer.
		/// </summary>
		/// <param name="cryptoProvider">The crypto provider.</param>
		/// <param name="buffer">The buffer.</param>
		/// <param name="allegedHashWebSafeBase64Thumbprint">The web-safe base64 encoding of the thumbprint that the specified buffer's thumbprint is expected to match.</param>
		/// <returns><c>true</c> if the thumbprints match; <c>false</c> otherwise.</returns>
		/// <exception cref="System.NotSupportedException">If the length of the thumbprint is not consistent with any supported hash algorithm.</exception>
		public static bool IsThumbprintMatch(this ICryptoProvider cryptoProvider, byte[] buffer, string allegedHashWebSafeBase64Thumbprint) {
			Requires.NotNull(cryptoProvider, "cryptoProvider");
			Requires.NotNull(buffer, "buffer");
			Requires.NotNullOrEmpty(allegedHashWebSafeBase64Thumbprint, "allegedHashWebSafeBase64Thumbprint");

			byte[] allegedThumbprint = Convert.FromBase64String(Utilities.FromBase64WebSafe(allegedHashWebSafeBase64Thumbprint));
			var hashAlgorithm = Utilities.GuessHashAlgorithmFromLength(allegedThumbprint.Length);

			var actualThumbprint = cryptoProvider.Hash(buffer, hashAlgorithm);
			return Utilities.AreEquivalent(actualThumbprint, allegedThumbprint);
		}

		/// <summary>
		/// Computes the hash of the specified buffer and checks for a match to an expected hash.
		/// </summary>
		/// <param name="cryptoProvider">The crypto provider.</param>
		/// <param name="data">The data to hash.</param>
		/// <param name="expectedHash">The expected hash.</param>
		/// <param name="hashAlgorithmName">Name of the hash algorithm. If <c>null</c>, the algorithm is guessed from the length of the hash.</param>
		/// <returns>
		/// <c>true</c> if the hashes came out equal; <c>false</c> otherwise.
		/// </returns>
		internal static bool IsHashMatchWithTolerantHashAlgorithm(this ICryptoProvider cryptoProvider, byte[] data, byte[] expectedHash, string hashAlgorithmName) {
			Requires.NotNull(cryptoProvider, "cryptoProvider");
			Requires.NotNull(data, "data");
			Requires.NotNull(expectedHash, "expectedHash");

			if (hashAlgorithmName == null) {
				hashAlgorithmName = Utilities.GuessHashAlgorithmFromLength(expectedHash.Length);
			}

			byte[] actualHash = cryptoProvider.Hash(data, hashAlgorithmName);
			return Utilities.AreEquivalent(expectedHash, actualHash);
		}

		/// <summary>
		/// Verifies the asymmetric signature of some data blob.
		/// </summary>
		/// <param name="cryptoProvider">The crypto provider.</param>
		/// <param name="signingPublicKey">The public key used to verify the signature.</param>
		/// <param name="data">The data that was signed.</param>
		/// <param name="signature">The signature.</param>
		/// <param name="hashAlgorithm">The hash algorithm used to hash the data. If <c>null</c>, SHA1 and SHA256 will be tried.</param>
		/// <returns>
		/// A value indicating whether the signature is valid.
		/// </returns>
		internal static bool VerifySignatureWithTolerantHashAlgorithm(this ICryptoProvider cryptoProvider, byte[] signingPublicKey, byte[] data, byte[] signature, string hashAlgorithm) {
			if (hashAlgorithm != null) {
				return cryptoProvider.VerifySignature(signingPublicKey, data, signature, hashAlgorithm);
			}

			return cryptoProvider.VerifySignature(signingPublicKey, data, signature, "SHA1")
				|| cryptoProvider.VerifySignature(signingPublicKey, data, signature, "SHA256");
		}
	}
}
