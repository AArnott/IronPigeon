// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using IronPigeon;
using Microsoft;
using Xunit;
using Xunit.Abstractions;

public class InteropTests : TestBase
{
    private readonly MockEnvironment environment = new MockEnvironment();

    public InteropTests(ITestOutputHelper logger)
        : base(logger)
    {
    }

    [Fact]
    public async Task CrossSecurityLevelAddressBookExchange()
    {
        CryptoSettings lowLevelCrypto = CryptoSettings.Testing;
        OwnEndpoint lowLevelEndpoint = await this.environment.CreateOwnEndpointAsync(lowLevelCrypto, this.TimeoutToken);

        CryptoSettings highLevelCrypto = CryptoSettings.Testing.WithAsymmetricKeySize(2048);
        OwnEndpoint highLevelEndpoint = await this.environment.CreateOwnEndpointAsync(highLevelCrypto, this.TimeoutToken);

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

        Channel channel = this.environment.CreateChannel(senderEndpoint, senderCrypto);
        channel.TraceSource = this.TraceSource;

        using var payload = new MemoryStream(Valid.MessageContent);
        await channel.PostAsync(payload, Valid.ContentType, new[] { receiverEndpoint }, Valid.ExpirationUtc);
    }

    private async Task ReceiveMessageAsync(CryptoSettings receiverCrypto, OwnEndpoint receiverEndpoint)
    {
        Requires.NotNull(receiverCrypto, nameof(receiverCrypto));
        Requires.NotNull(receiverEndpoint, nameof(receiverEndpoint));

        Channel channel = this.environment.CreateChannel(receiverEndpoint, receiverCrypto);
        channel.TraceSource = this.TraceSource;

        List<IronPigeon.Relay.InboxItem> receivedMessages = await channel.ReceiveInboxItemsAsync(cancellationToken: this.TimeoutToken).ToListAsync(this.TimeoutToken);
        Assert.Single(receivedMessages);
        using var actualPayload = new MemoryStream();
        await receivedMessages[0].PayloadReference.DownloadPayloadAsync(this.environment.HttpClient, actualPayload, cancellationToken: this.TimeoutToken);
        Assert.Equal<byte>(Valid.MessageContent, actualPayload.ToArray());
    }
}
