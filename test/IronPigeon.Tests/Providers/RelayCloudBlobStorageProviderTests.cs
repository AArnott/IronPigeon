// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests.Providers
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using IronPigeon.Providers;
    using IronPigeon.Tests.Mocks;
    using Xunit;
    using Xunit.Abstractions;

    public class RelayCloudBlobStorageProviderTests : TestBase
    {
        private readonly HttpMessageHandlerRecorder messageRecorder;
        private ICloudBlobStorageProvider provider;

        public RelayCloudBlobStorageProviderTests(ITestOutputHelper logger)
            : base(logger)
        {
            this.messageRecorder = HttpMessageHandlerRecorder.CreatePlayback(typeof(RelayCloudBlobStorageProviderTests));
#pragma warning disable CA2000 // Dispose objects before losing scope
            var messageHandler = new ContentLengthVerifyingMockHandler(this.messageRecorder);
            this.provider = new RelayCloudBlobStorageProvider(new HttpClient(messageHandler))
            {
                BlobPostUrl = new Uri("http://localhost:39472/api/blob"),
            };
#pragma warning restore CA2000 // Dispose objects before losing scope
        }

        [Fact]
        public async Task UploadTest()
        {
            this.messageRecorder.SetTestName();
            using (var content = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!")))
            {
                Uri location = await this.provider.UploadMessageAsync(
                    content, DateTime.UtcNow + TimeSpan.FromMinutes(5.5), cancellationToken: this.TimeoutToken);
                Assert.Equal("http://127.0.0.1:10000/devstoreaccount1/blobs/2012.08.26/22A0FLkPHlM-T5q", location.AbsoluteUri);
            }

            var progress = new Progress<long>(p => { });
            using (var content = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!")))
            {
                Uri location = await this.provider.UploadMessageAsync(
                    content, DateTime.UtcNow + TimeSpan.FromMinutes(5.5), progress, this.TimeoutToken);
                Assert.Equal("http://127.0.0.1:10000/devstoreaccount1/blobs/2012.08.26/22A0FLkPHlM-T5q", location.AbsoluteUri);
            }
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
