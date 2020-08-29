// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests.Mocks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft;
    using Xunit;

    internal class CloudBlobStorageProviderMock : ICloudBlobStorageProvider
    {
        internal static readonly string BaseUploadUri = "http://localhost/blob/";

        private readonly Dictionary<Uri, byte[]> blobs = new Dictionary<Uri, byte[]>();

        internal CloudBlobStorageProviderMock()
        {
        }

        internal Dictionary<Uri, byte[]> Blobs
        {
            get { return this.blobs; }
        }

        public async Task<Uri> UploadMessageAsync(Stream encryptedMessageContent, DateTime expiration, string? contentType, string? contentEncoding, IProgress<long>? bytesCopiedProgress, CancellationToken cancellationToken)
        {
            Assert.NotEqual(0, encryptedMessageContent.Length);
            Assert.Equal(0, encryptedMessageContent.Position);

            var buffer = new byte[encryptedMessageContent.Length - encryptedMessageContent.Position];
            await encryptedMessageContent.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            lock (this.blobs)
            {
                var contentUri = new Uri(BaseUploadUri + (this.blobs.Count + 1));
                this.blobs[contentUri] = buffer;
                return contentUri;
            }
        }

        internal void AddHttpHandler(HttpMessageHandlerMock httpMock)
        {
            Requires.NotNull(httpMock, nameof(httpMock));
            httpMock.RegisterHandler(this.HandleRequest);
        }

        private Task<HttpResponseMessage?> HandleRequest(HttpRequestMessage request)
        {
            if (this.blobs.TryGetValue(request.RequestUri, out byte[]? buffer))
            {
                return Task.FromResult<HttpResponseMessage?>(new HttpResponseMessage() { Content = new StreamContent(new MemoryStream(buffer)) });
            }

            return Task.FromResult<HttpResponseMessage?>(null);
        }
    }
}
