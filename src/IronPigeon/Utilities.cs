// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.IO.Pipelines;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Mime;
    using System.Runtime.InteropServices;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using MessagePack;
    using MessagePack.Formatters;
    using MessagePack.Resolvers;
    using Microsoft;
    using Nerdbank.Streams;
    using PCLCrypto;

    /// <summary>
    /// Common utilities for IronPigeon apps.
    /// </summary>
    public static class Utilities
    {
        /// <summary>
        /// Special resolvers required for IronPigeon types.
        /// </summary>
        public static readonly IFormatterResolver IronPigeonTypeResolver = CompositeResolver.Create(
            ByteReadOnlyMemoryFormatter.Instance,
            ByteReadOnlySequenceFormatter.Instance,
            ContentTypeFormatter.Instance);

        /// <summary>
        /// The <see cref="MessagePackSerializerOptions"/> to use when serializing IronPigeon types.
        /// </summary>
        public static readonly MessagePackSerializerOptions MessagePackSerializerOptions = MessagePackSerializerOptions.Standard
            .WithSecurity(MessagePackSecurity.UntrustedData)
            .WithResolver(CompositeResolver.Create(StandardResolver.Instance, IronPigeonTypeResolver));

        /// <summary>
        /// The recommended length of a randomly generated string used to name uploaded blobs.
        /// </summary>
        internal const int BlobNameLength = 15;

        private const HashAlgorithm ThumbprintHashAlgorithm = HashAlgorithm.Sha256;

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
        private static int randSeedSalt;

        /// <summary>
        /// Creates a web safe base64 thumbprint of some buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>A string representation of a hash of the <paramref name="buffer"/>.</returns>
        public static string CreateWebSafeBase64Thumbprint(ReadOnlyMemory<byte> buffer)
        {
            IHashAlgorithmProvider? hasher = WinRTCrypto.HashAlgorithmProvider.OpenAlgorithm(ThumbprintHashAlgorithm);
            var hash = hasher.HashData(buffer.AsOrCreateArray());
            return Utilities.ToBase64WebSafe(hash);
        }

        /// <summary>
        /// Determines whether a given thumbprint matches the actual hash of the specified buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="allegedHashWebSafeBase64Thumbprint">The web-safe base64 encoding of the thumbprint that the specified buffer's thumbprint is expected to match.</param>
        /// <returns><c>true</c> if the thumbprints match; <c>false</c> otherwise.</returns>
        /// <exception cref="System.NotSupportedException">If the length of the thumbprint is not consistent with any supported hash algorithm.</exception>
        public static bool IsThumbprintMatch(ReadOnlyMemory<byte> buffer, string allegedHashWebSafeBase64Thumbprint)
        {
            Requires.NotNullOrEmpty(allegedHashWebSafeBase64Thumbprint, nameof(allegedHashWebSafeBase64Thumbprint));

            byte[] allegedThumbprint = Convert.FromBase64String(FromBase64WebSafe(allegedHashWebSafeBase64Thumbprint));
            HashAlgorithm hashAlgorithm = GuessHashAlgorithmFromLength(allegedThumbprint.Length);

            IHashAlgorithmProvider? hasher = WinRTCrypto.HashAlgorithmProvider.OpenAlgorithm(hashAlgorithm);
            var actualThumbprint = hasher.HashData(buffer.AsOrCreateArray());
            return AreEquivalent(actualThumbprint, allegedThumbprint);
        }

        /// <summary>
        /// Tests whether two arrays are equal in contents and ordering.
        /// </summary>
        /// <param name="first">The first array in the comparison.  May not be null.</param>
        /// <param name="second">The second array in the comparison. May not be null.</param>
        /// <returns>True if the arrays equal; false otherwise.</returns>
        public static bool AreEquivalent(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
        {
            if (first.Length != second.Length)
            {
                return false;
            }

            for (int i = 0; i < first.Length; i++)
            {
                if (first[i] != second[i])
                {
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
        public static string ToBase64WebSafe(byte[] array)
        {
            Requires.NotNull(array, nameof(array));

            return ToBase64WebSafe(Convert.ToBase64String(array));
        }

        /// <summary>
        /// Converts a web-safe base64 encoded string to a standard base64 encoded string.
        /// </summary>
        /// <param name="base64WebSafe">The base64web encoded string.</param>
        /// <returns>A standard base64 encoded string.</returns>
        public static string FromBase64WebSafe(string base64WebSafe)
        {
            Requires.NotNull(base64WebSafe, nameof(base64WebSafe));
            if (base64WebSafe.IndexOfAny(WebSafeSpecificBase64Characters) < 0 && (base64WebSafe.Length % 4) == 0)
            {
                // This web-safe base64 encoded string is equivalent to its standard base64 form.
                return base64WebSafe;
            }

            var base64 = new StringBuilder(base64WebSafe);
            base64.Replace('-', '+');
            base64.Replace('_', '/');

            // Restore any missing padding.  Base64-encoded strings are always a multiple of 4 in length.
            if (base64.Length % 4 > 0)
            {
                base64.Append('=', 4 - (base64.Length % 4));
            }

            return base64.ToString();
        }

        /// <summary>
        /// Creates a random string of characters that can appear without escaping in URIs.
        /// </summary>
        /// <param name="length">The desired length of the random string.</param>
        /// <returns>The random string.</returns>
        /// <remarks>
        /// The randomization is not cryptographically strong.
        /// </remarks>
        public static string CreateRandomWebSafeName(int length)
        {
            Requires.Range(length > 0, "length");
            Random? random = NonCryptoRandomGenerator.Value;
            var buffer = new byte[length];
            random.NextBytes(buffer);
            return ToBase64WebSafe(buffer).Substring(0, length);
        }

        /// <summary>
        /// Shortens the specified long URL, but leaves the fragment part (if present) visibly applied to the shortened URL.
        /// </summary>
        /// <param name="shortener">The URL shortening service to use.</param>
        /// <param name="longUrl">The long URL.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The short URL.</returns>
        public static async Task<Uri> ShortenExcludeFragmentAsync(this IUrlShortener shortener, Uri longUrl, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(shortener, nameof(shortener));
            Requires.NotNull(longUrl, nameof(longUrl));

            Uri longUriWithoutFragment;
            if (longUrl.Fragment.Length == 0)
            {
                longUriWithoutFragment = longUrl;
            }
            else
            {
                var removeFragmentBuilder = new UriBuilder(longUrl);
                removeFragmentBuilder.Fragment = null;
                longUriWithoutFragment = removeFragmentBuilder.Uri;
            }

            Uri? shortUrl = await shortener.ShortenAsync(longUriWithoutFragment, cancellationToken).ConfigureAwait(false);

            if (longUrl.Fragment.Length > 0)
            {
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
        public static async Task<Endpoint?> LookupAsync(this IEnumerable<AddressBook> addressBooks, string identifier, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(addressBooks, nameof(addressBooks));
            Requires.NotNullOrEmpty(identifier, nameof(identifier));

            // NOTE: we could optimize this to return as soon as the *first* address book
            // returned a non-null result, and cancel the rest, rather than wait for
            // results from all of them.
            Endpoint?[]? results = await Task.WhenAll(addressBooks.Select(ab => ab.LookupAsync(identifier, cancellationToken))).ConfigureAwait(false);
            return results.FirstOrDefault(result => result != null);
        }

        /// <summary>
        /// Produces a URL-encoded string of key-value pairs.
        /// </summary>
        /// <param name="data">The key-value pairs to concatenate and escape.</param>
        /// <returns>The URL-encoded string.</returns>
#pragma warning disable CA1055 // Uri return values should not be strings
        public static string UrlEncode(this IEnumerable<KeyValuePair<string, string>> data)
#pragma warning restore CA1055 // Uri return values should not be strings
        {
            Requires.NotNull(data, nameof(data));

            var builder = new StringBuilder();
            foreach (KeyValuePair<string, string> pair in data)
            {
                if (builder.Length > 0)
                {
                    builder.Append('&');
                }

                builder.Append(Uri.EscapeDataString(pair.Key));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(pair.Value));
            }

            return builder.ToString();
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
        public static async Task<TOutput?> FastestQualifyingResultAsync<TInput, TOutput>(IEnumerable<TInput> inputs, Func<CancellationToken, TInput, Task<TOutput?>> asyncOperation, Func<TOutput, bool> qualifyingTest, CancellationToken cancellationToken = default)
            where TOutput : class
        {
            Requires.NotNull(inputs, nameof(inputs));
            Requires.NotNull(asyncOperation, nameof(asyncOperation));
            Requires.NotNull(qualifyingTest, nameof(qualifyingTest));

            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            List<Task<TOutput?>> tasks = inputs.Select(i => asyncOperation(cts.Token, i)).ToList();

            while (tasks.Count > 0)
            {
                Task<TOutput?>? completingTask = await Task.WhenAny(tasks).ConfigureAwait(false);
                TOutput? result = await completingTask.ConfigureAwait(false);
                if (result is object && qualifyingTest(result))
                {
                    cts.Cancel();
                    return await completingTask.ConfigureAwait(false);
                }
                else
                {
                    tasks.Remove(completingTask);
                }
            }

            return default;
        }

        /// <summary>
        /// Applies the appropriate headers to an HTTP request so that the response will not be cached.
        /// </summary>
        /// <param name="request">The request.</param>
        public static void ApplyNoCachePolicy(this HttpRequestMessage request)
        {
            Requires.NotNull(request, nameof(request));

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
        public static Stream ReadStreamWithProgress(this Stream stream, IProgress<long>? bytesReadProgress)
        {
            Requires.NotNull(stream, nameof(stream));

            return bytesReadProgress != null ? new StreamWithProgress(stream, bytesReadProgress) : stream;
        }

        /// <summary>
        /// Applies HTTP Basic authentication to an outgoing HTTP request message.
        /// </summary>
        /// <param name="request">The outbound HTTP request.</param>
        /// <param name="userName">The username.</param>
        /// <param name="password">The password.</param>
        public static void AuthorizeBasic(this HttpRequestMessage request, string userName, string password)
        {
            Requires.NotNull(request, nameof(request));
            Requires.NotNullOrEmpty(userName, nameof(userName));
            Requires.NotNull(password, nameof(password));

            string value = userName + ":" + password;
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        }

        /// <summary>
        /// Determines whether the specified string constitutes a valid Azure blob container name.
        /// </summary>
        /// <param name="containerName">Name of the container.</param>
        /// <returns>
        ///   <c>true</c> if the string is a valid container name; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsValidBlobContainerName(string containerName)
        {
            if (containerName == null)
            {
                return false;
            }

            // Rule #1: can only contain (lowercase) letters, numbers and dashes.
            if (!Regex.IsMatch(containerName, @"^[a-z0-9\-]+$"))
            {
                return false;
            }

            // Rule #2: all dashes must be preceded and followed by a letter or number.
            if (containerName.StartsWith("-", StringComparison.Ordinal) || containerName.EndsWith("-", StringComparison.Ordinal) || containerName.Contains("--"))
            {
                return false;
            }

            // Rule #3: all lowercase.
#pragma warning disable CA1308 // Normalize strings to uppercase
            if (containerName.ToLowerInvariant() != containerName)
#pragma warning restore CA1308 // Normalize strings to uppercase
            {
                return false;
            }

            // Rule #4: length is 3-63
            if (containerName.Length < 3 || containerName.Length > 63)
            {
                return false;
            }

            return true;
        }

#if !NET5_0
        /// <inheritdoc cref="HttpContent.ReadAsStreamAsync()"/>
#pragma warning disable CA1801 // Review unused parameters
        internal static Task<Stream> ReadAsStreamAsync(this HttpContent content, CancellationToken cancellationToken) => content.ReadAsStreamAsync();
#pragma warning restore CA1801 // Review unused parameters

        /// <inheritdoc cref="HttpContent.ReadAsStringAsync()"/>
#pragma warning disable CA1801 // Review unused parameters
        internal static Task<string> ReadAsStringAsync(this HttpContent content, CancellationToken cancellationToken) => content.ReadAsStringAsync();
#pragma warning restore CA1801 // Review unused parameters
#endif

        /// <summary>
        /// Guesses the hash algorithm used given the length of the result.
        /// </summary>
        /// <param name="hashLengthInBytes">The length of the output of the hash function bytes.</param>
        /// <returns>The probable hash algorithm.</returns>
        /// <exception cref="System.NotSupportedException">Thrown when an unrecognized length is specified.</exception>
        internal static HashAlgorithm GuessHashAlgorithmFromLength(int hashLengthInBytes)
        {
            switch (hashLengthInBytes)
            {
                case 160 / 8:
                    return HashAlgorithm.Sha1;
                case 256 / 8:
                    return HashAlgorithm.Sha256;
                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Converts a base64-encoded string to a web-safe base64-encoded string.
        /// </summary>
        /// <param name="base64">The base64-encoded string.</param>
        /// <returns>A base64web encoded string.</returns>
        internal static string ToBase64WebSafe(string base64)
        {
            if (base64.IndexOfAny(UnsafeBase64Characters) < 0)
            {
                // The base64 encoded characters happen to already be web-safe.
                return base64;
            }

            var webSafeBase64 = new StringBuilder(base64);
            webSafeBase64.Replace('+', '-');
            webSafeBase64.Replace('/', '_');
            while (webSafeBase64[webSafeBase64.Length - 1] == '=')
            {
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
        internal static async Task<Stream> GetBufferedStreamAsync(this HttpClient client, Uri location, CancellationToken cancellationToken)
        {
            Requires.NotNull(client, nameof(client));

            HttpResponseMessage? response = await client.GetAsync(location, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsBufferedStreamAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Buffers an HTTP stream's contents and returns the buffered stream.
        /// </summary>
        /// <param name="content">The HTTP content whose stream should be buffered.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task whose result is the buffered response stream.</returns>
        internal static async Task<Stream> ReadAsBufferedStreamAsync(this HttpContent content, CancellationToken cancellationToken)
        {
            Requires.NotNull(content, nameof(content));

            cancellationToken.ThrowIfCancellationRequested();
            using (Stream? stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            {
                var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream, 4096, cancellationToken).ConfigureAwait(false);
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
        internal static async Task<HttpResponseMessage> GetAsync(this HttpClient httpClient, Uri location, string bearerToken, CancellationToken cancellationToken)
        {
            Requires.NotNull(httpClient, nameof(httpClient));
            Requires.NotNull(location, nameof(location));
            Requires.NotNullOrEmpty(bearerToken, nameof(bearerToken));

            using var request = new HttpRequestMessage(HttpMethod.Get, location);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            // Aggressively disable caching since WP8 is rather aggressive at enabling it.
            // Disabling is important because this method is used to retrieve inbox items,
            // and in processing them, the clients tend to delete inbox items from the server
            // which would change the output of a future request to the server with the same URL.
            // But if a cached result is used instead of a real request to the server then we get
            // the same result back.
            ApplyNoCachePolicy(request);

            return await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes an HTTP DELETE, adding a bearer token to the HTTP Authorization header.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use.</param>
        /// <param name="location">The URL to DELETE.</param>
        /// <param name="bearerToken">The bearer token to add to the Authorization header.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The asynchronous HTTP response.</returns>
        internal static async Task<HttpResponseMessage> DeleteAsync(this HttpClient httpClient, Uri location, string bearerToken, CancellationToken cancellationToken)
        {
            Requires.NotNull(httpClient, nameof(httpClient));
            Requires.NotNull(location, nameof(location));
            Requires.NotNullOrEmpty(bearerToken, nameof(bearerToken));

            using var request = new HttpRequestMessage(HttpMethod.Delete, location);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            return await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the underlying array of a <see cref="ReadOnlyMemory{T}"/> or creates a new one with the same content.
        /// </summary>
        /// <typeparam name="T">The type of element stored in memory.</typeparam>
        /// <param name="memory">The memory.</param>
        /// <returns>An array, either the same array or a copy.</returns>
        internal static T[] AsOrCreateArray<T>(this ReadOnlyMemory<T> memory)
        {
            return MemoryMarshal.TryGetArray<T>(memory, out ArraySegment<T> arraySegment) && arraySegment.Offset == 0 && arraySegment.Count == arraySegment.Array.Length
                ? arraySegment.Array
                : memory.ToArray();
        }

        /// <summary>
        /// Gets a hash of a byte buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>The hash code.</returns>
        internal static int GetHashCode(ReadOnlySpan<byte> buffer)
        {
            int hash = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                unchecked
                {
                    hash ^= buffer[i];
                }
            }

            return hash;
        }

        /// <summary>
        /// Copies the (remaining) content of one stream to another.
        /// Reading from the source stream can happen concurrently with writing to the target stream to maximize throughput.
        /// </summary>
        /// <param name="from">The stream to read.</param>
        /// <param name="to">The stream tow rite.</param>
        /// <param name="progress">An optional progress indicator.</param>
        /// <param name="expectedContentLength">The anticipated length of the stream for use with reporting <paramref name="progress"/>.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The total number of bytes copied.</returns>
        internal static async Task<long> ConcurrentCopyToAsync(this Stream from, Stream to, IProgress<(long BytesCopied, long? Total)>? progress, long? expectedContentLength, CancellationToken cancellationToken)
        {
            if (expectedContentLength is null && from.CanSeek)
            {
                expectedContentLength = from.Length;
            }

            using var receivingStreamWithProgress = new StreamWithProgress(to, progress.Adapt(expectedContentLength), leaveOpen: true);

            var pipe = new Pipe();
            await Task.WhenAll(
                from.CopyToAsync(pipe.Writer, cancellationToken).ContinueWith(t => pipe.Writer.Complete(t.Exception?.InnerException ?? t.Exception), TaskScheduler.Default),
                pipe.Reader.CopyToAsync(receivingStreamWithProgress, cancellationToken)).ConfigureAwait(false);

            return receivingStreamWithProgress.BytesTransferred;
        }

        /// <summary>
        /// Creates an <see cref="IProgress{T}"/> that just takes a current value
        /// that forwards the report to an <see cref="IProgress{T}"/> that takes a tuple of current and expected total progress.
        /// </summary>
        /// <param name="progress">The progress object to forward to.</param>
        /// <param name="expectedTotal">The total to mix into each report.</param>
        /// <returns>The progress adapter, or null if <paramref name="progress"/> was null.</returns>
        [return: NotNullIfNotNull("progress")]
        internal static IProgress<long>? Adapt(this IProgress<(long Current, long? Total)>? progress, long? expectedTotal)
        {
            return progress is object ? new Progress<long>(current => progress.Report((current, expectedTotal))) : null;
        }

        /// <summary>
        /// Checks whether an exception is one that is likely to be due to message corruption.
        /// </summary>
        /// <param name="ex">The exception to test.</param>
        /// <returns><c>true</c> if the exception is commonly thrown due to message corruption; <c>false</c> otherwise.</returns>
        internal static bool IsCorruptionException(Exception ex)
        {
            return ex is ArgumentException
                || ex is FormatException
                || ex is InvalidOperationException // while PCLCrypto throws from X509SubjectPublicKeyInfoFormatter.ReadCore
                || ex is EndOfStreamException
                || ex is NotSupportedException
                || ex is System.Security.Cryptography.CryptographicException
                || ex is MessagePackSerializationException;
        }

        /// <summary>
        /// Formatter for the <see cref="ReadOnlyMemory{T}"/> type where T is <see cref="byte"/>.
        /// </summary>
        internal class ByteReadOnlyMemoryFormatter : IMessagePackFormatter<ReadOnlyMemory<byte>>
        {
            /// <summary>
            /// The singleton instance to use.
            /// </summary>
            public static readonly ByteReadOnlyMemoryFormatter Instance = new ByteReadOnlyMemoryFormatter();

            private ByteReadOnlyMemoryFormatter()
            {
            }

            /// <inheritdoc/>
            public void Serialize(ref MessagePackWriter writer, ReadOnlyMemory<byte> value, MessagePackSerializerOptions options)
            {
                writer.Write(value.Span);
            }

            /// <inheritdoc/>
            public ReadOnlyMemory<byte> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                return reader.ReadBytes() is ReadOnlySequence<byte> bytes ? new ReadOnlyMemory<byte>(bytes.ToArray()) : default;
            }
        }

        /// <summary>
        /// Formatter for the <see cref="ReadOnlySequence{T}"/> type where T is <see cref="byte"/>.
        /// </summary>
        internal class ByteReadOnlySequenceFormatter : IMessagePackFormatter<ReadOnlySequence<byte>>
        {
            /// <summary>
            /// The singleton instance to use.
            /// </summary>
            public static readonly ByteReadOnlySequenceFormatter Instance = new ByteReadOnlySequenceFormatter();

            private ByteReadOnlySequenceFormatter()
            {
            }

            /// <inheritdoc/>
            public void Serialize(ref MessagePackWriter writer, ReadOnlySequence<byte> value, MessagePackSerializerOptions options)
            {
                writer.WriteBinHeader(checked((int)value.Length));
                foreach (ReadOnlyMemory<byte> segment in value)
                {
                    writer.WriteRaw(segment.Span);
                }
            }

            /// <inheritdoc/>
            public ReadOnlySequence<byte> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                return reader.ReadBytes() is ReadOnlySequence<byte> bytes ? new ReadOnlySequence<byte>(bytes.ToArray()) : default;
            }
        }

        /// <summary>
        /// Formatter for <see cref="ContentType"/>.
        /// </summary>
        internal class ContentTypeFormatter : IMessagePackFormatter<ContentType?>
        {
            /// <summary>
            /// The singleton instance to use.
            /// </summary>
            public static readonly ContentTypeFormatter Instance = new ContentTypeFormatter();

            private ContentTypeFormatter()
            {
            }

            /// <inheritdoc/>
            public ContentType? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                if (reader.TryReadNil())
                {
                    return null;
                }

                return new ContentType(reader.ReadString());
            }

            /// <inheritdoc/>
            public void Serialize(ref MessagePackWriter writer, ContentType? value, MessagePackSerializerOptions options)
            {
                if (value is null)
                {
                    writer.WriteNil();
                }

                writer.Write(value.ToString());
            }
        }
    }
}
