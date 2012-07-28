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
	using Windows.Data.Xml.Dom;
	using Windows.Security.Cryptography;
	using Windows.Security.Cryptography.Core;
	using Windows.Storage.Streams;
	using Windows.UI.Core;
	using Windows.UI.Notifications;

	public static class WinRTUtilities {
		public static CryptographicKey ExtractPublicKey(this CryptographicKey key, AsymmetricKeyAlgorithmProvider provider) {
			return provider.ImportPublicKey(key.ExportPublicKey());
		}

		public static byte[] ToArray(this IBuffer buffer) {
			byte[] result;
			CryptographicBuffer.CopyToByteArray(buffer, out result);
			return result;
		}

		public static IBuffer ToBuffer(this byte[] array) {
			return CryptographicBuffer.CreateFromByteArray(array);
		}

		internal static async Task WriteSizeAndBufferAsync(this IOutputStream stream, IBuffer buffer) {
			await stream.WriteAsync(CryptographicBuffer.CreateFromByteArray(BitConverter.GetBytes(buffer.Length)));
			await stream.WriteAsync(buffer);
		}

		internal static async Task<IBuffer> ReadSizeAndBufferAsync(this IInputStream stream) {
			IBuffer sizeBuffer = new Windows.Storage.Streams.Buffer(sizeof(int));
			sizeBuffer = await stream.ReadAsync(sizeBuffer, sizeBuffer.Capacity, InputStreamOptions.None); // FEEDBACK: why doesn't sizeBuffer's Length automatically get increased?
			uint size = (uint)BitConverter.ToInt32(sizeBuffer.ToArray(), 0);

			IBuffer buffer = new Windows.Storage.Streams.Buffer(size);
			buffer = await stream.ReadAsync(buffer, size, InputStreamOptions.None);
			return buffer;
		}
	}
}
