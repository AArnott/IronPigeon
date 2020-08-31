// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests.Mocks
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using IronPigeon.Relay;
    using MessagePack;
    using Microsoft.Extensions.Azure;
    using Nerdbank.Streams;

    internal class InboxHttpHandlerMock
    {
        internal static readonly Uri InboxBaseUri = new Uri("http://localhost/inbox/");

        internal InboxHttpHandlerMock(IReadOnlyList<Endpoint> recipients)
        {
            this.Inboxes = recipients.ToDictionary(r => r, r => new List<(byte[], DateTime)>());
        }

        internal Dictionary<Endpoint, List<(byte[] Content, DateTime ReceivedTimestamp)>> Inboxes { get; }

        internal void Register(HttpMessageHandlerMock httpMock)
        {
            httpMock.RegisterHandler(this.HttpHandler);
        }

        private static ReadOnlySequence<byte> WriteIncomingMessage(Endpoint recipient, int counter, (ReadOnlyMemory<byte> Content, DateTime ReceivedTimestamp) item)
        {
            var sequence = new Sequence<byte>();
            var writer = new MessagePackWriter(sequence);
            WriteIncomingMessage(recipient, ref writer, counter, item);
            writer.Flush();
            return sequence;
        }

        private static void WriteIncomingMessage(Endpoint recipient, ref MessagePackWriter writer, int counter, (ReadOnlyMemory<byte> Content, DateTime ReceivedTimestamp) item)
        {
            writer.WriteMapHeader(3);
            writer.Write(nameof(IncomingInboxItem.Identity));
            writer.Write(new Uri(recipient.MessageReceivingEndpoint, $"/{counter++}").AbsoluteUri);

            writer.Write(nameof(IncomingInboxItem.DatePostedUtc));
            writer.Write(item.ReceivedTimestamp);

            writer.Write(nameof(IncomingInboxItem.Envelope));
            writer.WriteRaw(item.Content.Span);
        }

        private async Task<HttpResponseMessage?> HttpHandler(HttpRequestMessage request)
        {
            if (request.Method == HttpMethod.Post)
            {
                Endpoint? recipient = this.Inboxes.Keys.FirstOrDefault(r => r.MessageReceivingEndpoint.AbsolutePath == request.RequestUri.AbsolutePath);
                if (recipient is object)
                {
                    List<(byte[] Content, DateTime ReceivedTimestamp)>? inbox = this.Inboxes[recipient];
                    byte[] buffer = await request.Content.ReadAsByteArrayAsync();
                    inbox.Add((buffer, DateTime.UtcNow));
                    return new HttpResponseMessage();
                }
            }
            else if (request.Method == HttpMethod.Get)
            {
                Endpoint? recipient = this.Inboxes.Keys.FirstOrDefault(r => r.MessageReceivingEndpoint == request.RequestUri);
                if (recipient is object)
                {
                    ReadOnlySequence<byte> ros = this.WriteIncomingMessages(recipient);
                    return new HttpResponseMessage { Content = new ByteArrayContent(ros.ToArray()) };
                }

                recipient = this.Inboxes.Keys.FirstOrDefault(r => request.RequestUri.AbsolutePath.StartsWith(r.MessageReceivingEndpoint.AbsolutePath + "/", StringComparison.Ordinal));
                if (recipient is object)
                {
                    var messageIndex = int.Parse(request.RequestUri.Segments[request.RequestUri.Segments.Length - 1], CultureInfo.InvariantCulture);
                    (ReadOnlyMemory<byte> Content, DateTime ReceivedTimestamp) message = this.Inboxes[recipient][messageIndex];
                    ReadOnlySequence<byte> messageBuffer = WriteIncomingMessage(recipient, messageIndex, message);
                    return new HttpResponseMessage { Content = new ByteArrayContent(messageBuffer.ToArray()) };
                }
            }

            return null;
        }

        private ReadOnlySequence<byte> WriteIncomingMessages(Endpoint recipient)
        {
            var sequence = new Sequence<byte>();
            var writer = new MessagePackWriter(sequence);
            int counter = 0;
            foreach ((ReadOnlyMemory<byte> Content, DateTime ReceivedTimestamp) item in this.Inboxes[recipient])
            {
                WriteIncomingMessage(recipient, ref writer, counter++, item);
            }

            writer.Flush();

            return sequence;
        }
    }
}
