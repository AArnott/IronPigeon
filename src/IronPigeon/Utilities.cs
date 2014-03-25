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
	using Validation;
	using TaskEx = System.Threading.Tasks.Task;

	/// <summary>
	/// Common utilities for IronPigeon apps.
	/// </summary>
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
		/// Gets a thread-local instance of a non-crypto random number generator.
		/// </summary>
		/// <remarks>
		/// It's important that we minimize the risk of creating these with the same seed as another
		/// in the case of two threads simultaneously asking for their first RNG. So we use a simple
		/// incrementing salt value to mix into the clock ticks so that each instance produces unique data.
		/// </remarks>
		private static readonly ThreadLocal<Random> NonCryptoRandomGenerator = new ThreadLocal<Random>(
			() => new Random((int)((DateTime.Now.Ticks + Interlocked.Increment(ref randSeedSalt)) % int.MaxValue)));

		/// <summary>
		/// A simple salt for instantiating non-crypto RNGs.
		/// </summary>
		private static int randSeedSalt = 0;

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
			Requires.NotNull(array, "array");

			return ToBase64WebSafe(Convert.ToBase64String(array));
		}

		/// <summary>
		/// Converts a web-safe base64 encoded string to a standard base64 encoded string.
		/// </summary>
		/// <param name="base64WebSafe">The base64web encoded string.</param>
		/// <returns>A standard base64 encoded string.</returns>
		public static string FromBase64WebSafe(string base64WebSafe) {
			Requires.NotNull(base64WebSafe, "base64WebSafe");
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
			var random = NonCryptoRandomGenerator.Value;
			var buffer = new byte[length];
			random.NextBytes(buffer);
			return ToBase64WebSafe(buffer).Substring(0, length);
		}

		/// <summary>
		/// Serializes a data contract.
		/// </summary>
		/// <typeparam name="T">The type of object to serialize.</typeparam>
		/// <param name="writer">The stream writer to use for serialization.</param>
		/// <param name="graph">The object to serialize.</param>
		/// <remarks>
		/// Useful when a data contract is serialized to a stream but is not the only member of that stream.
		/// </remarks>
		public static void SerializeDataContract<T>(this BinaryWriter writer, T graph) {
			Requires.NotNull(writer, "writer");
			Requires.NotNullAllowStructs(graph, "graph");

			var serializer = new DataContractSerializer(typeof(T));
			var ms = new MemoryStream();
			serializer.WriteObject(ms, graph);
			writer.Write((int)ms.Length);
			writer.Write(ms.ToArray(), 0, (int)ms.Length);
		}

		/// <summary>
		/// Deserializes a data contract from a given stream.
		/// </summary>
		/// <typeparam name="T">The type of object to deserialize.</typeparam>
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

		/// <summary>
		/// Writes out the serialized form of the specified object as Base64-encoded text,
		/// with line breaks such that no line exceeds 79 characters in length.
		/// </summary>
		/// <typeparam name="T">The type of the object to serialize.</typeparam>
		/// <param name="writer">The writer to use for emitting base64 encoded text.</param>
		/// <param name="graph">The object to serialize.</param>
		/// <returns>A task that is completed when serialization has completed.</returns>
		public static async Task SerializeDataContractAsBase64Async<T>(TextWriter writer, T graph) where T : class {
			Requires.NotNull(writer, "writer");
			Requires.NotNull(graph, "graph");

			var ms = new MemoryStream();
			var binaryWriter = new BinaryWriter(ms);
			SerializeDataContract(binaryWriter, graph);
			binaryWriter.Flush();
			ms.Position = 0;

			const int MaxLineLength = 79;
			string entireBase64 = Convert.ToBase64String(ms.ToArray());
			for (int i = 0; i < entireBase64.Length; i += MaxLineLength) {
				await writer.WriteLineAsync(entireBase64.Substring(i, Math.Min(MaxLineLength, entireBase64.Length - i)));
			}
		}

		/// <summary>
		/// Deserializes an object previously stored as base64-encoded text, possibly with line breaks.
		/// </summary>
		/// <typeparam name="T">The type of object to deserialize.</typeparam>
		/// <param name="reader">The reader from which to draw base64-encoded text.</param>
		/// <returns>A task whose result is the deserialized object.</returns>
		public static async Task<T> DeserializeDataContractFromBase64Async<T>(TextReader reader) where T : class {
			Requires.NotNull(reader, "reader");

			var builder = new StringBuilder();
			string line;
			while ((line = await reader.ReadLineAsync()) != null) {
				builder.Append(line);
			}

			byte[] buffer;
			try {
				buffer = Convert.FromBase64String(builder.ToString());
			} catch (FormatException ex) {
				throw new SerializationException("Failed to decode base64 string.", ex);
			}

			var ms = new MemoryStream(buffer);
			var binaryReader = new BinaryReader(ms);
			var value = DeserializeDataContract<T>(binaryReader);
			return value;
		}

		/// <summary>
		/// Reads the size of a buffer, and then the buffer itself, from a binary reader.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <returns>The buffer.</returns>
		public static byte[] ReadSizeAndBuffer(this BinaryReader reader) {
			Requires.NotNull(reader, "reader");

			int size = reader.ReadInt32();
			var buffer = new byte[size];
			reader.BaseStream.Read(buffer, 0, size);
			return buffer;
		}

		/// <summary>
		/// Writes out the size of the buffer and its contents.
		/// </summary>
		/// <param name="writer">The receiver of the written bytes.</param>
		/// <param name="buffer">The buffer.</param>
		public static void WriteSizeAndBuffer(this BinaryWriter writer, byte[] buffer) {
			Requires.NotNull(writer, "writer");
			Requires.NotNull(buffer, "buffer");

			writer.Write(buffer.Length);
			writer.Write(buffer);
		}

		/// <summary>
		/// Writes out the size of the buffer and its contents.
		/// </summary>
		/// <param name="stream">The stream to write the buffer's length and contents to.</param>
		/// <param name="buffer">The buffer.</param>
		/// <param name="cancellationToken">The cancellation token.  Cancellation may leave the stream in a partially written state.</param>
		/// <returns>A task whose completion indicates the async operation has completed.</returns>
		public static async Task WriteSizeAndBufferAsync(this Stream stream, byte[] buffer, CancellationToken cancellationToken) {
			Requires.NotNull(stream, "stream");
			Requires.NotNull(buffer, "buffer");

			byte[] lengthBuffer = BitConverter.GetBytes(buffer.Length);
			await stream.WriteAsync(lengthBuffer, 0, lengthBuffer.Length, cancellationToken);
			await stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
		}

		/// <summary>
		/// Writes out the size of the buffer and its contents.
		/// </summary>
		/// <param name="stream">The stream to write the buffer's length and contents to.</param>
		/// <param name="sourceStream">The source stream to write out.</param>
		/// <param name="cancellationToken">The cancellation token.  Cancellation may leave the stream in a partially written state.</param>
		/// <returns>A task whose completion indicates the async operation has completed.</returns>
		public static async Task WriteSizeAndStreamAsync(this Stream stream, Stream sourceStream, CancellationToken cancellationToken) {
			Requires.NotNull(stream, "stream");
			Requires.NotNull(sourceStream, "sourceStream");

			byte[] streamLength = BitConverter.GetBytes((int)(sourceStream.Length - sourceStream.Position));
			await stream.WriteAsync(streamLength, 0, streamLength.Length, cancellationToken);
			await sourceStream.CopyToAsync(stream, 4096, cancellationToken);
		}

		/// <summary>
		/// Reads the size of a buffer, and then the buffer itself, from a binary reader.
		/// </summary>
		/// <param name="stream">The stream from which to read the buffer's size and contents.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <param name="maxSize">The maximum value to accept as the length of the buffer.</param>
		/// <returns>The buffer.</returns>
		/// <exception cref="InvalidMessageException">Thrown if the buffer length read from the stream exceeds the maximum allowable size.</exception>
		public static async Task<byte[]> ReadSizeAndBufferAsync(this Stream stream, CancellationToken cancellationToken, int maxSize = 10 * 1024) {
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
		/// Reads the size of a buffer, and then the buffer itself, from a binary reader.
		/// </summary>
		/// <param name="stream">The stream from which to read the buffer's size and contents.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <param name="maxSize">The maximum value to accept as the length of the buffer.</param>
		/// <returns>The substream of the specified stream that will read just the length of the next object.</returns>
		/// <exception cref="InvalidMessageException">Thrown if the buffer length read from the stream exceeds the maximum allowable size.</exception>
		public static async Task<Stream> ReadSizeAndStreamAsync(this Stream stream, CancellationToken cancellationToken, int maxSize = 10 * 1024) {
			byte[] lengthBuffer = new byte[sizeof(int)];
			await stream.ReadAsync(lengthBuffer, 0, lengthBuffer.Length, cancellationToken);
			int size = BitConverter.ToInt32(lengthBuffer, 0);
			if (size > maxSize) {
				throw new InvalidMessageException(Strings.MaxAllowableMessagePartSizeExceeded);
			}

			return new Substream(stream, size);
		}

		/// <summary>
		/// Sets the specified security level's key lengths to the specified crypto provider.
		/// </summary>
		/// <param name="cryptoProvider">The crypto provider.</param>
		/// <param name="level">The level of security to apply.</param>
		public static void ApplySecurityLevel(this CryptoSettings cryptoProvider, SecurityLevel level) {
			level.Apply(cryptoProvider);
		}

		/// <summary>
		/// Shortens the specified long URL, but leaves the fragment part (if present) visibly applied to the shortened URL.
		/// </summary>
		/// <param name="shortener">The URL shortening service to use.</param>
		/// <param name="longUrl">The long URL.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>The short URL.</returns>
		public static async Task<Uri> ShortenExcludeFragmentAsync(this IUrlShortener shortener, Uri longUrl, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(shortener, "shortener");

			Uri longUriWithoutFragment;
			if (longUrl.Fragment.Length == 0) {
				longUriWithoutFragment = longUrl;
			} else {
				var removeFragmentBuilder = new UriBuilder(longUrl);
				removeFragmentBuilder.Fragment = null;
				longUriWithoutFragment = removeFragmentBuilder.Uri;
			}

			var shortUrl = await shortener.ShortenAsync(longUriWithoutFragment, cancellationToken);

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
			var results = await Task.WhenAll(addressBooks.Select(ab => ab.LookupAsync(identifier, cancellationToken)));
			return results.FirstOrDefault(result => result != null);
		}

		/// <summary>
		/// Produces a URL-encoded string of key-value pairs.
		/// </summary>
		/// <param name="data">The key-value pairs to concatenate and escape.</param>
		/// <returns>The URL-encoded string.</returns>
		public static string UrlEncode(this IEnumerable<KeyValuePair<string, string>> data) {
			Requires.NotNull(data, "data");

			var builder = new StringBuilder();
			foreach (var pair in data) {
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
		/// Wraps a task with one that will complete as cancelled based on a cancellation token, 
		/// allowing someone to await a task but be able to break out early by cancelling the token.
		/// </summary>
		/// <typeparam name="T">The type of value returned by the task.</typeparam>
		/// <param name="task">The task to wrap.</param>
		/// <param name="cancellationToken">The token that can be canceled to break out of the await.</param>
		/// <returns>The wrapping task.</returns>
		public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken) {
			if (cancellationToken.CanBeCanceled) {
				var tcs = new TaskCompletionSource<bool>();
				using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs)) {
					if (task != await Task.WhenAny(task, tcs.Task)) {
						cancellationToken.ThrowIfCancellationRequested();
					}
				}
			}

			// Return result or rethrow any fault/cancellation exception.
			return await task;
		}

		/// <summary>
		/// Wraps a task with one that will complete as cancelled based on a cancellation token, 
		/// allowing someone to await a task but be able to break out early by cancelling the token.
		/// </summary>
		/// <param name="task">The task to wrap.</param>
		/// <param name="cancellationToken">The token that can be canceled to break out of the await.</param>
		/// <returns>The wrapping task.</returns>
		public static async Task WithCancellation(this Task task, CancellationToken cancellationToken) {
			if (cancellationToken.CanBeCanceled) {
				var tcs = new TaskCompletionSource<bool>();
				using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs)) {
					if (task != await Task.WhenAny(task, tcs.Task)) {
						cancellationToken.ThrowIfCancellationRequested();
					}
				}
			}

			// Rethrow any fault/cancellation exception.
			await task;
		}

		/// <summary>
		/// Executes an operation against a collection of providers simultaneously and accepts the first
		/// qualifying result, cancelling the slower responses.
		/// </summary>
		/// <typeparam name="TInput">The type of provider being queried.</typeparam>
		/// <typeparam name="TOutput">The type of result supplied by the provider.</typeparam>
		/// <param name="inputs">The collection of providers.</param>
		/// <param name="asyncOperation">The operation to execute against each provider.</param>
		/// <param name="qualifyingTest">The function that tests whether a received result qualifies.  This function will not be executed concurrently and need not be thread-safe.</param>
		/// <param name="cancellationToken">The overall cancellation token.</param>
		/// <returns>A task whose result is the qualifying result, or <c>default(TOutput)</c> if no result qualified.</returns>
		public static async Task<TOutput> FastestQualifyingResultAsync<TInput, TOutput>(IEnumerable<TInput> inputs, Func<CancellationToken, TInput, Task<TOutput>> asyncOperation, Func<TOutput, bool> qualifyingTest, CancellationToken cancellationToken = default(CancellationToken)) {
			CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			List<Task<TOutput>> tasks = inputs.Select(i => asyncOperation(cts.Token, i)).ToList();

			while (tasks.Count > 0) {
				var completingTask = await Task.WhenAny(tasks);
				if (qualifyingTest(completingTask.Result)) {
					cts.Cancel();
					return completingTask.Result;
				} else {
					tasks.Remove(completingTask);
				}
			}

			return default(TOutput);
		}

		/// <summary>
		/// Applies the appropriate headers to an HTTP request so that the response will not be cached.
		/// </summary>
		/// <param name="request">The request.</param>
		public static void ApplyNoCachePolicy(this HttpRequestMessage request) {
			Requires.NotNull(request, "request");

			// The no-cache headers don't seem to impact the client at all, but perhaps they prevent any intermediaries from caching?
			request.Headers.CacheControl = new CacheControlHeaderValue() { NoCache = true };
			request.Headers.Pragma.Add(new NameValueHeaderValue("no-cache"));
			request.Headers.IfModifiedSince = DateTime.UtcNow; // This last one seems to be the trick that actually works.
		}

		/// <summary>
		/// Gets a stream wrapper that reports bytes read as progress.
		/// </summary>
		/// <param name="stream">The stream to read.</param>
		/// <param name="bytesReadProgress">The progress receiver. May be <c>null</c>.</param>
		/// <returns>The progress-reporting stream.</returns>
		public static Stream ReadStreamWithProgress(this Stream stream, IProgress<int> bytesReadProgress) {
			Requires.NotNull(stream, "stream");

			return bytesReadProgress != null ? new ReadStreamWithProgress(stream, bytesReadProgress) : stream;
		}

		/// <summary>
		/// Applies HTTP Basic authentication to an outgoing HTTP request message.
		/// </summary>
		/// <param name="request">The outbound HTTP request.</param>
		/// <param name="userName">The username.</param>
		/// <param name="password">The password.</param>
		public static void AuthorizeBasic(this HttpRequestMessage request, string userName, string password) {
			Requires.NotNull(request, "request");
			Requires.NotNullOrEmpty(userName, "userName");
			Requires.NotNull(password, "password");

			string value = userName + ":" + password;
			string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
			request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
		}

		/// <summary>
		/// Guesses the hash algorithm used given the length of the result.
		/// </summary>
		/// <param name="hashLengthInBytes">The length of the output of the hash functino bytes.</param>
		/// <returns>The probable hash algorithm.</returns>
		/// <exception cref="System.NotSupportedException">Thrown when an unrecognized length is specified.</exception>
		internal static string GuessHashAlgorithmFromLength(int hashLengthInBytes) {
			switch (hashLengthInBytes) {
				case 160 / 8:
					return "SHA1";
				case 256 / 8:
					return "SHA256";
				default:
					throw new NotSupportedException();
			}
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
		/// Executes an HTTP GET request at the specified location, throwing if the server returns a failing result or a buffered stream on success.
		/// </summary>
		/// <param name="client">The HTTP client to use.</param>
		/// <param name="location">The URI to HTTP GET.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>A task whose result is the buffered response stream.</returns>
		internal static async Task<Stream> GetBufferedStreamAsync(this HttpClient client, Uri location, CancellationToken cancellationToken) {
			Requires.NotNull(client, "client");

			var response = await client.GetAsync(location, cancellationToken);
			response.EnsureSuccessStatusCode();
			return await response.Content.ReadAsBufferedStreamAsync(cancellationToken);
		}

		/// <summary>
		/// Buffers an HTTP stream's contents and returns the buffered stream.
		/// </summary>
		/// <param name="content">The HTTP content whose stream should be buffered.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>A task whose result is the buffered response stream.</returns>
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

		/// <summary>
		/// Executes an HTTP GET, adding a bearer token to the HTTP Authorization header.
		/// </summary>
		/// <param name="httpClient">The HTTP client to use.</param>
		/// <param name="location">The URL to GET.</param>
		/// <param name="bearerToken">The bearer token to add to the Authorization header.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The asynchronous HTTP response.</returns>
		internal static Task<HttpResponseMessage> GetAsync(this HttpClient httpClient, Uri location, string bearerToken, CancellationToken cancellationToken) {
			Requires.NotNull(httpClient, "httpClient");
			Requires.NotNull(location, "location");
			Requires.NotNullOrEmpty(bearerToken, "bearerToken");

			var request = new HttpRequestMessage(HttpMethod.Get, location);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

			// Aggressively disable caching since WP8 is rather aggressive at enabling it.
			// Disabling is important because this method is used to retrieve inbox items,
			// and in processing them, the clients tend to delete inbox items from the server
			// which would change the output of a future request to the server with the same URL.
			// But if a cached result is used instead of a real request to the server then we get
			// the same result back.
			ApplyNoCachePolicy(request);

			return httpClient.SendAsync(request, cancellationToken);
		}

		/// <summary>
		/// Executes an HTTP DELETE, adding a bearer token to the HTTP Authorization header.
		/// </summary>
		/// <param name="httpClient">The HTTP client to use.</param>
		/// <param name="location">The URL to DELETE.</param>
		/// <param name="bearerToken">The bearer token to add to the Authorization header.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The asynchronous HTTP response.</returns>
		internal static Task<HttpResponseMessage> DeleteAsync(this HttpClient httpClient, Uri location, string bearerToken, CancellationToken cancellationToken) {
			Requires.NotNull(httpClient, "httpClient");
			Requires.NotNull(location, "location");
			Requires.NotNullOrEmpty(bearerToken, "bearerToken");

			var request = new HttpRequestMessage(HttpMethod.Delete, location);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
			return httpClient.SendAsync(request, cancellationToken);
		}

		/// <summary>
		/// Gets the string form of the specified buffer.
		/// </summary>
		/// <param name="encoding">The encoding.</param>
		/// <param name="buffer">The buffer.</param>
		/// <returns>A string.</returns>
		internal static string GetString(this Encoding encoding, byte[] buffer) {
			Requires.NotNull(encoding, "encoding");
			Requires.NotNull(buffer, "buffer");

			return encoding.GetString(buffer, 0, buffer.Length);
		}
	}
}
