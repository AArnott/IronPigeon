namespace IronPigeon.Dart {
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	using Microsoft;
#if NET40
	using ReadOnlyListOfMessage = System.Collections.ObjectModel.ReadOnlyCollection<Message>;
#else
	using ReadOnlyListOfMessage = System.Collections.Generic.IReadOnlyList<Message>;
#endif

	/// <summary>
	/// An email sending and receiving service.
	/// </summary>
	public class PostalService {
		/// <summary>
		/// Initializes a new instance of the <see cref="PostalService" /> class.
		/// </summary>
		/// <param name="channel">The channel used to send and receive messages.</param>
		public PostalService(Channel channel) {
			Requires.NotNull(channel, "channel");
			this.Channel = channel;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="PostalService" /> class.
		/// </summary>
		/// <param name="blobStorageProvider">The blob storage provider.</param>
		/// <param name="cryptoProvider">The crypto provider.</param>
		/// <param name="endpoint">The endpoint.</param>
		public PostalService(ICloudBlobStorageProvider blobStorageProvider, ICryptoProvider cryptoProvider, OwnEndpoint endpoint) {
			Requires.NotNull(blobStorageProvider, "blobStorageProvider");
			Requires.NotNull(cryptoProvider, "cryptoProvider");
			Requires.NotNull(endpoint, "endpoint");

			this.Channel = new Channel(blobStorageProvider, cryptoProvider, endpoint);
		}

		/// <summary>
		/// Gets the channel used to send and receive messages.
		/// </summary>
		public Channel Channel { get; private set; }

		/// <summary>
		/// Sends the specified dart to the recipients specified in the message.
		/// </summary>
		/// <param name="message">The dart to send.</param>
		/// <param name="expirationUtc">The UTC expiration date of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The asynchronous result.</returns>
		public Task PostAsync(Message message, DateTime expirationUtc, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(message, "message");
			Requires.Argument(expirationUtc.Kind == DateTimeKind.Utc, "expirationUtc", Strings.UTCTimeRequired);

			var ms = new MemoryStream();
			var writer = new BinaryWriter(ms);
			writer.SerializeDataContract(message);
			writer.Flush();
			ms.Position = 0;

			var payload = new Payload(ms.ToArray(), Message.ContentType);
			var allRecipients = new List<Endpoint>(message.Recipients);
			allRecipients.AddRange(message.CarbonCopyRecipients);
			return this.Channel.PostAsync(payload, allRecipients, expirationUtc, cancellationToken);
		}

		/// <summary>
		/// Retrieves all messages waiting for pickup at our endpoint.
		/// </summary>
		/// <param name="progress">A callback to invoke for each downloaded message as it arrives.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>A task whose result is the complete list of received messages.</returns>
		public async Task<ReadOnlyListOfMessage> ReceiveAsync(IProgress<Message> progress = null, CancellationToken cancellationToken = default(CancellationToken)) {
			var messages = new List<Message>();
			var payloadProgress = new Progress<Payload>(
				payload => {
					var message = FromPayload(payload);
					if (message != null) {
						lock (messages) {
							messages.Add(message);
							Monitor.Pulse(messages);
						}

						if (progress != null) {
							progress.Report(message);
						}
					}
				});

			var payloads = await this.Channel.ReceiveAsync(payloadProgress, cancellationToken);

			// Ensure that we've receives the asynchronous progress notifications for all the payloads
			// so we don't return a partial result.
			lock (messages) {
				while (messages.Count < payloads.Count) {
					Monitor.Wait(messages);
				}
			}


#if NET40
			return new ReadOnlyCollection<Message>(messages);
#else
			return messages;
#endif
		}

		/// <summary>
		/// Deletes the specified message from its online inbox so it won't be retrieved again.
		/// </summary>
		/// <param name="message">The message to delete from its online location.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The asynchronous result.</returns>
		public Task DeleteAsync(Message message, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(message, "message");
			return this.Channel.DeleteInboxItem(message.OriginatingPayload, cancellationToken);
		}

		/// <summary>
		/// Extracts a message from its serialized payload wrapper.
		/// </summary>
		/// <param name="payload">The payload to extract the message from.</param>
		/// <returns>The extracted message.</returns>
		private static Message FromPayload(Payload payload) {
			Requires.NotNull(payload, "payload");

			if (payload.ContentType != Message.ContentType) {
				return null;
			}

			using (var reader = new BinaryReader(new MemoryStream(payload.Content))) {
				var message = reader.DeserializeDataContract<Message>();
				message.OriginatingPayload = payload;
				return message;
			}
		}
	}
}
