// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using IronPigeon;
using Microsoft;
using Xunit;

internal class CloudBlobStorageProviderMock : ICloudBlobStorageProvider
{
    internal const string BaseUploadUri = "http://localhost/blob/";

    private readonly Dictionary<Uri, byte[]> blobs = new Dictionary<Uri, byte[]>();

    internal CloudBlobStorageProviderMock()
    {
    }

    internal Dictionary<Uri, byte[]> Blobs
    {
        get { return this.blobs; }
    }

    public async Task<Uri> UploadMessageAsync(Stream encryptedMessageContent, DateTime expiration, MediaTypeHeaderValue? contentType, IProgress<long>? bytesCopiedProgress, CancellationToken cancellationToken)
    {
        using var bufferStream = new MemoryStream();
        await encryptedMessageContent.CopyToAsync(bufferStream, 4096, cancellationToken);
        Assert.NotEqual(0, bufferStream.Length);

        lock (this.blobs)
        {
            var contentUri = new Uri(BaseUploadUri + (this.blobs.Count + 1));
            this.blobs[contentUri] = bufferStream.ToArray();
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
