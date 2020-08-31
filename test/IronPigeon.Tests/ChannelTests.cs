// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    public class ChannelTests : TestBase, IDisposable
    {
        private readonly Mocks.HttpMessageHandlerMock httpHandler;
        private readonly HttpClient httpClient;
        private readonly Channel channel1;
        private readonly Channel channel2;
        private readonly Mocks.CloudBlobStorageProviderMock cloudStorage;
        private readonly Mocks.InboxHttpHandlerMock inboxMock;
        private readonly MemoryStream validPayload;

        public ChannelTests(ITestOutputHelper logger)
            : base(logger)
        {
            this.httpHandler = new Mocks.HttpMessageHandlerMock();

            this.cloudStorage = new Mocks.CloudBlobStorageProviderMock();
            this.cloudStorage.AddHttpHandler(this.httpHandler);

            this.inboxMock = new Mocks.InboxHttpHandlerMock(new[] { Valid.ReceivingEndpoint1.PublicEndpoint });
            this.inboxMock.Register(this.httpHandler);

            this.httpClient = new HttpClient(this.httpHandler);

            this.channel1 = new Channel(this.httpClient, Valid.ReceivingEndpoint1, this.cloudStorage, Valid.CryptoSettings);
            this.channel2 = new Channel(this.httpClient, Valid.ReceivingEndpoint2, this.cloudStorage, Valid.CryptoSettings);

            this.validPayload = new MemoryStream(new byte[] { 1, 2, 3 });
        }

        [Fact]
        public void Ctor()
        {
            var blobProvider = new Mock<ICloudBlobStorageProvider>();
            using var httpClient = new HttpClient();
            CryptoSettings cryptoSettings = CryptoSettings.Testing;
            var channel = new Channel(httpClient, Valid.ReceivingEndpoint, blobProvider.Object, cryptoSettings);
            Assert.Same(blobProvider.Object, channel.CloudBlobStorage);
            Assert.Same(Valid.ReceivingEndpoint, channel.Endpoint);
            Assert.Same(httpClient, channel.HttpClient);
            Assert.Same(cryptoSettings, channel.CryptoSettings);
            Assert.NotNull(channel.RelayServer);
        }

        [Fact]
        public async Task PostAsyncBadArgs()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("payload", () => this.channel1.PostAsync(null!, Valid.ContentType, Valid.OneEndpoint, Valid.ExpirationUtc, cancellationToken: this.TimeoutToken));
            await Assert.ThrowsAsync<ArgumentNullException>("contentType", () => this.channel1.PostAsync(this.validPayload, null!, Valid.OneEndpoint, Valid.ExpirationUtc));
            await Assert.ThrowsAsync<ArgumentNullException>("recipients", () => this.channel1.PostAsync(this.validPayload, Valid.ContentType, null!, Valid.ExpirationUtc));

            await Assert.ThrowsAsync<ArgumentException>("recipients", () => this.channel1.PostAsync(this.validPayload, Valid.ContentType, Valid.EmptyEndpoints, Valid.ExpirationUtc));
            await Assert.ThrowsAsync<ArgumentException>("expiresUtc", () => this.channel1.PostAsync(this.validPayload, Valid.ContentType, Valid.OneEndpoint, Invalid.ExpirationUtc));
        }

        [Fact]
        public async Task PostAndReceiveAsync()
        {
            await this.channel1.PostAsync(this.validPayload, Valid.ContentType, new[] { Valid.ReceivingEndpoint2.PublicEndpoint }, Valid.ExpirationUtc, cancellationToken: this.TimeoutToken);
            List<Relay.InboxItem> receivedMessages = await this.channel2.ReceiveInboxItemsAsync(cancellationToken: this.TimeoutToken).ToListAsync(this.TimeoutToken);

            Assert.Single(receivedMessages);
            Assert.Equal(Valid.ContentType, receivedMessages[0].PayloadReference.ContentType);

            using var actualPayload = new MemoryStream();
            await receivedMessages[0].PayloadReference.DownloadPayloadAsync(this.httpClient, actualPayload, cancellationToken: this.TimeoutToken);
            Assert.Equal<byte>(this.validPayload.ToArray(), actualPayload.ToArray());
        }

        [Fact]
        public async Task PayloadReferenceTamperingTests()
        {
            for (int i = 0; i < 100; i++)
            {
                await this.channel1.PostAsync(this.validPayload, Valid.ContentType, new[] { Valid.ReceivingEndpoint2.PublicEndpoint }, Valid.ExpirationUtc, cancellationToken: this.TimeoutToken);

                // Tamper with the payload reference.
                TestUtilities.ApplyFuzzing(this.inboxMock.Inboxes[Valid.ReceivingEndpoint2.PublicEndpoint][0].Content, 1);

                List<Relay.InboxItem> receivedMessages = await this.channel2.ReceiveInboxItemsAsync(cancellationToken: this.TimeoutToken).ToListAsync(this.TimeoutToken);
                Assert.Empty(receivedMessages);
            }
        }

        [Fact]
        public async Task PayloadTamperingTests()
        {
            for (int i = 0; i < 100; i++)
            {
                await this.channel1.PostAsync(this.validPayload, Valid.ContentType, new[] { Valid.ReceivingEndpoint2.PublicEndpoint }, Valid.ExpirationUtc, cancellationToken: this.TimeoutToken);

                // Tamper with the payload itself.
                TestUtilities.ApplyFuzzing(this.cloudStorage.Blobs.Single().Value, 1);

                List<Relay.InboxItem> receivedMessages = await this.channel2.ReceiveInboxItemsAsync(cancellationToken: this.TimeoutToken).ToListAsync(this.TimeoutToken);
                Assert.Empty(receivedMessages);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.httpClient.Dispose();
                this.httpHandler.Dispose();
                this.validPayload.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
