namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.Contracts;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Runtime.Serialization;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Validation;
	using Windows.Data.Xml.Dom;
	using Windows.Security.Cryptography;
	using Windows.Security.Cryptography.Core;
	using Windows.Storage.Streams;
	using Windows.UI.Core;
	using Windows.UI.Notifications;

	/// <summary>
	/// Utilities common to an IronPigeon application targeting WinRT.
	/// </summary>
	public static class WinRTUtilities {
		/// <summary>
		/// Converts a WinRT buffer to a .NET buffer.
		/// </summary>
		/// <param name="buffer">The WinRT buffer.</param>
		/// <returns>The .NET buffer.</returns>
		public static byte[] ToArray(this IBuffer buffer) {
			Requires.NotNull(buffer, "buffer");

			if (buffer.Length == 0) {
				return new byte[0]; // CopyToByteArray produces a null array in this case, so we fix it here.
			}

			byte[] result;
			CryptographicBuffer.CopyToByteArray(buffer, out result);
			return result;
		}

		/// <summary>
		/// Converts a .NET buffer to a WinRT buffer.
		/// </summary>
		/// <param name="array">The .NET buffer.</param>
		/// <returns>The WinRT buffer.</returns>
		public static IBuffer ToBuffer(this byte[] array) {
			return CryptographicBuffer.CreateFromByteArray(array);
		}
	}
}
