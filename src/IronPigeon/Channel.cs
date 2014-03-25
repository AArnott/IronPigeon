namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Composition;
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
	using PCLCrypto;
	using Validation;

	/// <summary>
	/// A channel for sending or receiving secure messages.
	/// </summary>
	public class Channel {
		/// <summary>
		/// A cache of identifiers and their resolved endpoints.
		/// </summary>
		private readonly Dictionary<string, Endpoint> resolvedIdentifiersCache =
			new Dictionary<string, Endpoint>();

		/// <summary>
		/// The HTTP client to use for long poll HTTP requests.
		/// </summary>
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private HttpClient httpClientLongPoll;

		/// <summary>
		/// Initializes a new instance of the <see cref="Channel" /> class.
		/// </summary>
		public Channel() {
		}

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
		public CryptoSettings CryptoServices { get; set; }

		/// <summary>
		/// Gets or sets the endpoint used to receive messages.
		/// </summary>
		/// <value>
		/// The endpoint.
		/// </value>
		public OwnEndpoint Endpoint { get; set; }

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

		/// <summary>
		/// Gets or sets the set of address books to use when verifying claimed identifiers on received messages.
		/// </summary>
		[ImportMany]
		public IList<AddressBook> AddressBooks { get; set; }

		/// <summary>
		/// Downloads messages from the server.
		/// </summary>
		/// <param name="longPoll"><c>true</c> to asynchronously wait for messages if there are none immediately available for download.</param>
		/// <param name="progress">A callback that receives messages as they are retrieved.</param>
		/// <param name="cancellationToken">A token whose cancellation signals lost interest in the result of this method.</param>
		/// <returns>A collection of all messages that were waiting at the time this method was invoked.</returns>
		/// <exception cref="HttpRequestException">Thrown when a connection to the server could not be established, or was terminated.</exception>
		public async Task<IReadOnlyList<PayloadReceipt>> ReceiveAsync(bool longPoll = false, IProgress<PayloadReceipt> progress = null, CancellationToken cancellationToken = default(CancellationToken)) {
			var inboxItems = await this.DownloadIncomingItemsAsync(longPoll, cancellationToken);

			var payloads = new List<PayloadReceipt>();
			foreach (var item in inboxItems) {
				try {
					try {
						var invite = await this.DownloadPayloadReferenceAsync(item, cancellationToken);
						if (invite == null) {
							continue;
						}

						var message = await this.DownloadPayloadAsync(invite, cancellationToken);
						var receipt = new PayloadReceipt(message, item.DatePostedUtc);
						payloads.Add(receipt);
						if (progress != null) {
							progress.Report(receipt);
						}
					} catch (SerializationException ex) {
						throw new InvalidMessageException(Strings.InvalidMessage, ex);
					} catch (DecoderFallbackException ex) {
						throw new InvalidMessageException(Strings.InvalidMessage, ex);
					} catch (OverflowException ex) {
						throw new InvalidMessageException(Strings.InvalidMessage, ex);
					} catch (OutOfMemoryException ex) {
						throw new InvalidMessageException(Strings.InvalidMessage, ex);
					} catch (InvalidMessageException) {
						throw;
					} catch (Exception ex) {
						// all those platform-specific exceptions that aren't available to portable libraries.
						throw new InvalidMessageException(Strings.InvalidMessage, ex);
					}
				} catch (InvalidMessageException ex) {
					Debug.WriteLine(ex);

					// Delete the payload reference since it was an invalid message.
					var nowait = this.DeletePayloadReferenceAsync(item.Location, cancellationToken);
				}
			}

			return payloads;
		}

		/// <summary>
		/// Sends some payload to a set of recipients.
		/// </summary>
		/// <param name="message">The payload to transmit.</param>
		/// <param name="recipients">The recipients to receive the message.</param>
		/// <param name="expiresUtc">The date after which the message may be destroyed.</param>
		/// <param name="bytesCopiedProgress">Progress in terms of bytes copied.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The task representing the asynchronous operation.</returns>
		public async Task<IReadOnlyCollection<NotificationPostedReceipt>> PostAsync(Payload message, IReadOnlyCollection<Endpoint> recipients, DateTime expiresUtc, IProgress<int> bytesCopiedProgress = null, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(message, "message");
			Requires.That(expiresUtc.Kind == DateTimeKind.Utc, "expiresUtc", Strings.UTCTimeRequired);
			Requires.NotNullOrEmpty(recipients, "recipients");

			var payloadReference = await this.PostPayloadAsync(message, expiresUtc, bytesCopiedProgress, cancellationToken);
			return await this.PostPayloadReferenceAsync(payloadReference, recipients, cancellationToken);
		}

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

		/// <summary>
		/// Gets the set of identifiers this endpoint claims that are verifiable.
		/// </summary>
		/// <param name="endpoint">The endpoint whose authorized identifiers are to be verified.</param>
		/// <param name="cancellationToken">A general cancellation token on the request.</param>
		/// <returns>A task whose result is the set of verified identifiers.</returns>
		public async Task<IReadOnlyCollection<string>> GetVerifiableIdentifiersAsync(Endpoint endpoint, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(endpoint, "endpoint");

			var verifiedIdentifiers = new List<string>();
			if (endpoint.AuthorizedIdentifiers != null) {
				var map = endpoint.AuthorizedIdentifiers.Where(id => id != null).ToDictionary(
					id => id,
					id => this.IsVerifiableIdentifierAsync(endpoint, id, cancellationToken));
				await Task.WhenAll(map.Values);
				foreach (var result in map) {
					if (result.Value.Result) {
						verifiedIdentifiers.Add(result.Key);
					}
				}
			}

			return verifiedIdentifiers;
		}

		/// <summary>
		/// Encrypts a message and uploads it to the cloud.
		/// </summary>
		/// <param name="message">The message being transmitted.</param>
		/// <param name="expiresUtc">The date after which the message may be destroyed.</param>
		/// <param name="bytesCopiedProgress">Receives progress in terms of number of bytes uploaded.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The task whose result is a reference to the uploaded payload including decryption key.</returns>
		public virtual async Task<PayloadReference> PostPayloadAsync(Payload message, DateTime expiresUtc, IProgress<int> bytesCopiedProgress = null, CancellationToken cancellationToken = default(CancellationToken)) {
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

			plainTextStream.Position = 0;
			var cipherTextStream = new MemoryStream();
			var encryptionVariables = await this.CryptoServices.EncryptAsync(plainTextStream, cipherTextStream, cancellationToken: cancellationToken);
			this.Log("Message symmetrically encrypted", cipherTextStream.ToArray());
			this.Log("Message symmetric key", encryptionVariables.Key);
			this.Log("Message symmetric IV", encryptionVariables.IV);

			cipherTextStream.Position = 0;
			var hasher = this.CryptoServices.GetHashAlgorithm();
			var messageHash = hasher.HashData(cipherTextStream.ToArray());
			this.Log("Encrypted message hash", messageHash);

			cipherTextStream.Position = 0;
			Uri blobUri = await this.CloudBlobStorage.UploadMessageAsync(cipherTextStream, expiresUtc, contentType: message.ContentType, bytesCopiedProgress: bytesCopiedProgress, cancellationToken: cancellationToken);
			return new PayloadReference(blobUri, messageHash, this.CryptoServices.SymmetricHashAlgorithmName, encryptionVariables.Key, encryptionVariables.IV, expiresUtc);
		}

		/// <summary>
		/// Downloads the message payload referred to by the specified <see cref="PayloadReference"/>.
		/// </summary>
		/// <param name="notification">The payload reference.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The task representing the asynchronous operation.</returns>
		public virtual async Task<Payload> DownloadPayloadAsync(PayloadReference notification, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(notification, "notification");

			var responseMessage = await this.HttpClient.GetAsync(notification.Location, cancellationToken);
			var messageBuffer = await responseMessage.Content.ReadAsByteArrayAsync();

			// Calculate hash of downloaded message and check that it matches the referenced message hash.
			if (!this.CryptoServices.IsHashMatchWithTolerantHashAlgorithm(messageBuffer, notification.Hash, notification.HashAlgorithmName)) {
				throw new InvalidMessageException();
			}

			var encryptionVariables = new SymmetricEncryptionVariables(notification.Key, notification.IV);

			var cipherStream = new MemoryStream(messageBuffer);
			var plainTextStream = new MemoryStream();
			await this.CryptoServices.DecryptAsync(cipherStream, plainTextStream, encryptionVariables, cancellationToken);
			plainTextStream.Position = 0;
			var plainTextReader = new BinaryReader(plainTextStream);
			var message = Utilities.DeserializeDataContract<Payload>(plainTextReader);
			message.PayloadReferenceUri = notification.ReferenceLocation;
			return message;
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
			var ownDecryptionKey = WinRTCrypto.AsymmetricKeyAlgorithmProvider.OpenAlgorithm(this.CryptoServices.EncryptionAlgorithm)
				.ImportKeyPair(this.Endpoint.EncryptionKeyPrivateMaterial, CryptographicPrivateKeyBlobType.Capi1PrivateKey);
			var key = WinRTCrypto.CryptographicEngine.Decrypt(ownDecryptionKey, encryptedKey);
			var iv = await responseStreamCopy.ReadSizeAndBufferAsync(cancellationToken);
			var ciphertextStream = await responseStreamCopy.ReadSizeAndStreamAsync(cancellationToken);
			var encryptedVariables = new SymmetricEncryptionVariables(key, iv);

			var plainTextPayloadStream = new MemoryStream();
			await this.CryptoServices.DecryptAsync(ciphertextStream, plainTextPayloadStream, encryptedVariables, cancellationToken);

			plainTextPayloadStream.Position = 0;
			AsymmetricAlgorithm? signingHashAlgorithm = null; //// Encoding.UTF8.GetString(await plainTextPayloadStream.ReadSizeAndBufferAsync(cancellationToken));
			byte[] signature = await plainTextPayloadStream.ReadSizeAndBufferAsync(cancellationToken);
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
			if (messageReference.HashAlgorithmName == null) {
				messageReference.HashAlgorithmName = Utilities.GuessHashAlgorithmFromLength(messageReference.Hash.Length);
			}

			if (!this.CryptoServices.VerifySignatureWithTolerantHashAlgorithm(notificationAuthor.SigningKeyPublicMaterial, signedBytes, signature, signingHashAlgorithm)) {
				throw new InvalidMessageException();
			}

			if (!Utilities.AreEquivalent(recipientPublicSigningKeyBuffer, this.Endpoint.PublicEndpoint.SigningKeyPublicMaterial)) {
				throw new InvalidMessageException(Strings.MisdirectedMessage);
			}

			return messageReference;
		}

		/// <summary>
		/// Shares the reference to a message payload with the specified set of recipients.
		/// </summary>
		/// <param name="messageReference">The payload reference to share.</param>
		/// <param name="recipients">The set of recipients that should be notified of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The task representing the asynchronous operation.</returns>
		protected virtual async Task<IReadOnlyCollection<NotificationPostedReceipt>> PostPayloadReferenceAsync(PayloadReference messageReference, IReadOnlyCollection<Endpoint> recipients, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(messageReference, "messageReference");
			Requires.NotNullOrEmpty(recipients, "recipients");

			// Kick off individual tasks concurrently for each recipient.
			// Each recipient requires cryptography (CPU intensive) to be performed, so don't block the calling thread.
			var postTasks = recipients.Select(recipient => Task.Run(() => this.PostPayloadReferenceAsync(messageReference, recipient, cancellationToken))).ToList();
			return await Task.WhenAll(postTasks);
		}

		/// <summary>
		/// Shares the reference to a message payload with the specified recipient.
		/// </summary>
		/// <param name="messageReference">The payload reference to share.</param>
		/// <param name="recipient">The recipient that should be notified of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The task representing the asynchronous operation.</returns>
		protected virtual async Task<NotificationPostedReceipt> PostPayloadReferenceAsync(PayloadReference messageReference, Endpoint recipient, CancellationToken cancellationToken) {
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

			var signingKey = WinRTCrypto.AsymmetricKeyAlgorithmProvider.OpenAlgorithm(this.CryptoServices.SigningAlgorithm)
				.ImportKeyPair(this.Endpoint.SigningKeyPrivateMaterial);
			byte[] notificationSignature = WinRTCrypto.CryptographicEngine.Sign(signingKey, plainTextPayloadStream.ToArray());
			var signedPlainTextPayloadStream = new MemoryStream((int)plainTextPayloadStream.Length + notificationSignature.Length + 4);
			////await signedPlainTextPayloadStream.WriteSizeAndBufferAsync(Encoding.UTF8.GetBytes(this.CryptoServices.HashAlgorithmName), cancellationToken);
			await signedPlainTextPayloadStream.WriteSizeAndBufferAsync(notificationSignature, cancellationToken);
			plainTextPayloadStream.Position = 0;
			await plainTextPayloadStream.CopyToAsync(signedPlainTextPayloadStream, 4096, cancellationToken);
			signedPlainTextPayloadStream.Position = 0;
			var cipherTextStream = new MemoryStream();
			var encryptedVariables = await this.CryptoServices.EncryptAsync(signedPlainTextPayloadStream, cipherTextStream, cancellationToken: cancellationToken);
			this.Log("Message invite ciphertext", cipherTextStream.ToArray());
			this.Log("Message invite key", encryptedVariables.Key);
			this.Log("Message invite IV", encryptedVariables.IV);

			var builder = new UriBuilder(recipient.MessageReceivingEndpoint);
			var lifetimeInMinutes = (int)(messageReference.ExpiresUtc - DateTime.UtcNow).TotalMinutes;
			builder.Query += "&lifetime=" + lifetimeInMinutes.ToString(CultureInfo.InvariantCulture);

			var postContent = new MemoryStream();
			var encryptionKey = WinRTCrypto.AsymmetricKeyAlgorithmProvider.OpenAlgorithm(this.CryptoServices.EncryptionAlgorithm)
				.ImportPublicKey(recipient.EncryptionKeyPublicMaterial);
			var encryptedKey = WinRTCrypto.CryptographicEngine.Encrypt(encryptionKey, encryptedVariables.Key);
			this.Log("Message invite encrypted key", encryptedKey);
			await postContent.WriteSizeAndBufferAsync(encryptedKey, cancellationToken);
			await postContent.WriteSizeAndBufferAsync(encryptedVariables.IV, cancellationToken);
			cipherTextStream.Position = 0;
			await postContent.WriteSizeAndStreamAsync(cipherTextStream, cancellationToken);
			await postContent.FlushAsync();
			postContent.Position = 0;

			using (var response = await this.HttpClient.PostAsync(builder.Uri, new StreamContent(postContent), cancellationToken)) {
				if (response.Content != null) {
					// Just to help in debugging.
					string responseContent = await response.Content.ReadAsStringAsync();
				}

				response.EnsureSuccessStatusCode();
				var receipt = new NotificationPostedReceipt(recipient, response.Headers.Date);
				return receipt;
			}
		}

		#endregion

		/// <summary>
		/// Checks whether the specified identifier yields an endpoint equivalent to this one.
		/// </summary>
		/// <param name="claimingEndpoint">The endpoint that claims to be resolvable from a given identifier.</param>
		/// <param name="claimedIdentifier">The identifier to check.</param>
		/// <param name="cancellationToken">A general cancellation token on the request.</param>
		/// <returns>A task whose result is <c>true</c> if the identifier verified correctly; otherwise <c>false</c>.</returns>
		private async Task<bool> IsVerifiableIdentifierAsync(Endpoint claimingEndpoint, string claimedIdentifier, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(claimingEndpoint, "claimingEndpoint");
			Requires.NotNullOrEmpty(claimedIdentifier, "claimedIdentifier");

			Endpoint cachedEndpoint;
			lock (this.resolvedIdentifiersCache) {
				if (this.resolvedIdentifiersCache.TryGetValue(claimedIdentifier, out cachedEndpoint)) {
					return cachedEndpoint.Equals(claimingEndpoint);
				}
			}

			var matchingEndpoint = await Utilities.FastestQualifyingResultAsync(
				this.AddressBooks,
				(ct, addressBook) => addressBook.LookupAsync(claimedIdentifier, ct),
				resolvedEndpoint => claimingEndpoint.Equals(resolvedEndpoint),
				cancellationToken);

			if (matchingEndpoint != null) {
				lock (this.resolvedIdentifiersCache) {
					if (!this.resolvedIdentifiersCache.ContainsKey(claimedIdentifier)) {
						this.resolvedIdentifiersCache.Add(claimedIdentifier, matchingEndpoint);
					}
				}
			}

			return matchingEndpoint != null;
		}

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
		private async Task<IReadOnlyList<IncomingList.IncomingItem>> DownloadIncomingItemsAsync(bool longPoll, CancellationToken cancellationToken) {
			var deserializer = new DataContractJsonSerializer(typeof(IncomingList));
			var requestUri = this.Endpoint.PublicEndpoint.MessageReceivingEndpoint;
			var httpClient = this.HttpClient;
			if (longPoll) {
				requestUri = new Uri(requestUri.AbsoluteUri + "?longPoll=true");
				httpClient = this.httpClientLongPoll;
			}

			var responseMessage = await httpClient.GetAsync(requestUri, this.Endpoint.InboxOwnerCode, cancellationToken);
			responseMessage.EnsureSuccessStatusCode();
			var responseStream = await responseMessage.Content.ReadAsStreamAsync();
			var inboxResults = (IncomingList)deserializer.ReadObject(responseStream);

			return inboxResults.Items;
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

		/// <summary>
		/// A message payload and the time notification of it was received by the cloud inbox.
		/// </summary>
		public class PayloadReceipt {
			/// <summary>
			/// Initializes a new instance of the <see cref="PayloadReceipt"/> class.
			/// </summary>
			/// <param name="payload">The payload itself.</param>
			/// <param name="dateNotificationPosted">The date the cloud inbox received notification of the payload.</param>
			public PayloadReceipt(Payload payload, DateTimeOffset dateNotificationPosted) {
				Requires.NotNull(payload, "payload");
				this.Payload = payload;
				this.DateNotificationPosted = dateNotificationPosted;
			}

			/// <summary>
			/// Gets the payload itself.
			/// </summary>
			public Payload Payload { get; private set; }

			/// <summary>
			/// Gets the time the cloud inbox received notification of the payload.
			/// </summary>
			public DateTimeOffset DateNotificationPosted { get; private set; }
		}

		/// <summary>
		/// The result of posting a message notification to a cloud inbox.
		/// </summary>
		public class NotificationPostedReceipt {
			/// <summary>
			/// Initializes a new instance of the <see cref="NotificationPostedReceipt"/> class.
			/// </summary>
			/// <param name="recipient">The inbox that received the notification.</param>
			/// <param name="cloudInboxReceiptTimestamp">The timestamp included in the HTTP response from the server.</param>
			public NotificationPostedReceipt(Endpoint recipient, DateTimeOffset? cloudInboxReceiptTimestamp) {
				Requires.NotNull(recipient, "recipient");

				this.Recipient = recipient;
				this.CloudInboxReceiptTimestamp = cloudInboxReceiptTimestamp;
			}

			/// <summary>
			/// Gets the receiver of the notification.
			/// </summary>
			public Endpoint Recipient { get; private set; }

			/// <summary>
			/// Gets the timestamp the receiving cloud inbox returned after receiving the notification.
			/// </summary>
			public DateTimeOffset? CloudInboxReceiptTimestamp { get; private set; }
		}
	}
}
