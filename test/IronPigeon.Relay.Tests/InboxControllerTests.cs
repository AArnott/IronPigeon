// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using IronPigeon;
using IronPigeon.Providers;
using IronPigeon.Relay;
using IronPigeon.Relay.Tests;
using MessagePack;
using Xunit;
using Xunit.Abstractions;

public class InboxControllerTests : TestBase, IClassFixture<RelayAppFactory>
{
    private const string TestMessageContent = "Test";
    private readonly RelayAppFactory factory;
    private readonly RelayCloudBlobStorageProvider relayProvider;
    private readonly HttpClient httpClient;

    public InboxControllerTests(RelayAppFactory factory, ITestOutputHelper logger)
        : base(logger)
    {
        factory.Logger = logger;
        this.factory = factory;
        this.httpClient = this.factory.CreateClient();
        this.relayProvider = new RelayCloudBlobStorageProvider(this.httpClient)
        {
            InboxFactoryUrl = new Uri("Inbox/create", UriKind.Relative),
            BlobPostUrl = new Uri("blob", UriKind.Relative),
        };
    }

    [Fact]
    public async Task CreateNewInbox()
    {
        InboxCreationResponse inbox = await this.relayProvider.CreateInboxAsync(this.TimeoutToken);
        this.Logger.WriteLine($"Created inbox: {inbox.MessageReceivingEndpoint.AbsoluteUri} with owner code: {inbox.InboxOwnerCode}");
    }

    [Fact]
    public async Task NewInboxIsEmpty()
    {
        OwnEndpoint endpoint = await OwnEndpoint.CreateAsync(CryptoSettings.Testing, this.relayProvider, this.TimeoutToken);
        var channel = new Channel(this.factory.CreateClient(), endpoint, this.relayProvider, CryptoSettings.Testing);
        Assert.Empty(await channel.ReceiveInboxItemsAsync(cancellationToken: this.TimeoutToken).ToListAsync(this.TimeoutToken));
    }

