namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Net.Http;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft;

	public class Channel {
		private HttpMessageHandler httpMessageHandler = new HttpClientHandler();
		private HttpClient httpClient;

		public Channel() {
			this.httpClient = new HttpClient(this.httpMessageHandler);
		}

		public HttpMessageHandler HttpMessageHandler {
			get { return this.httpMessageHandler; }
			set {
				this.httpMessageHandler = value;
				this.httpClient = new HttpClient(value);
			}
		}

		public ICloudBlobStorageProvider CloudBlobStorage { get; set; }

		public ICryptoProvider CryptoServices { get; set; }

		public OwnEndpoint Endpoint { get; set; }

		#region Message receiving methods

		public async Task<IReadOnlyCollection<Payload>> ReceiveAsync(IProgress<Payload> progress = null, CancellationToken cancellationToken = default(CancellationToken)) {
			var inboxItems = await this.DownloadIncomingItemsAsync(cancellationToken);

			var payloads = new List<Payload>();
			foreach (var item in inboxItems) {
				try {
					var invite = await this.DownloadPayloadReferenceAsync(item, cancellationToken);
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

			return payloads;
		}

		protected virtual async Task<PayloadReference> DownloadPayloadReferenceAsync(IncomingList.IncomingItem inboxItem, CancellationToken cancellationToken) {
			Requires.NotNull(inboxItem, "inboxItem");

			var responseMessage = await this.httpClient.GetAsync(inboxItem.Location, cancellationToken);
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

			if (!this.CryptoServices.VerifySignature(notificationAuthor.SigningKeyPublicMaterial, signedBytes, signature)) {
				throw new InvalidMessageException();
			}

			if (!Utilities.AreEquivalent(recipientPublicSigningKeyBuffer, this.Endpoint.PublicEndpoint.SigningKeyPublicMaterial)) {
				throw new InvalidMessageException(Strings.MisdirectedMessage);
			}

			return messageReference;
		}

		protected virtual async Task<Payload> DownloadPayloadAsync(PayloadReference notification, CancellationToken cancellationToken) {
			Requires.NotNull(notification, "notification");

			var responseMessage = await this.httpClient.GetAsync(notification.Location, cancellationToken);
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
			return message;
		}

		#endregion

		#region Message sending methods

		public async Task PostAsync(Payload message, IReadOnlyCollection<Endpoint> recipients, DateTime expiresUtc, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(message, "message");
			Requires.That(expiresUtc.Kind == DateTimeKind.Utc, "expiresUtc", Strings.UTCTimeRequired);
			Requires.NotNullOrEmpty(recipients, "recipients");

			var payloadReference = await this.PostPayloadAsync(message, expiresUtc, cancellationToken);
			await this.PostPayloadReferenceAsync(payloadReference, recipients, cancellationToken);
		}

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
				Uri blobUri = await this.CloudBlobStorage.UploadMessageAsync(cipherTextStream, expiresUtc, cancellationToken);
				return new PayloadReference(blobUri, messageHash, encryptionResult.Key, encryptionResult.IV, expiresUtc, message.ContentType);
			}
		}

		protected virtual async Task PostPayloadReferenceAsync(PayloadReference messageReference, IReadOnlyCollection<Endpoint> recipients, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(messageReference, "messageReference");
			Requires.NotNullOrEmpty(recipients, "recipients");

			// Kick off individual tasks concurrently for each recipient.
			// Each recipient requires cryptography (CPU intensive) to be performed, so don't block the calling thread.
			await Task.WhenAll(
				recipients.Select(recipient => Task.Run(() => this.PostPayloadReferenceAsync(messageReference, recipient, cancellationToken))));
		}

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
			//string base64HashOfInvite = Utilities.ToBase64WebSafe(this.Hash(postContent.ToArray()));

			using (var response = await this.httpClient.PostAsync(builder.Uri, new StreamContent(postContent), cancellationToken)) {
				response.EnsureSuccessStatusCode();
			}
		}

		#endregion

		private async Task<IReadOnlyList<IncomingList.IncomingItem>> DownloadIncomingItemsAsync(CancellationToken cancellationToken) {
			var deserializer = new DataContractJsonSerializer(typeof(IncomingList));
			var messages = new List<Payload>();
			var responseMessage = await this.httpClient.GetAsync(this.Endpoint.PublicEndpoint.MessageReceivingEndpoint, cancellationToken);
			var responseStream = await responseMessage.Content.ReadAsStreamAsync();
			var inboxResults = (IncomingList)deserializer.ReadObject(responseStream);
			return inboxResults.Items;
		}

		public ILogger Logger { get; set; }

		private void Log(string caption, byte[] buffer) {
			var logger = this.Logger;
			if (logger != null) {
				logger.WriteLine(caption, buffer);
			}
		}
	}
}
