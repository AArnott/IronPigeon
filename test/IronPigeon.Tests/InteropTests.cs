// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft;
    using Xunit;
    using Xunit.Abstractions;

    public class InteropTests
    {
        private Mocks.LoggerMock logger;

        public InteropTests(ITestOutputHelper logger)
        {
            this.logger = new Mocks.LoggerMock(logger);
        }

        [Fact]
        public async Task CrossSecurityLevelAddressBookExchange()
        {
            var lowLevelCrypto = new CryptoSettings(SecurityLevel.Minimum);
            OwnEndpoint? lowLevelEndpoint = Valid.GenerateOwnEndpoint(lowLevelCrypto);

            var highLevelCrypto = new CryptoSettings(SecurityLevel.Minimum) { AsymmetricKeySize = 2048 };
            OwnEndpoint? highLevelEndpoint = Valid.GenerateOwnEndpoint(highLevelCrypto);

            await this.TestSendAndReceiveAsync(lowLevelCrypto, lowLevelEndpoint, highLevelCrypto, highLevelEndpoint);
            await this.TestSendAndReceiveAsync(highLevelCrypto, highLevelEndpoint, lowLevelCrypto, lowLevelEndpoint);
        }

        private async Task TestSendAndReceiveAsync(
            CryptoSettings senderCrypto, OwnEndpoint senderEndpoint, CryptoSettings receiverCrypto, OwnEndpoint receiverEndpoint)
        {
            var inboxMock = new Mocks.InboxHttpHandlerMock(new[] { receiverEndpoint.PublicEndpoint });
            var cloudStorage = new Mocks.CloudBlobStorageProviderMock();

            await this.SendMessageAsync(cloudStorage, inboxMock, senderCrypto, senderEndpoint, receiverEndpoint.PublicEndpoint);
            await this.ReceiveMessageAsync(cloudStorage, inboxMock, receiverCrypto, receiverEndpoint);
        }

        private async Task SendMessageAsync(Mocks.CloudBlobStorageProviderMock cloudStorage, Mocks.InboxHttpHandlerMock inboxMock, CryptoSettings senderCrypto, OwnEndpoint senderEndpoint, Endpoint receiverEndpoint)
        {
            Requires.NotNull(cloudStorage, nameof(cloudStorage));
            Requires.NotNull(senderCrypto, nameof(senderCrypto));
            Requires.NotNull(senderEndpoint, nameof(senderEndpoint));
            Requires.NotNull(receiverEndpoint, nameof(receiverEndpoint));

            using var httpHandler = new Mocks.HttpMessageHandlerMock();

            cloudStorage.AddHttpHandler(httpHandler);

            inboxMock.Register(httpHandler);

            Payload? sentMessage = Valid.Message;

            var channel = new Channel()
            {
                HttpClient = new HttpClient(httpHandler),
                CloudBlobStorage = cloudStorage,
                CryptoServices = senderCrypto,
                Endpoint = senderEndpoint,
                Logger = this.logger,
            };

            await channel.PostAsync(sentMessage, new[] { receiverEndpoint }, Valid.ExpirationUtc);
        }

        private async Task ReceiveMessageAsync(Mocks.CloudBlobStorageProviderMock cloudStorage, Mocks.InboxHttpHandlerMock inboxMock, CryptoSettings receiverCrypto, OwnEndpoint receiverEndpoint)
        {
            Requires.NotNull(cloudStorage, nameof(cloudStorage));
            Requires.NotNull(receiverCrypto, nameof(receiverCrypto));
            Requires.NotNull(receiverEndpoint, nameof(receiverEndpoint));

            using var httpHandler = new Mocks.HttpMessageHandlerMock();

            cloudStorage.AddHttpHandler(httpHandler);
            inboxMock.Register(httpHandler);

            var channel = new Channel
            {
                HttpClient = new HttpClient(httpHandler),
                HttpClientLongPoll = new HttpClient(httpHandler),
                CloudBlobStorage = cloudStorage,
                CryptoServices = receiverCrypto,
                Endpoint = receiverEndpoint,
                Logger = this.logger,
            };

            IReadOnlyList<Channel.PayloadReceipt>? messages = await channel.ReceiveAsync();
            Assert.Equal(1, messages.Count);
            Assert.Equal(Valid.Message, messages[0].Payload);
        }
    }
}