    [Fact]
    public async Task PostNotification_NoLifetime()
    {
        InboxCreationResponse inbox = await this.relayProvider.CreateInboxAsync(this.TimeoutToken);
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, inbox.MessageReceivingEndpoint)
        {
            Content = new ByteArrayContent(new byte[3])
            {
                Headers = { ContentLength = 3 },
            },
        };
        AuthorizeInboxRequest(request, inbox);
        HttpResponseMessage response = await this.httpClient.SendAsync(request, this.TimeoutToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetInbox_Unauthorized()
    {
        OwnEndpoint endpoint = await OwnEndpoint.CreateAsync(CryptoSettings.Testing, this.relayProvider, this.TimeoutToken);
        HttpResponseMessage response = await this.httpClient.GetAsync(endpoint.MessageReceivingEndpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetIndividualNotification_WithoutAuthorization()
    {
        (_, Channel receivingChannel) = await this.TransferTestMessageAsync();

        // Receive the message and retrieve it explicitly.
        InboxItem item = Assert.Single(await receivingChannel.ReceiveInboxItemsAsync(cancellationToken: this.TimeoutToken).ToListAsync(this.TimeoutToken));
        HttpResponseMessage itemResponse = await this.httpClient.GetAsync(item.RelayServerItem.Identity, this.TimeoutToken);
        Assert.Equal(HttpStatusCode.Unauthorized, itemResponse.StatusCode);
    }

    [Fact]
    public async Task GetIndividualNotification()
    {
        (_, Channel receivingChannel) = await this.TransferTestMessageAsync();

        // Receive the message and retrieve it explicitly.
        InboxItem item = Assert.Single(await receivingChannel.ReceiveInboxItemsAsync(cancellationToken: this.TimeoutToken).ToListAsync(this.TimeoutToken));
        using var itemRequest = new HttpRequestMessage(HttpMethod.Get, item.RelayServerItem.Identity);
        AuthorizeInboxRequest(itemRequest, receivingChannel.Endpoint);
        HttpResponseMessage itemResponse = await this.httpClient.SendAsync(itemRequest, this.TimeoutToken);
        itemResponse.EnsureSuccessStatusCode();
        byte[] itemContent = await itemResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal<byte>(item.RelayServerItem.Envelope.ToArray(), itemContent);
    }

    [Fact]
    public async Task SendOneMessage()
    {
        (Channel transmittingChannel, Channel receivingChannel) = await this.TransferTestMessageAsync();

        // Receive the message.
        InboxItem item = Assert.Single(await receivingChannel.ReceiveInboxItemsAsync(cancellationToken: this.TimeoutToken).ToListAsync(this.TimeoutToken));
        Assert.Equal(transmittingChannel.Endpoint.PublicEndpoint, item.Author);
        Assert.Equal(receivingChannel.Endpoint.PublicEndpoint, item.Recipient);
        using var receivingContentStream = new MemoryStream();
        using var externalHttpClient = new HttpClient();
        await item.PayloadReference.DownloadPayloadAsync(externalHttpClient, receivingContentStream, cancellationToken: this.TimeoutToken);
        string receivedText = Encoding.UTF8.GetString(receivingContentStream.ToArray());
        Assert.Equal(TestMessageContent, receivedText);
    }

    [Fact]
    public async Task DeleteIncomingMessagePreventsItsRedownload()
    {
        (_, Channel receivingChannel) = await this.TransferTestMessageAsync();

        // Receive the message
        InboxItem item = Assert.Single(await receivingChannel.ReceiveInboxItemsAsync(cancellationToken: this.TimeoutToken).ToListAsync(this.TimeoutToken));

        // Verify it comes again.
        item = Assert.Single(await receivingChannel.ReceiveInboxItemsAsync(cancellationToken: this.TimeoutToken).ToListAsync(this.TimeoutToken));

        // Remove it from the incoming server.
        Assert.NotNull(item.RelayServerItem);
        var relayServer = new RelayServer(this.httpClient, receivingChannel.Endpoint);
        await relayServer.DeleteInboxItemAsync(item.RelayServerItem!, this.TimeoutToken);

        // Verify it does not download again.
        Assert.Empty(await receivingChannel.ReceiveInboxItemsAsync(cancellationToken: this.TimeoutToken).ToListAsync(this.TimeoutToken));
    }

    [Fact]
    public async Task DeleteIncomingMessage_Unauthorized()
    {
        (_, Channel receivingChannel) = await this.TransferTestMessageAsync();

        InboxItem item = Assert.Single(await receivingChannel.ReceiveInboxItemsAsync(cancellationToken: this.TimeoutToken).ToListAsync(this.TimeoutToken));

        HttpResponseMessage response = await this.httpClient.DeleteAsync(item.RelayServerItem.Identity, this.TimeoutToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteInbox()
    {
        InboxCreationResponse inbox = await this.relayProvider.CreateInboxAsync(this.TimeoutToken);

        // First delete should work.
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, inbox.MessageReceivingEndpoint);
        AuthorizeInboxRequest(deleteRequest, inbox);
        HttpResponseMessage response = await this.httpClient.SendAsync(deleteRequest, this.TimeoutToken);
        response.EnsureSuccessStatusCode();

        // Second delete should produce a 404.
        using var deleteRequest2 = new HttpRequestMessage(HttpMethod.Delete, inbox.MessageReceivingEndpoint);
        AuthorizeInboxRequest(deleteRequest2, inbox);
        HttpResponseMessage response2 = await this.httpClient.SendAsync(deleteRequest2, this.TimeoutToken);
        Assert.Equal(HttpStatusCode.NotFound, response2.StatusCode);
    }

    [Fact]
    public async Task DeleteInbox_Unauthorized()
    {
        InboxCreationResponse inbox = await this.relayProvider.CreateInboxAsync(this.TimeoutToken);

        // First delete should work.
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, inbox.MessageReceivingEndpoint);
        HttpResponseMessage response = await this.httpClient.SendAsync(deleteRequest, this.TimeoutToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static void AuthorizeInboxRequest(HttpRequestMessage request, InboxCreationResponse inboxCreationResponse)
    {
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", inboxCreationResponse.InboxOwnerCode);
    }

    private static void AuthorizeInboxRequest(HttpRequestMessage request, OwnEndpoint endpoint)
    {
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", endpoint.InboxOwnerCode);
    }

    private async Task<(Channel TransmittingChannel, Channel ReceivingChannel)> TransferTestMessageAsync()
    {
        OwnEndpoint endpoint1 = await OwnEndpoint.CreateAsync(CryptoSettings.Testing, this.relayProvider, this.TimeoutToken);
        var channel1 = new Channel(this.factory.CreateClient(), endpoint1, this.relayProvider, CryptoSettings.Testing);

        OwnEndpoint endpoint2 = await OwnEndpoint.CreateAsync(CryptoSettings.Testing, this.relayProvider, this.TimeoutToken);
        var channel2 = new Channel(this.factory.CreateClient(), endpoint2, this.relayProvider, CryptoSettings.Testing);

        // Send the message.
        string sentText = "Test";
        using var sentStream = new MemoryStream(Encoding.UTF8.GetBytes(sentText));
        NotificationPostedReceipt receipt = Assert.Single(await channel1.PostAsync(sentStream, new ContentType("text/plain"), new Endpoint[] { channel2.Endpoint.PublicEndpoint }, DateTime.UtcNow.AddHours(1), cancellationToken: this.TimeoutToken));
        Assert.Same(channel2.Endpoint.PublicEndpoint, receipt.Recipient);

        return (channel1, channel2);
    }
}
