namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
#if NET40
	using System.ComponentModel.Composition;
#else
	using System.Composition;
#endif
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	using IronPigeon.Providers;
	using IronPigeon.Relay;

	using Validation;
#if NET40
	using ReadOnlyCollectionOfEndpoint = System.Collections.Generic.IEnumerable<Endpoint>;
	using ReadOnlyListOfInboxItem = System.Collections.ObjectModel.ReadOnlyCollection<IncomingList.IncomingItem>;
	using ReadOnlyListOfPayload = System.Collections.ObjectModel.ReadOnlyCollection<Payload>;
#else
	using ReadOnlyCollectionOfEndpoint = System.Collections.Generic.IReadOnlyCollection<Endpoint>;
	using ReadOnlyListOfInboxItem = System.Collections.Generic.IReadOnlyList<IncomingList.IncomingItem>;
	using ReadOnlyListOfPayload = System.Collections.Generic.IReadOnlyList<Payload>;
	using TaskEx = System.Threading.Tasks.Task;
#endif

	/// <summary>
	/// A channel for sending or receiving secure messages.
	/// </summary>
	[Export]
#if !NET40
	[Shared]
#endif
	public class Channel {
		/// <summary>
		/// The HTTP client to use for long poll HTTP requests.
		/// </summary>
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private HttpClient httpClientLongPoll;

		/// <summary>
		/// Gets or sets the provider of blob storage.
		/// </summary>
		[Import]
		public ICloudBlobStorageProvider CloudBlobStorage { get; set; }

		/// <summary>
		/// Gets or sets the provider for cryptographic operations.
		/// </summary>
		/// <value>
		/// The crypto services.
		/// </value>
		[Import]
		public ICryptoProvider CryptoServices { get; set; }

		/// <summary>
		/// Gets or sets the endpoint used to receive messages.
		/// </summary>
		/// <value>
		/// The endpoint.
		/// </value>
		public OwnEndpoint Endpoint { get; set; }

		/// <summary>
		/// Gets or sets the URL shortener.
		/// </summary>
		[Import(AllowDefault = true)]
		public IUrlShortener UrlShortener { get; set; }

		/// <summary>
		/// Gets or sets the logger.
		/// </summary>
		public ILogger Logger { get; set; }

		/// <summary>
		/// Gets or sets the HTTP client used for outbound HTTP requests.
		/// </summary>
		[Import]
		public HttpClient HttpClient { get; set; }

		/// <summary>
		/// Gets or sets the HTTP client to use for long poll HTTP requests.
		/// </summary>
		[Import]
		public HttpClient HttpClientLongPoll {
			get {
				return this.httpClientLongPoll;
			}

			set {
				value.Timeout = TimeSpan.FromMilliseconds(Timeout.Infinite);
				this.httpClientLongPoll = value;
			}
		}

		#region Initialization methods

		/// <summary>
		/// Contacts a message relay services to request the creation of a new inbox to receive messages.
		/// </summary>
		/// <param name="messageReceivingEndpointBaseUrl">The URL of the message relay service to use for the new endpoint.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>A task whose completion signals the registration result.</returns>
		public async Task CreateInboxAsync(Uri messageReceivingEndpointBaseUrl, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(messageReceivingEndpointBaseUrl, "messageReceivingEndpointBaseUrl");
			Verify.Operation(this.Endpoint.PublicEndpoint.MessageReceivingEndpoint == null, "Inbox already created.");

			var abe = this.Endpoint.CreateAddressBookEntry(this.CryptoServices);
			var ms = new MemoryStream();
			var addressBookEntryWriter = new BinaryWriter(ms);
			addressBookEntryWriter.SerializeDataContract(abe);
			addressBookEntryWriter.Flush();

			var registerUrl = new Uri(messageReceivingEndpointBaseUrl, "create");

			var responseMessage =
				await this.HttpClient.PostAsync(registerUrl, null, cancellationToken);
			responseMessage.EnsureSuccessStatusCode();
			using (var responseStream = await responseMessage.Content.ReadAsStreamAsync()) {
				var deserializer = new DataContractJsonSerializer(typeof(InboxCreationResponse));
				var creationResponse = (InboxCreationResponse)deserializer.ReadObject(responseStream);
				this.Endpoint.PublicEndpoint.MessageReceivingEndpoint = new Uri(creationResponse.MessageReceivingEndpoint, UriKind.Absolute);
				this.Endpoint.InboxOwnerCode = creationResponse.InboxOwnerCode;
			}
		}

		/// <summary>
		/// Saves the information required to send this channel messages to the blob store,
		/// and returns the URL to share with senders.
		/// </summary>
		/// <param name="cancellationToken">A cancellation token to abort the publish.</param>
		/// <returns>A task whose result is the absolute URI to the address book entry.</returns>
		public async Task<Uri> PublishAddressBookEntryAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			var abe = this.Endpoint.CreateAddressBookEntry(this.CryptoServices);
			var abeWriter = new StringWriter();
			await Utilities.SerializeDataContractAsBase64Async(abeWriter, abe);
			var ms = new MemoryStream(Encoding.UTF8.GetBytes(abeWriter.ToString()));
			var location = await this.CloudBlobStorage.UploadMessageAsync(ms, DateTime.MaxValue, AddressBookEntry.ContentType, cancellationToken: cancellationToken);
			if (this.UrlShortener != null) {
				location = await this.UrlShortener.ShortenAsync(location);
			}

			var fullLocationWithFragment = new Uri(
				location,
				"#" + this.CryptoServices.CreateWebSafeBase64Thumbprint(this.Endpoint.PublicEndpoint.SigningKeyPublicMaterial));
			return fullLocationWithFragment;
		}

		#endregion

		#region Message receiving methods

		/// <summary>
		/// Downloads messages from the server.
		/// </summary>
		/// <param name="longPoll"><c>true</c> to asynchronously wait for messages if there are none immediately available for download.</param>
		/// <param name="progress">A callback that receives messages as they are retrieved.</param>
		/// <param name="cancellationToken">A token whose cancellation signals lost interest in the result of this method.</param>
		/// <returns>A collection of all messages that were waiting at the time this method was invoked.</returns>
		/// <exception cref="HttpRequestException">Thrown when a connection to the server could not be established, or was terminated.</exception>
		public async Task<ReadOnlyListOfPayload> ReceiveAsync(bool longPoll = false, IProgress<Payload> progress = null, CancellationToken cancellationToken = default(CancellationToken)) {
			var inboxItems = await this.DownloadIncomingItemsAsync(longPoll, cancellationToken);

			var payloads = new List<Payload>();
			foreach (var item in inboxItems) {
				try {
					var invite = await this.DownloadPayloadReferenceAsync(item, cancellationToken);
					if (invite == null) {
						continue;
					}

					var message = await this.DownloadPayloadAsync(invite, cancellationToken);
					payloads.Add(message);
					if (progress != null) {
						progress.Report(message);
					}
				} catch (SerializationException ex) {
					throw new InvalidMessageException(Strings.InvalidMessage, ex);
				} catch (DecoderFallbackException ex) {
					throw new InvalidMessageException(Strings.InvalidMessage, ex);
				} catch (OverflowException ex) {
					throw new InvalidMessageException(Strings.InvalidMessage, ex);
				} catch (OutOfMemoryException ex) {
					throw new InvalidMessageException(Strings.InvalidMessage, ex);
				} catch (Exception ex) { // all those platform-specific exceptions that aren't available to portable libraries.
					throw new InvalidMessageException(Strings.InvalidMessage, ex);
				}
			}

#if NET40
			return new ReadOnlyCollection<Payload>(payloads);
#else
			return payloads;
#endif
		}

		#endregion

		#region Message sending methods

		/// <summary>
		/// Sends some payload to a set of recipients.
		/// </summary>
		/// <param name="message">The payload to transmit.</param>
		/// <param name="recipients">The recipients to receive the message.</param>
		/// <param name="expiresUtc">The date after which the message may be destroyed.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The task representing the asynchronous operation.</returns>
		public async Task PostAsync(Payload message, ReadOnlyCollectionOfEndpoint recipients, DateTime expiresUtc, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(message, "message");
			Requires.That(expiresUtc.Kind == DateTimeKind.Utc, "expiresUtc", Strings.UTCTimeRequired);
			Requires.NotNullOrEmpty(recipients, "recipients");

			var payloadReference = await this.PostPayloadAsync(message, expiresUtc, cancellationToken);
			await this.PostPayloadReferenceAsync(payloadReference, recipients, cancellationToken);
		}

		#endregion

		/// <summary>
		/// Deletes the online inbox item that points to a previously downloaded payload.
		/// </summary>
		/// <param name="payload">The payload whose originating inbox item should be deleted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The task representing the asynchronous operation.</returns>
		/// <remarks>
		/// This method should be called after the client application has saved the
		/// downloaded payload to persistent storage.
		/// </remarks>
		public Task DeleteInboxItemAsync(Payload payload, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(payload, "payload");
			Requires.Argument(payload.PayloadReferenceUri != null, "payload", "Original payload reference URI no longer available.");

			return this.DeletePayloadReferenceAsync(payload.PayloadReferenceUri, cancellationToken);
		}

		#region Protected message sending/receiving methods

		/// <summary>
		/// Downloads a <see cref="PayloadReference"/> that is referenced from an incoming inbox item.
		/// </summary>
		/// <param name="inboxItem">The inbox item that referenced the <see cref="PayloadReference"/>.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The task representing the asynchronous operation.</returns>
		protected virtual async Task<PayloadReference> DownloadPayloadReferenceAsync(IncomingList.IncomingItem inboxItem, CancellationToken cancellationToken) {
			Requires.NotNull(inboxItem, "inboxItem");

			var responseMessage = await this.HttpClient.GetAsync(inboxItem.Location, cancellationToken);
			if (responseMessage.StatusCode == HttpStatusCode.NotFound) {
				// delete inbox item and move on.
				await this.DeletePayloadReferenceAsync(inboxItem.Location, cancellationToken);
				this.Log("Missing payload reference.", null);
				return null;
			}

			responseMessage.EnsureSuccessStatusCode();
			var responseStream = await responseMessage.Content.ReadAsStreamAsync();
			var responseStreamCopy = new MemoryStream();
			await responseStream.CopyToAsync(responseStreamCopy, 4096, cancellationToken);
			responseStreamCopy.Position = 0;

			var encryptedKey = await responseStreamCopy.ReadSizeAndBufferAsync(cancellationToken);
			var key = this.CryptoServices.Decrypt(this.Endpoint.EncryptionKeyPrivateMaterial, encryptedKey);
			var iv = await responseStreamCopy.ReadSizeAndBufferAsync(cancellationToken);
			var ciphertext = await responseStreamCopy.ReadSizeAndBufferAsync(cancellationToken);
			var encryptedPayload = new SymmetricEncryptionResult(key, iv, ciphertext);

			var plainTextPayloadBuffer = this.CryptoServices.Decrypt(encryptedPayload);

			var plainTextPayloadStream = new MemoryStream(plainTextPayloadBuffer);
			var signature = await plainTextPayloadStream.ReadSizeAndBufferAsync(cancellationToken);
			long payloadStartPosition = plainTextPayloadStream.Position;
			var signedBytes = new byte[plainTextPayloadStream.Length - plainTextPayloadStream.Position];
			await plainTextPayloadStream.ReadAsync(signedBytes, 0, signedBytes.Length);
			plainTextPayloadStream.Position = payloadStartPosition;
			var plainTextPayloadReader = new BinaryReader(plainTextPayloadStream);

			var recipientPublicSigningKeyBuffer = plainTextPayloadReader.ReadSizeAndBuffer();

			var creationDateUtc = DateTime.FromBinary(plainTextPayloadReader.ReadInt64());
			var notificationAuthor = Utilities.DeserializeDataContract<Endpoint>(plainTextPayloadReader);
			var messageReference = Utilities.DeserializeDataContract<PayloadReference>(plainTextPayloadReader);
			messageReference.ReferenceLocation = inboxItem.Location;

			if (!this.CryptoServices.VerifySignature(notificationAuthor.SigningKeyPublicMaterial, signedBytes, signature)) {
				throw new InvalidMessageException();
			}

			if (!Utilities.AreEquivalent(recipientPublicSigningKeyBuffer, this.Endpoint.PublicEndpoint.SigningKeyPublicMaterial)) {
				throw new InvalidMessageException(Strings.MisdirectedMessage);
			}

			return messageReference;
		}

		/// <summary>
		/// Downloads the message payload referred to by the specified <see cref="PayloadReference"/>.
		/// </summary>
		/// <param name="notification">The payload reference.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The task representing the asynchronous operation.</returns>
		protected virtual async Task<Payload> DownloadPayloadAsync(PayloadReference notification, CancellationToken cancellationToken) {
			Requires.NotNull(notification, "notification");

			var responseMessage = await this.HttpClient.GetAsync(notification.Location, cancellationToken);
			var messageBuffer = await responseMessage.Content.ReadAsByteArrayAsync();

			// Calculate hash of downloaded message and check that it matches the referenced message hash.
			var messageHash = this.CryptoServices.Hash(messageBuffer);
			if (!Utilities.AreEquivalent(messageHash, notification.Hash)) {
				throw new InvalidMessageException();
			}

			var encryptedResult = new SymmetricEncryptionResult(
				notification.Key,
				notification.IV,
				messageBuffer);

			var plainTextBuffer = this.CryptoServices.Decrypt(encryptedResult);
			var plainTextStream = new MemoryStream(plainTextBuffer);
			var plainTextReader = new BinaryReader(plainTextStream);
			var message = Utilities.DeserializeDataContract<Payload>(plainTextReader);
			message.PayloadReferenceUri = notification.ReferenceLocation;
			return message;
		}

		/// <summary>
		/// Encrypts a message and uploads it to the cloud.
		/// </summary>
		/// <param name="message">The message being transmitted.</param>
		/// <param name="expiresUtc">The date after which the message may be destroyed.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The task whose result is a reference to the uploaded payload including decryption key.</returns>
		protected virtual async Task<PayloadReference> PostPayloadAsync(Payload message, DateTime expiresUtc, CancellationToken cancellationToken) {
			Requires.NotNull(message, "message");
			Requires.That(expiresUtc.Kind == DateTimeKind.Utc, "expiresUtc", Strings.UTCTimeRequired);
			Requires.ValidState(this.CloudBlobStorage != null, "BlobStorageProvider must not be null");

			cancellationToken.ThrowIfCancellationRequested();

			var plainTextStream = new MemoryStream();
			var writer = new BinaryWriter(plainTextStream);
			writer.SerializeDataContract(message);
			writer.Flush();
			var plainTextBuffer = plainTextStream.ToArray();
			this.Log("Message plaintext", plainTextBuffer);

			var encryptionResult = this.CryptoServices.Encrypt(plainTextBuffer);
			this.Log("Message symmetrically encrypted", encryptionResult.Ciphertext);
			this.Log("Message symmetric key", encryptionResult.Key);
			this.Log("Message symmetric IV", encryptionResult.IV);

			var messageHash = this.CryptoServices.Hash(encryptionResult.Ciphertext);
			this.Log("Encrypted message hash", messageHash);

			using (MemoryStream cipherTextStream = new MemoryStream(encryptionResult.Ciphertext)) {
				Uri blobUri = await this.CloudBlobStorage.UploadMessageAsync(cipherTextStream, expiresUtc, cancellationToken: cancellationToken);
				return new PayloadReference(blobUri, messageHash, encryptionResult.Key, encryptionResult.IV, expiresUtc);
			}
		}

		/// <summary>
		/// Shares the reference to a message payload with the specified set of recipients.
		/// </summary>
		/// <param name="messageReference">The payload reference to share.</param>
		/// <param name="recipients">The set of recipients that should be notified of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The task representing the asynchronous operation.</returns>
		protected virtual async Task PostPayloadReferenceAsync(PayloadReference messageReference, ReadOnlyCollectionOfEndpoint recipients, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(messageReference, "messageReference");
			Requires.NotNullOrEmpty(recipients, "recipients");

			// Kick off individual tasks concurrently for each recipient.
			// Each recipient requires cryptography (CPU intensive) to be performed, so don't block the calling thread.
			await TaskEx.WhenAll(
				recipients.Select(recipient => TaskEx.Run(() => this.PostPayloadReferenceAsync(messageReference, recipient, cancellationToken))));
		}

		/// <summary>
		/// Shares the reference to a message payload with the specified recipient.
		/// </summary>
		/// <param name="messageReference">The payload reference to share.</param>
		/// <param name="recipient">The recipient that should be notified of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The task representing the asynchronous operation.</returns>
		protected virtual async Task PostPayloadReferenceAsync(PayloadReference messageReference, Endpoint recipient, CancellationToken cancellationToken) {
			Requires.NotNull(recipient, "recipient");
			Requires.NotNull(messageReference, "messageReference");

			cancellationToken.ThrowIfCancellationRequested();

			// Prepare the payload.
			var plainTextPayloadStream = new MemoryStream();
			var plainTextPayloadWriter = new BinaryWriter(plainTextPayloadStream);

			// Include the intended recipient's signing certificate so the recipient knows that 
			// the message author intended the recipient to receive it (defeats fowarding and re-encrypting
			// a message notification with the intent to deceive a victim that a message was intended for them when it was not.)
			plainTextPayloadWriter.WriteSizeAndBuffer(recipient.SigningKeyPublicMaterial);

			plainTextPayloadWriter.Write(DateTime.UtcNow.ToBinary());

			// Write out the author of this notification (which may be different from the author of the 
			// message itself in the case of a "forward").
			plainTextPayloadWriter.SerializeDataContract(this.Endpoint.PublicEndpoint);

			plainTextPayloadWriter.SerializeDataContract(messageReference);
			plainTextPayloadWriter.Flush();
			this.Log("Message invite plaintext", plainTextPayloadStream.ToArray());

			byte[] notificationSignature = this.CryptoServices.Sign(plainTextPayloadStream.ToArray(), this.Endpoint.SigningKeyPrivateMaterial);
			var signedPlainTextPayloadStream = new MemoryStream((int)plainTextPayloadStream.Length + notificationSignature.Length + 4);
			await signedPlainTextPayloadStream.WriteSizeAndBufferAsync(notificationSignature, cancellationToken);
			plainTextPayloadStream.Position = 0;
			await plainTextPayloadStream.CopyToAsync(signedPlainTextPayloadStream, 4096, cancellationToken);
			var encryptedPayload = this.CryptoServices.Encrypt(signedPlainTextPayloadStream.ToArray());
			this.Log("Message invite ciphertext", encryptedPayload.Ciphertext);
			this.Log("Message invite key", encryptedPayload.Key);
			this.Log("Message invite IV", encryptedPayload.IV);

			var builder = new UriBuilder(recipient.MessageReceivingEndpoint);
			var lifetimeInMinutes = (int)(messageReference.ExpiresUtc - DateTime.UtcNow).TotalMinutes;
			builder.Query += "&lifetime=" + lifetimeInMinutes.ToString(CultureInfo.InvariantCulture);

			var postContent = new MemoryStream();
			var encryptedKey = this.CryptoServices.Encrypt(recipient.EncryptionKeyPublicMaterial, encryptedPayload.Key);
			this.Log("Message invite encrypted key", encryptedKey);
			await postContent.WriteSizeAndBufferAsync(encryptedKey, cancellationToken);
			await postContent.WriteSizeAndBufferAsync(encryptedPayload.IV, cancellationToken);
			await postContent.WriteSizeAndBufferAsync(encryptedPayload.Ciphertext, cancellationToken);
			await postContent.FlushAsync();
			postContent.Position = 0;

			using (var response = await this.HttpClient.PostAsync(builder.Uri, new StreamContent(postContent), cancellationToken)) {
				response.EnsureSuccessStatusCode();
			}
		}

		#endregion

		/// <summary>
		/// Deletes an entry from an inbox's incoming item list.
		/// </summary>
		/// <param name="payloadReferenceLocation">The URL to the downloadable <see cref="PayloadReference"/> that the inbox item to delete contains.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The task representing the asynchronous operation.</returns>
		private async Task DeletePayloadReferenceAsync(Uri payloadReferenceLocation, CancellationToken cancellationToken) {
			Requires.NotNull(payloadReferenceLocation, "payloadReferenceLocation");

			var deleteEndpoint = new UriBuilder(this.Endpoint.PublicEndpoint.MessageReceivingEndpoint);
			deleteEndpoint.Query = "notification=" + Uri.EscapeDataString(payloadReferenceLocation.AbsoluteUri);
			using (var response = await this.HttpClient.DeleteAsync(deleteEndpoint.Uri, this.Endpoint.InboxOwnerCode, cancellationToken)) {
				if (response.StatusCode == HttpStatusCode.NotFound) {
					// Good enough.
					return;
				}

				response.EnsureSuccessStatusCode();
			}
		}

		/// <summary>
		/// Downloads inbox items from the server.
		/// </summary>
		/// <param name="longPoll"><c>true</c> to asynchronously wait for messages if there are none immediately available for download.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The task whose result is the list of downloaded inbox items.</returns>
		private async Task<ReadOnlyListOfInboxItem> DownloadIncomingItemsAsync(bool longPoll, CancellationToken cancellationToken) {
			var deserializer = new DataContractJsonSerializer(typeof(IncomingList));
			var requestUri = this.Endpoint.PublicEndpoint.MessageReceivingEndpoint;
			var httpClient = this.HttpClient;
			if (longPoll) {
				requestUri = new Uri(requestUri.AbsoluteUri + "?longPoll=true");
				httpClient = this.httpClientLongPoll;
			}

			while (true) {
				try {
					var responseMessage = await httpClient.GetAsync(requestUri, this.Endpoint.InboxOwnerCode, cancellationToken);
					responseMessage.EnsureSuccessStatusCode();
					var responseStream = await responseMessage.Content.ReadAsStreamAsync();
					var inboxResults = (IncomingList)deserializer.ReadObject(responseStream);

#if NET40
					return new ReadOnlyCollection<IncomingList.IncomingItem>(inboxResults.Items);
#else
					return inboxResults.Items;
#endif
				} catch (OperationCanceledException) {
					// This can occur if the caller cancelled or if our HTTP client timed out.
					// On time-outs, we want to re-establish.  For caller cancellation, propagate it out.
					if (cancellationToken.IsCancellationRequested) {
						throw;
					}
				}
			}
		}

		/// <summary>Logs a message.</summary>
		/// <param name="caption">A description of what the contents of the <paramref name="buffer"/> are.</param>
		/// <param name="buffer">The buffer.</param>
		private void Log(string caption, byte[] buffer) {
			var logger = this.Logger;
			if (logger != null) {
				logger.WriteLine(caption, buffer);
			}
		}
	}
}
