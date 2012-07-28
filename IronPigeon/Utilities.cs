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
		/// Converts a byte array to a web-safe base64-encoded string.
		/// </summary>
		/// <param name="array">The array of bytes to encode.</param>
		/// <returns>A base64web encoded string.</returns>
		public static string ToBase64WebSafe(byte[] array) {
			Contract.Requires(array != null);
			Contract.Ensures(Contract.Result<string>() != null);

			return ToBase64WebSafe(Convert.ToBase64String(array));
		}

		/// <summary>
		/// Converts a web-safe base64 encoded string to a standard base64 encoded string.
		/// </summary>
		/// <param name="base64WebSafe">The base64web encoded string.</param>
		/// <returns>A standard base64 encoded string.</returns>
		public static string FromBase64WebSafe(string base64WebSafe) {
			Contract.Requires(base64WebSafe != null);
			if (base64WebSafe.IndexOfAny(WebSafeSpecificBase64Characters) < 0 && (base64WebSafe.Length % 4) == 0) {
				// This web-safe base64 encoded string is equivalent to its standard base64 form.
				return base64WebSafe;
			}

			var base64 = new StringBuilder(base64WebSafe);
			base64.Replace('-', '+');
			base64.Replace('_', '/');

			// Restore any missing padding.  Base64-encoded strings are always a multiple of 4 in length.
			if (base64.Length % 4 > 0) {
				base64.Append('=', 4 - (base64.Length % 4));
			}

			return base64.ToString();
		}

		/// <summary>
		/// Sends a multipart HTTP POST request (useful for posting files).
		/// </summary>
		/// <param name="request">The HTTP request.</param>
		/// <param name="requestHandler">The request handler.</param>
		/// <param name="parts">The parts to include in the POST entity.</param>
		/// <returns>The HTTP response.</returns>
		public static async Task<HttpWebResponse> PostMultipartAsync(this HttpWebRequest request, IEnumerable<MultipartPostPart> parts) {
			Requires.NotNull(request, "request");
			Requires.NotNull(parts, "parts");

			await PostMultipartNoGetResponseAsync(request, parts);
			return (HttpWebResponse)await request.GetResponseAsync();
		}

		public static void FixupInteropMultipartFormData(this MultipartFormDataContent content) {
			Requires.NotNull(content, "content");

			foreach (var part in content) {
				part.Headers.ContentDisposition.FileNameStar = null;

				if (!string.IsNullOrEmpty(part.Headers.ContentDisposition.Name)) {
					part.Headers.ContentDisposition.Name = EnsureSurroundingQuotes(part.Headers.ContentDisposition.Name);
					part.Headers.ContentDisposition.FileName = EnsureSurroundingQuotes(part.Headers.ContentDisposition.FileName);
				}
			}
		}

		public static string GetFullMessage(this Exception exception) {
			var builder = new StringBuilder();
			while (exception != null) {
				builder.Append(exception.Message);
				if (!exception.Message.TrimEnd().EndsWith(".")) {
					builder.Append(".");
				}

				builder.Append(" ");
				exception = exception.InnerException;
			}

			return builder.ToString();
		}

		public static string AssembleQueryString(Dictionary<string, string> args) {
			Requires.NotNull(args, "args");

			var builder = new StringBuilder();
			foreach (var pair in args) {
				if (builder.Length > 0) {
					builder.Append("&");
				}

				builder.Append(Uri.EscapeDataString(pair.Key));
				builder.Append("=");
				builder.Append(Uri.EscapeDataString(pair.Value));
			}

			return builder.ToString();
		}

		/// <summary>
		/// Sends a multipart HTTP POST request (useful for posting files) but doesn't call GetResponse on it.
		/// </summary>
		/// <param name="request">The HTTP request.</param>
		/// <param name="requestHandler">The request handler.</param>
		/// <param name="parts">The parts to include in the POST entity.</param>
		internal static async Task PostMultipartNoGetResponseAsync(this HttpWebRequest request, IEnumerable<MultipartPostPart> parts) {
			Requires.NotNull(request, "request");
			Requires.NotNull(parts, "parts");

			parts = parts.ToList();
			string boundary = Guid.NewGuid().ToString();
			string initialPartLeadingBoundary = string.Format(CultureInfo.InvariantCulture, "--{0}\r\n", boundary);
			string partLeadingBoundary = string.Format(CultureInfo.InvariantCulture, "\r\n--{0}\r\n", boundary);
			string finalTrailingBoundary = string.Format(CultureInfo.InvariantCulture, "\r\n--{0}--\r\n", boundary);
			request.Method = "POST";
			request.ContentType = "multipart/form-data; boundary=" + boundary;
			long contentLength = parts.Sum(p => partLeadingBoundary.Length + p.Length) + finalTrailingBoundary.Length;
			if (parts.Any()) {
				contentLength -= 2; // the initial part leading boundary has no leading \r\n
			}
			////request.Headers[HttpRequestHeader.ContentLength] = contentLength.ToString(CultureInfo.InvariantCulture);

			var requestStream = await request.GetRequestStreamAsync();
			try {
				StreamWriter writer = new StreamWriter(requestStream, PostEntityEncoding);
				bool firstPart = true;
				foreach (var part in parts) {
					await writer.WriteAsync(firstPart ? initialPartLeadingBoundary : partLeadingBoundary);
					firstPart = false;
					await part.SerializeAsync(writer);
					part.Dispose();
				}

				await writer.WriteAsync(finalTrailingBoundary);
				await writer.FlushAsync();
			} finally {
				// We need to be sure to close the request stream...
				// unless it is a MemoryStream, which is a clue that we're in
				// a mock stream situation and closing it would preclude reading it later.
				if (!(requestStream is MemoryStream)) {
					requestStream.Dispose();
				}
			}
		}

		private static string EnsureSurroundingQuotes(string value) {
			if (value == null || value.StartsWith("\"")) {
				return value;
			}

			return "\"" + value + "\"";
		}

		/// <summary>
		/// Converts a base64-encoded string to a web-safe base64-encoded string.
		/// </summary>
		/// <param name="base64">The base64-encoded string.</param>
		/// <returns>A base64web encoded string.</returns>
		internal static string ToBase64WebSafe(string base64) {
			Contract.Requires(base64 != null);
			Contract.Ensures(Contract.Result<string>() != null);

			if (base64.IndexOfAny(UnsafeBase64Characters) < 0) {
				// The base64 encoded characters happen to already be web-safe.
				return base64;
			}

			var webSafeBase64 = new StringBuilder(base64);
			webSafeBase64.Replace('+', '-');
			webSafeBase64.Replace('/', '_');
			while (webSafeBase64[webSafeBase64.Length - 1] == '=') {
				webSafeBase64.Length--;
			}

			return webSafeBase64.ToString();
		}

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
		/// Returns a hash code for a given buffer.
		/// </summary>
		/// <param name="buffer">The buffer.</param>
		/// <returns>
		/// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
		/// </returns>
		internal static int GetHashCode(byte[] buffer) {
			int hashCode = 0;

			if (buffer != null) {
				unchecked {
					hashCode = buffer.Aggregate(hashCode, (c, b) => c + b);
				}
			}

			return hashCode;
		}

		/// <summary>
		/// Tests equality between two objects, allowing one or both to be  null without throwing an exception.
		/// </summary>
		/// <param name="value1">The first value to compare.</param>
		/// <param name="value2">The second value to compare.</param>
		/// <returns><c>true</c> if both values are null, or the values are equal; <c>false</c> otherwise.</returns>
		internal static bool SafeEquals(this object value1, object value2) {
			if ((value1 == null) ^ (value2 == null)) {
				return false;
			}

			if (value1 == null) {
				return true;
			}

			return value1.Equals(value2);
		}

		/// <summary>
		/// Rounds a number up to some least common multiple.
		/// </summary>
		/// <param name="value">The value.</param>
		/// <param name="blockSize">Size of the step.</param>
		/// <returns>The adjusted value.</returns>
		internal static int RoundUp(int value, int blockSize) {
			int expansion = value % blockSize;
			return expansion == 0 ? value : value + (blockSize - expansion);
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

		public static async Task<byte[]> ReadSizeAndBufferAsync(this Stream stream, CancellationToken cancellationToken) {
			byte[] lengthBuffer = new byte[sizeof(int)];
			await stream.ReadAsync(lengthBuffer, 0, lengthBuffer.Length, cancellationToken);
			int size = BitConverter.ToInt32(lengthBuffer, 0);

			byte[] buffer = new byte[size];
			await stream.ReadAsync(buffer, 0, size, cancellationToken);
			return buffer;
		}

		/// <summary>
		/// Throws an exception if a condition is not true.
		/// </summary>
		internal static void VerifyThrowInvalidFormat(bool condition, string message, params object[] formattingArgs) {
			Contract.Ensures(condition);
			Contract.EnsuresOnThrow<MessageSecurityException>(!condition);
			if (!condition) {
				throw new MessageSecurityException(String.Format(message, formattingArgs));
			}
		}
	}
}
