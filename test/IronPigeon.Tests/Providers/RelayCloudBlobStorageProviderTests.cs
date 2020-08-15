// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests.Providers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using IronPigeon.Providers;
    using Mocks;
    using Xunit;

    public class RelayCloudBlobStorageProviderTests
    {
        private readonly HttpMessageHandlerRecorder messageRecorder;
        private ICloudBlobStorageProvider provider;

        public RelayCloudBlobStorageProviderTests()
        {
            var provider = new RelayCloudBlobStorageProvider(new Uri("http://localhost:39472/api/blob"));
            this.messageRecorder = HttpMessageHandlerRecorder.CreatePlayback(typeof(RelayCloudBlobStorageProviderTests));
            var messageHandler = new ContentLengthVerifyingMockHandler(this.messageRecorder);
            provider.HttpClient = new HttpClient(messageHandler);
            this.provider = provider;
        }

        [Fact]
        public void UploadTest()
        {
            this.messageRecorder.SetTestName();
            var content = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));
            var location = this.provider.UploadMessageAsync(
                content, DateTime.UtcNow + TimeSpan.FromMinutes(5.5), "application/testcontent", "testencoding").Result;
            Assert.Equal("http://127.0.0.1:10000/devstoreaccount1/blobs/2012.08.26/22A0FLkPHlM-T5q", location.AbsoluteUri);

            var progress = new Progress<int>(p => { });
            content = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));
            location = this.provider.UploadMessageAsync(
                content, DateTime.UtcNow + TimeSpan.FromMinutes(5.5), "application/testcontent", "testencoding", progress).Result;
            Assert.Equal("http://127.0.0.1:10000/devstoreaccount1/blobs/2012.08.26/22A0FLkPHlM-T5q", location.AbsoluteUri);
        }

        private class ContentLengthVerifyingMockHandler : MessageProcessingHandler
        {
            internal ContentLengthVerifyingMockHandler(HttpMessageHandler inner)
                : base(inner)
            {
            }

            protected override HttpRequestMessage ProcessRequest(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                Assert.True(request.Content.Headers.ContentLength.HasValue);
                return request;
            }

            protected override HttpResponseMessage ProcessResponse(HttpResponseMessage response, System.Threading.CancellationToken cancellationToken)
            {
                return response;
            }
        }
    }
}
