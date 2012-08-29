namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.Contracts;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Runtime.Serialization;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft;
#if !NET40
	using TaskEx = System.Threading.Tasks.Task;
#endif

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
		/// Creates a random string of characters that can appear without escaping in URIs.
		/// </summary>
		/// <param name="length">The desired length of the random string.</param>
		/// <returns>The random string</returns>
		/// <remarks>
		/// The randomization is not cryptographically strong.
		/// </remarks>
		public static string CreateRandomWebSafeName(int length) {
			Requires.Range(length > 0, "length");
			var random = new Random();
			var buffer = new byte[length];
			random.NextBytes(buffer);
			return Utilities.ToBase64WebSafe(buffer).Substring(0, length);
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
		public static void SerializeDataContract<T>(this BinaryWriter writer, T graph) where T : class {
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
		public static T DeserializeDataContract<T>(this BinaryReader binaryReader) {
			Requires.NotNull(binaryReader, "binaryReader");

			var serializer = new DataContractSerializer(typeof(T));
			int length = binaryReader.ReadInt32();
			var ms = new MemoryStream(binaryReader.ReadBytes(length));
			return (T)serializer.ReadObject(ms);
		}

		public static async Task SerializeDataContractAsBase64Async<T>(TextWriter writer, T graph) where T : class {
			Requires.NotNull(writer, "writer");
			Requires.NotNull(graph, "graph");

			var ms = new MemoryStream();
			var binaryWriter = new BinaryWriter(ms);
			SerializeDataContract<T>(binaryWriter, graph);
			binaryWriter.Flush();
			ms.Position = 0;

			const int MaxLineLength = 79;
			string entireBase64 = Convert.ToBase64String(ms.ToArray());
			for (int i = 0; i < entireBase64.Length; i += MaxLineLength) {
				await writer.WriteLineAsync(entireBase64.Substring(i, Math.Min(MaxLineLength, entireBase64.Length - i)));
			}
		}

		public static async Task<T> DeserializeDataContractFromBase64Async<T>(TextReader reader) where T : class {
			Requires.NotNull(reader, "reader");

			var builder = new StringBuilder();
			string line;
			while ((line = await reader.ReadLineAsync()) != null) {
				builder.Append(line);
			}

			var ms = new MemoryStream(Convert.FromBase64String(builder.ToString()));
			var binaryReader = new BinaryReader(ms);
			var value = DeserializeDataContract<T>(binaryReader);
			return value;
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

		/// <summary>
		/// Sets the specified security level's key lengths to the specified crypto provider.
		/// </summary>
		/// <param name="cryptoProvider">The crypto provider.</param>
		/// <param name="level">The level of security to apply.</param>
		public static void ApplySecurityLevel(this ICryptoProvider cryptoProvider, SecurityLevel level) {
			level.Apply(cryptoProvider);
		}

		/// <summary>
		/// Shortens the specified long URL, but leaves the fragment part (if present) visibly applied to the shortened URL.
		/// </summary>
		/// <param name="longUrl">The long URL.</param>
		/// <returns>The short URL.</returns>
		public static async Task<Uri> ShortenExcludeFragmentAsync(this IUrlShortener shortener, Uri longUrl) {
			Requires.NotNull(shortener, "shortener");

			Uri longUriWithoutFragment;
			if (longUrl.Fragment.Length == 0) {
				longUriWithoutFragment = longUrl;
			} else {
				var removeFragmentBuilder = new UriBuilder(longUrl);
				removeFragmentBuilder.Fragment = null;
				longUriWithoutFragment = removeFragmentBuilder.Uri;
			}

			var shortUrl = await shortener.ShortenAsync(longUriWithoutFragment);

			if (longUrl.Fragment.Length > 0) {
				var addFragmentBuilder = new UriBuilder(shortUrl);
				addFragmentBuilder.Fragment = longUrl.Fragment.Substring(1);
				shortUrl = addFragmentBuilder.Uri;
			}

			return shortUrl;
		}

		/// <summary>
		/// Retrieves a contact with some user supplied identifier.
		/// </summary>
		/// <param name="addressBooks">The address books to lookup an identifier in.</param>
		/// <param name="identifier">The user-supplied identifier for the contact.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>
		/// A task whose result is the contact, or null if no match is found.
		/// </returns>
		/// <exception cref="BadAddressBookEntryException">Thrown when a validation error occurs while reading the address book entry.</exception>
		public static async Task<Endpoint> LookupAsync(this IEnumerable<AddressBook> addressBooks, string identifier, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(addressBooks, "addressBooks");
			Requires.NotNullOrEmpty(identifier, "identifier");

			// NOTE: we could optimize this to return as soon as the *first* address book
			// returned a non-null result, and cancel the rest, rather than wait for
			// results from all of them.
			var results = await TaskEx.WhenAll(addressBooks.Select(ab => ab.LookupAsync(identifier, cancellationToken)));
			return results.FirstOrDefault(result => result != null);
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

		internal static async Task<Stream> GetBufferedStreamAsync(this HttpClient client, Uri location, CancellationToken cancellationToken) {
			Requires.NotNull(client, "client");

			var response = await client.GetAsync(location, cancellationToken);
			response.EnsureSuccessStatusCode();
			return await response.Content.ReadAsBufferedStreamAsync(cancellationToken);
		}

		internal static async Task<Stream> ReadAsBufferedStreamAsync(this HttpContent content, CancellationToken cancellationToken) {
			Requires.NotNull(content, "content");

			cancellationToken.ThrowIfCancellationRequested();
			using (var stream = await content.ReadAsStreamAsync()) {
				var memoryStream = new MemoryStream();
				await stream.CopyToAsync(memoryStream, 4096, cancellationToken);
				memoryStream.Position = 0;
				return memoryStream;
			}
		}

		internal static Task<HttpResponseMessage> GetAsync(this HttpClient httpClient, Uri location, string bearerToken, CancellationToken cancellationToken) {
			Requires.NotNull(httpClient, "httpClient");
			Requires.NotNull(location, "location");
			Requires.NotNullOrEmpty(bearerToken, "bearerToken");

			var request = new HttpRequestMessage(HttpMethod.Get, location);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
			return httpClient.SendAsync(request, cancellationToken);
		}

		internal static Task<HttpResponseMessage> DeleteAsync(this HttpClient httpClient, Uri location, string bearerToken, CancellationToken cancellationToken) {
			Requires.NotNull(httpClient, "httpClient");
			Requires.NotNull(location, "location");
			Requires.NotNullOrEmpty(bearerToken, "bearerToken");

			var request = new HttpRequestMessage(HttpMethod.Delete, location);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
			return httpClient.SendAsync(request, cancellationToken);
		}

#if NET40
		internal static Type GetTypeInfo(this Type type) {
			return type;
		}
#endif
	}
}
