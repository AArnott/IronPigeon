// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IronPigeon.Relay;
using MessagePack;
using Nerdbank.Streams;

internal class InboxHttpHandlerMock : IEndpointInboxFactory
{
    internal static readonly Uri InboxBaseUri = new Uri("http://localhost/inbox/");

    private int counter;

    internal InboxHttpHandlerMock()
    {
        this.Inboxes = new Dictionary<Uri, Inbox>();
    }

    internal Dictionary<Uri, Inbox> Inboxes { get; }

    public Task<InboxCreationResponse> CreateInboxAsync(CancellationToken cancellationToken = default)
    {
        int counter = this.counter++;
        Inbox inbox = new Inbox(new Uri(InboxBaseUri, $"some{counter}/"), $"code{counter}");
        this.Inboxes.Add(inbox.Url, inbox);
        return Task.FromResult(new InboxCreationResponse(inbox.Url, inbox.OwnerCode));
    }

    internal void Register(HttpMessageHandlerMock httpMock)
    {
        httpMock.RegisterHandler(this.HttpHandler);
    }

    private static ReadOnlySequence<byte> WriteIncomingMessage(Inbox inbox, int counter, (ReadOnlyMemory<byte> Content, DateTime ReceivedTimestamp) item)
    {
        var sequence = new Sequence<byte>();
        var writer = new MessagePackWriter(sequence);
        WriteIncomingMessage(inbox, ref writer, counter, item);
        writer.Flush();
        return sequence;
    }

    private static void WriteIncomingMessage(Inbox inbox, ref MessagePackWriter writer, int counter, (ReadOnlyMemory<byte> Content, DateTime ReceivedTimestamp) item)
    {
        writer.WriteMapHeader(3);
        writer.Write(nameof(IncomingInboxItem.Identity));
        writer.Write(new Uri(inbox.Url, $"{counter}").AbsoluteUri);

        writer.Write(nameof(IncomingInboxItem.DatePostedUtc));
        writer.Write(item.ReceivedTimestamp);

        writer.Write(nameof(IncomingInboxItem.Envelope));
        writer.Write(item.Content.Span);
    }

    private static ReadOnlySequence<byte> WriteIncomingMessages(Inbox inbox)
    {
        var sequence = new Sequence<byte>();
        var writer = new MessagePackWriter(sequence);
        int counter = 0;
        foreach ((ReadOnlyMemory<byte> Content, DateTime ReceivedTimestamp) item in inbox.Messages)
        {
            WriteIncomingMessage(inbox, ref writer, counter++, item);
        }

        writer.Flush();

        return sequence;
    }

    private async Task<HttpResponseMessage?> HttpHandler(HttpRequestMessage request)
    {
        UriBuilder simpleUrl = new UriBuilder(request.RequestUri);
        simpleUrl.Query = null;

        // TODO: add inbox owner code check.
        if (request.Method == HttpMethod.Post)
        {
            if (this.Inboxes.TryGetValue(simpleUrl.Uri, out Inbox? inbox))
            {
                byte[] buffer = await request.Content.ReadAsByteArrayAsync();
                inbox.Messages.Add((buffer, DateTime.UtcNow));
                return new HttpResponseMessage { Headers = { Date = DateTime.UtcNow } };
            }
        }
        else if (request.Method == HttpMethod.Get)
        {
            if (this.Inboxes.TryGetValue(simpleUrl.Uri, out Inbox? inbox))
            {
                ReadOnlySequence<byte> ros = WriteIncomingMessages(inbox);
                return new HttpResponseMessage { Content = new ByteArrayContent(ros.ToArray()) };
            }

            if (request.RequestUri.Segments.Length > 0 && int.TryParse(request.RequestUri.Segments[request.RequestUri.Segments.Length - 1], out int messageIndex))
            {
                UriBuilder inboxUrlBuilder = new UriBuilder(simpleUrl.Uri);
                inboxUrlBuilder.Path = inboxUrlBuilder.Path.Substring(0, inboxUrlBuilder.Path.LastIndexOf('/') + 1);
                if (this.Inboxes.TryGetValue(inboxUrlBuilder.Uri, out inbox))
                {
                    (ReadOnlyMemory<byte> Content, DateTime ReceivedTimestamp) message = inbox.Messages[messageIndex];
                    ReadOnlySequence<byte> messageBuffer = WriteIncomingMessage(inbox, messageIndex, message);
                    return new HttpResponseMessage { Content = new ByteArrayContent(messageBuffer.ToArray()) };
                }
            }
        }
        else if (request.Method == HttpMethod.Delete)
        {
            if (request.RequestUri.Segments.Length > 0 && int.TryParse(request.RequestUri.Segments[request.RequestUri.Segments.Length - 1], out int messageIndex))
            {
                UriBuilder inboxUrlBuilder = new UriBuilder(simpleUrl.Uri);
                inboxUrlBuilder.Path = inboxUrlBuilder.Path.Substring(0, inboxUrlBuilder.Path.LastIndexOf('/') + 1);
                if (this.Inboxes.TryGetValue(inboxUrlBuilder.Uri, out Inbox? inbox))
                {
                    inbox.Messages.RemoveAt(messageIndex);
                    return new HttpResponseMessage();
                }
            }
        }

        return null;
    }

    internal class Inbox
    {
        public Inbox(Uri url, string ownerCode)
        {
            this.Url = url;
            this.OwnerCode = ownerCode;
        }

        public Uri Url { get; }

        public string OwnerCode { get; }

        public List<(byte[] Content, DateTime ReceivedTimestamp)> Messages { get; } = new List<(byte[] Content, DateTime ReceivedTimestamp)>();
    }
}
