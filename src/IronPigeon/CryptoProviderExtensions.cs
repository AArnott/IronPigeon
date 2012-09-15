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

			var hash = cryptoProvider.Hash(buffer);
			return Utilities.ToBase64WebSafe(hash);
		}
	}
}
