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

    public class ChannelTests : TestBase, IAsyncLifetime, IDisposable
    {
        private readonly Mocks.MockEnvironment environment = new Mocks.MockEnvironment();
        private readonly MemoryStream validPayload;
        private Channel channel1 = null!; // InitializeAsync
        private Channel channel2 = null!; // InitializeAsync

        public ChannelTests(ITestOutputHelper logger)
            : base(logger)
        {
            this.validPayload = new MemoryStream(new byte[] { 1, 2, 3 });
        }

        public async Task InitializeAsync()
        {
            this.channel1 = await this.environment.CreateChannelAsync(this.TimeoutToken);
            this.channel2 = await this.environment.CreateChannelAsync(this.TimeoutToken);
        }

        public Task DisposeAsync()
        {
            this.environment.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public void Ctor()
        {
            var blobProvider = new Mock<ICloudBlobStorageProvider>();
            using var httpClient = new HttpClient();
            CryptoSettings cryptoSettings = CryptoSettings.Testing;
            var channel = new Channel(httpClient, this.channel1.Endpoint, blobProvider.Object, cryptoSettings);
            Assert.Same(blobProvider.Object, channel.CloudBlobStorage);
            Assert.Same(this.channel1.Endpoint, channel.Endpoint);
            Assert.Same(httpClient, channel.HttpClient);
            Assert.Same(cryptoSettings, channel.CryptoSettings);
            Assert.NotNull(channel.RelayServer);
        }

        [Fact]
        public async Task PostAsyncBadArgs()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("payload", () => this.channel1.PostAsync(null!, Valid.ContentType, new[] { this.channel1.Endpoint.PublicEndpoint }, Valid.ExpirationUtc, cancellationToken: this.TimeoutToken));
            await Assert.ThrowsAsync<ArgumentNullException>("contentType", () => this.channel1.PostAsync(this.validPayload, null!, new[] { this.channel1.Endpoint.PublicEndpoint }, Valid.ExpirationUtc));
            await Assert.ThrowsAsync<ArgumentNullException>("recipients", () => this.channel1.PostAsync(this.validPayload, Valid.ContentType, null!, Valid.ExpirationUtc));

            await Assert.ThrowsAsync<ArgumentException>("recipients", () => this.channel1.PostAsync(this.validPayload, Valid.ContentType, Array.Empty<Endpoint>(), Valid.ExpirationUtc));
            await Assert.ThrowsAsync<ArgumentException>("expiresUtc", () => this.channel1.PostAsync(this.validPayload, Valid.ContentType, new[] { this.channel1.Endpoint.PublicEndpoint }, Invalid.ExpirationUtc));
        }

        [Fact]
        public async Task PostAndReceiveAsync()
        {
            await this.channel1.PostAsync(this.validPayload, Valid.ContentType, new[] { this.channel2.Endpoint.PublicEndpoint }, Valid.ExpirationUtc, cancellationToken: this.TimeoutToken);
            List<Relay.InboxItem> receivedMessages = await this.channel2.ReceiveInboxItemsAsync(cancellationToken: this.TimeoutToken).ToListAsync(this.TimeoutToken);

            Assert.Single(receivedMessages);
            Assert.Equal(Valid.ContentType, receivedMessages[0].PayloadReference.ContentType);

            using var actualPayload = new MemoryStream();
            await receivedMessages[0].PayloadReference.DownloadPayloadAsync(this.environment.HttpClient, actualPayload, cancellationToken: this.TimeoutToken);
            Assert.Equal<byte>(this.validPayload.ToArray(), actualPayload.ToArray());
        }

        [Fact]
        public async Task PayloadReferenceTamperingTests()
        {
            for (int i = 0; i < 100; i++)
            {
                using MemoryStream payload = new MemoryStream(new byte[] { 1, 2, 3 });
                await this.channel1.PostAsync(payload, Valid.ContentType, new[] { this.channel2.Endpoint.PublicEndpoint }, Valid.ExpirationUtc, cancellationToken: this.TimeoutToken);

                // Tamper with the payload reference.
                TestUtilities.ApplyFuzzing(this.environment.InboxServer.Inboxes[this.channel2.Endpoint.MessageReceivingEndpoint].Messages[0].Content, 1);

                List<Relay.InboxItem> receivedMessages = await this.channel2.ReceiveInboxItemsAsync(cancellationToken: this.TimeoutToken).ToListAsync(this.TimeoutToken);
                Assert.Empty(receivedMessages);
            }
        }

        [Fact]
        public async Task PayloadTamperingTests()
        {
            for (int i = 0; i < 16; i++)
            {
                using MemoryStream payload = new MemoryStream(new byte[] { 1, 2, 3 });
                await this.channel1.PostAsync(payload, Valid.ContentType, new[] { this.channel2.Endpoint.PublicEndpoint }, Valid.ExpirationUtc, cancellationToken: this.TimeoutToken);

                // Tamper with the payload itself.
                unchecked
                {
                    this.environment.CloudStorage.Blobs.Single().Value[i]++;
                }

                await foreach (Relay.InboxItem receivedMessages in this.channel2.ReceiveInboxItemsAsync(cancellationToken: this.TimeoutToken))
                {
                    using var ms = new MemoryStream();
                    await Assert.ThrowsAsync<InvalidMessageException>(() => receivedMessages.PayloadReference.DownloadPayloadAsync(this.environment.HttpClient, ms, cancellationToken: this.TimeoutToken));
                }

                this.environment.CloudStorage.Blobs.Clear();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.validPayload.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
