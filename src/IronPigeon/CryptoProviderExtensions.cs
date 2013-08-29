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
	}
}
