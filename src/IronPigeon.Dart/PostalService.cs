// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Dart
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using IronPigeon.Relay;
    using MessagePack;
    using Microsoft;
    using Microsoft.VisualStudio.Threading;
    using Nerdbank.Streams;

    /// <summary>
    /// An email sending and receiving service.
    /// </summary>
    public class PostalService
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PostalService"/> class.
        /// </summary>
        /// <param name="channel">The channel.</param>
        public PostalService(Channel channel)
        {
            this.Channel = channel ?? throw new ArgumentNullException(nameof(channel));
        }

        /// <summary>
        /// Gets the channel used to send and receive messages.
        /// </summary>
        public Channel Channel { get; }

        /// <summary>
        /// Sends the specified dart to the recipients specified in the message.
        /// </summary>
        /// <param name="message">The dart to send.</param>
        /// <param name="bytesCopiedProgress">Progress feedback in terms of bytes uploaded.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The asynchronous result.</returns>
        public Task<IReadOnlyCollection<NotificationPostedReceipt>> PostAsync(Message message, IProgress<(long BytesTransferred, long? Total)>? bytesCopiedProgress = null, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(message, nameof(message));

            using Sequence<byte> messageWriter = new Sequence<byte>(ArrayPool<byte>.Shared);
            MessagePackSerializer.Serialize(messageWriter, message, Utilities.MessagePackSerializerOptions, cancellationToken);

            var allRecipients = new List<Endpoint>(message.Recipients);
            if (message.CarbonCopyRecipients != null)
            {
                allRecipients.AddRange(message.CarbonCopyRecipients);
            }

            return this.Channel.PostAsync(messageWriter.AsReadOnlySequence.AsStream(), Message.ContentType, allRecipients, message.ExpirationUtc, bytesCopiedProgress, cancellationToken);
        }

        /// <summary>
        /// Retrieves all messages waiting for pickup at our endpoint.
        /// </summary>
        /// <param name="longPoll">if set to <c>true</c> [long poll].</param>
        /// <param name="purgeUnsupportedMessages">A value indicating whether to purge any messages that are not Dart messages. False will skip the messages but will not delete them from the server.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// Each message as it is received.
        /// </returns>
        public async IAsyncEnumerable<Message> ReceiveAsync(bool longPoll = false, bool purgeUnsupportedMessages = false, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (InboxItem item in this.Channel.ReceiveInboxItemsAsync(longPoll, cancellationToken).ConfigureAwait(false))
            {
                // We're only interested in Dart messages.
                if (!item.PayloadReference.ContentType.Equals(Message.ContentType))
                {
                    if (purgeUnsupportedMessages && item.RelayServerItem is object)
                    {
                        await this.Channel.RelayServer.DeleteInboxItemAsync(item.RelayServerItem, cancellationToken).ConfigureAwait(false);
                    }

                    continue;
                }

                using var httpTimeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                httpTimeoutTokenSource.CancelAfter(this.Channel.HttpTimeout);

                using var messageWriter = new Sequence<byte>();
                await item.PayloadReference.DownloadPayloadAsync(this.Channel.HttpClient, messageWriter.AsStream(), cancellationToken: httpTimeoutTokenSource.Token).ConfigureAwait(false);

                Message message = MessagePackSerializer.Deserialize<Message>(messageWriter.AsReadOnlySequence, Utilities.MessagePackSerializerOptions, cancellationToken);
                message.OriginatingInboxItem = item;
                yield return message;
            }
        }

        /// <summary>
        /// Deletes the specified message from its online inbox so it won't be retrieved again.
        /// </summary>
        /// <param name="message">The message to delete from its online location.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The asynchronous result.</returns>
        public Task DeleteAsync(Message message, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(message, nameof(message));
            Requires.Argument(message.OriginatingInboxItem?.RelayServerItem is object, nameof(message), "Original inbox item no longer available.");

            return this.Channel.RelayServer.DeleteInboxItemAsync(message.OriginatingInboxItem.RelayServerItem, cancellationToken);
        }
    }
}
