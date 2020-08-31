// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft;
    using Xunit;
    using Xunit.Abstractions;

    public class InteropTests : TestBase
    {
        private readonly Mocks.HttpMessageHandlerMock httpHandler;
        private readonly HttpClient httpClient;
        private readonly Mocks.CloudBlobStorageProviderMock cloudStorage;
        private readonly Mocks.InboxHttpHandlerMock inboxMock;

        public InteropTests(ITestOutputHelper logger)
            : base(logger)
        {
            this.httpHandler = new Mocks.HttpMessageHandlerMock();

            this.cloudStorage = new Mocks.CloudBlobStorageProviderMock();
            this.cloudStorage.AddHttpHandler(this.httpHandler);

            this.inboxMock = new Mocks.InboxHttpHandlerMock(new[] { Valid.ReceivingEndpoint1.PublicEndpoint });
            this.inboxMock.Register(this.httpHandler);

            this.httpClient = new HttpClient(this.httpHandler);
        }

        [Fact]
        public async Task CrossSecurityLevelAddressBookExchange()
        {
            CryptoSettings lowLevelCrypto = CryptoSettings.Testing;
            OwnEndpoint lowLevelEndpoint = Valid.GenerateOwnEndpoint(lowLevelCrypto);

            CryptoSettings highLevelCrypto = CryptoSettings.Testing.WithAsymmetricKeySize(2048);
            OwnEndpoint highLevelEndpoint = Valid.GenerateOwnEndpoint(highLevelCrypto);

            await this.TestSendAndReceiveAsync(lowLevelCrypto, lowLevelEndpoint, highLevelCrypto, highLevelEndpoint);
            await this.TestSendAndReceiveAsync(highLevelCrypto, highLevelEndpoint, lowLevelCrypto, lowLevelEndpoint);
        }

        private async Task TestSendAndReceiveAsync(
            CryptoSettings senderCrypto, OwnEndpoint senderEndpoint, CryptoSettings receiverCrypto, OwnEndpoint receiverEndpoint)
        {
            await this.SendMessageAsync(senderCrypto, senderEndpoint, receiverEndpoint.PublicEndpoint);
            await this.ReceiveMessageAsync(receiverCrypto, receiverEndpoint);
        }

        private async Task SendMessageAsync(CryptoSettings senderCrypto, OwnEndpoint senderEndpoint, Endpoint receiverEndpoint)
        {
            Requires.NotNull(senderCrypto, nameof(senderCrypto));
            Requires.NotNull(senderEndpoint, nameof(senderEndpoint));
            Requires.NotNull(receiverEndpoint, nameof(receiverEndpoint));

            var channel = new Channel(this.httpClient, Valid.ReceivingEndpoint1, this.cloudStorage, senderCrypto)
            {
                TraceSource = this.TraceSource,
            };

            using var payload = new MemoryStream(Valid.MessageContent);
            await channel.PostAsync(payload, Valid.ContentType, new[] { receiverEndpoint }, Valid.ExpirationUtc);
        }

        private async Task ReceiveMessageAsync(CryptoSettings receiverCrypto, OwnEndpoint receiverEndpoint)
        {
            Requires.NotNull(receiverCrypto, nameof(receiverCrypto));
            Requires.NotNull(receiverEndpoint, nameof(receiverEndpoint));

            var channel = new Channel(this.httpClient, Valid.ReceivingEndpoint1, this.cloudStorage, receiverCrypto)
            {
                TraceSource = this.TraceSource,
            };

            List<Relay.InboxItem> receivedMessages = await channel.ReceiveInboxItemsAsync(cancellationToken: this.TimeoutToken).ToListAsync(this.TimeoutToken);
            Assert.Single(receivedMessages);
            using var actualPayload = new MemoryStream();
            await receivedMessages[0].PayloadReference.DownloadPayloadAsync(this.httpClient, actualPayload, cancellationToken: this.TimeoutToken);
            Assert.Equal<byte>(Valid.MessageContent, actualPayload.ToArray());
        }
    }
}
