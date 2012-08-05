namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.Contracts;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Runtime.Serialization;
	using System.ServiceModel.Security;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft;

	public static class Utilities {
		/// <summary>
		/// The encoding to use when writing out POST entity strings.
		/// </summary>
		private static readonly Encoding PostEntityEncoding = new UTF8Encoding(false);

		/// <summary>
		/// The set of unsafe characters that may be found in a base64-encoded string.
		/// </summary>
		private static readonly char[] UnsafeBase64Characters = new char[] { '+', '/', '=' };

		/// <summary>
		/// The substituted characters that may appear in a web-safe base64 string that should be restored to their
		/// original values before base64-decoding can take place.
		/// </summary>
		private static readonly char[] WebSafeSpecificBase64Characters = new char[] { '-', '_' };

		/// <summary>
		/// Tests whether two arrays are equal in contents and ordering.
		/// </summary>
		/// <typeparam name="T">The type of elements in the arrays.</typeparam>
		/// <param name="first">The first array in the comparison.  May not be null.</param>
		/// <param name="second">The second array in the comparison. May not be null.</param>
		/// <returns>True if the arrays equal; false otherwise.</returns>
		public static bool AreEquivalent<T>(T[] first, T[] second) {
			if ((first == null) ^ (second == null)) {
				return false;
			}

			if (first == null) {
				return true;
			}

			if (first.Length != second.Length) {
				return false;
			}

			for (int i = 0; i < first.Length; i++) {
				if (!first[i].Equals(second[i])) {
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Serializes a data contract.
		/// </summary>
		/// <param name="serializer">The serializer.</param>
		/// <param name="writer">The stream writer to use for serialization.</param>
		/// <param name="graph">The object to serialize.</param>
		/// <remarks>
		/// Useful when a data contract is serialized to a stream but is not the only member of that stream.
		/// </remarks>
		internal static void SerializeDataContract<T>(this BinaryWriter writer, T graph) where T : class {
			Requires.NotNull(writer, "writer");
			Requires.NotNull(graph, "graph");

			var serializer = new DataContractSerializer(typeof(T));
			var ms = new MemoryStream();
			serializer.WriteObject(ms, graph);
			writer.Write((int)ms.Length);
			writer.Write(ms.ToArray(), 0, (int)ms.Length);
		}

		/// <summary>
		/// Deserializes a data contract from a given stream.
		/// </summary>
		/// <param name="serializer">The serializer.</param>
		/// <param name="binaryReader">The stream reader to use for deserialization.</param>
		/// <returns>The deserialized object.</returns>
		/// <remarks>
		/// Useful when a data contract is serialized to a stream but is not the only member of that stream.
		/// </remarks>
		internal static T DeserializeDataContract<T>(this BinaryReader binaryReader) {
			Requires.NotNull(binaryReader, "binaryReader");

			var serializer = new DataContractSerializer(typeof(T));
			int length = binaryReader.ReadInt32();
			var ms = new MemoryStream(binaryReader.ReadBytes(length));
			return (T)serializer.ReadObject(ms);
		}

		public static byte[] ReadSizeAndBuffer(this BinaryReader reader) {
			Requires.NotNull(reader, "reader");

			int size = reader.ReadInt32();
			var buffer = new byte[size];
			reader.BaseStream.Read(buffer, 0, size);
			return buffer;
		}

		public static void WriteSizeAndBuffer(this BinaryWriter writer, byte[] buffer) {
			Requires.NotNull(writer, "writer");
			Requires.NotNull(buffer, "buffer");

			writer.Write(buffer.Length);
			writer.Write(buffer);
		}

		public static async Task WriteSizeAndBufferAsync(this Stream stream, byte[] buffer, CancellationToken cancellationToken) {
			Requires.NotNull(stream, "stream");
			Requires.NotNull(buffer, "buffer");

			byte[] lengthBuffer = BitConverter.GetBytes(buffer.Length);
			await stream.WriteAsync(lengthBuffer, 0, lengthBuffer.Length, cancellationToken);
			await stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
		}

		public static async Task<byte[]> ReadSizeAndBufferAsync(this Stream stream, CancellationToken cancellationToken, int maxSize = 10*1024) {
			byte[] lengthBuffer = new byte[sizeof(int)];
			await stream.ReadAsync(lengthBuffer, 0, lengthBuffer.Length, cancellationToken);
			int size = BitConverter.ToInt32(lengthBuffer, 0);
			if (size > maxSize) {
				throw new InvalidMessageException(Strings.MaxAllowableMessagePartSizeExceeded);
			}

			byte[] buffer = new byte[size];
			await stream.ReadAsync(buffer, 0, size, cancellationToken);
			return buffer;
		}
	}
}
