// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.ExceptionServices;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Json;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft;
    using PCLCrypto;

    /// <summary>
    /// A channel for sending or receiving secure messages.
    /// </summary>
    public class Channel
    {
        /// <summary>
        /// A cache of identifiers and their resolved endpoints.
        /// </summary>
        private readonly Dictionary<string, Endpoint> resolvedIdentifiersCache =
            new Dictionary<string, Endpoint>();

        /// <summary>
        /// The HTTP client to use for long poll HTTP requests.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private HttpClient? httpClientLongPoll;

        /// <summary>
        /// Initializes a new instance of the <see cref="Channel" /> class.
        /// </summary>
        public Channel()
        {
            this.CryptoServices = new CryptoSettings();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Channel"/> class.
        /// </summary>
        /// <param name="cloudBlobStorage">The cloud blob storage.</param>
        /// <param name="httpClient">The HTTP client.</param>
        /// <param name="httpClientLongPoll">The HTTP client long poll.</param>
        /// <param name="addressBooks">The address books.</param>
        public Channel(ICloudBlobStorageProvider cloudBlobStorage, HttpClient httpClient, HttpClient httpClientLongPoll, IEnumerable<AddressBook> addressBooks)
            : this()
        {
            this.CloudBlobStorage = cloudBlobStorage;
            this.HttpClient = httpClient;
            this.HttpClientLongPoll = httpClientLongPoll;
            this.AddressBooks = addressBooks != null ? addressBooks.ToList() : null;
        }

        /// <summary>
        /// Gets or sets the provider of blob storage.
        /// </summary>
        public ICloudBlobStorageProvider? CloudBlobStorage { get; set; }

        /// <summary>
        /// Gets or sets the provider for cryptographic operations.
        /// </summary>
        /// <value>
        /// The crypto services.
        /// </value>
        public CryptoSettings CryptoServices { get; set; }

        /// <summary>
        /// Gets or sets the endpoint used to receive messages.
        /// </summary>
        /// <value>
        /// The endpoint.
        /// </value>
        public OwnEndpoint? Endpoint { get; set; }

        /// <summary>
        /// Gets or sets the trace source to which messages are logged.
        /// </summary>
        public TraceSource? TraceSource { get; set; }

        /// <summary>
        /// Gets or sets the HTTP client used for outbound HTTP requests.
        /// </summary>
        public HttpClient? HttpClient { get; set; }

        /// <summary>
        /// Gets or sets the HTTP client to use for long poll HTTP requests.
        /// </summary>
        public HttpClient? HttpClientLongPoll
        {
            get
            {
                return this.httpClientLongPoll;
            }

            set
            {
                if (value is object)
                {
                    value.Timeout = TimeSpan.FromMilliseconds(Timeout.Infinite);
                }

                this.httpClientLongPoll = value;
            }
        }

        /// <summary>
        /// Gets or sets the set of address books to use when verifying claimed identifiers on received messages.
        /// </summary>
        public IList<AddressBook>? AddressBooks { get; set; }

        /// <summary>
        /// Downloads messages from the server.
        /// </summary>
        /// <param name="longPoll"><c>true</c> to asynchronously wait for messages if there are none immediately available for download.</param>
        /// <param name="progress">A callback that receives messages as they are retrieved.</param>
        /// <param name="cancellationToken">A token whose cancellation signals lost interest in the result of this method.</param>
        /// <returns>A collection of all messages that were waiting at the time this method was invoked.</returns>
        /// <exception cref="HttpRequestException">Thrown when a connection to the server could not be established, or was terminated.</exception>
        public async Task<IReadOnlyList<PayloadReceipt>> ReceiveAsync(bool longPoll = false, IProgress<PayloadReceipt>? progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            IReadOnlyList<IncomingList.IncomingItem>? inboxItems = await this.DownloadIncomingItemsAsync(longPoll, cancellationToken).ConfigureAwait(false);

            var payloads = new List<PayloadReceipt>();
            foreach (IncomingList.IncomingItem? item in inboxItems)
            {
                try
                {
                    try
                    {
                        PayloadReference? invite = await this.DownloadPayloadReferenceAsync(item, cancellationToken).ConfigureAwait(false);
                        if (invite == null)
                        {
                            continue;
                        }

                        Payload? message = await this.DownloadPayloadAsync(invite, cancellationToken).ConfigureAwait(false);
                        var receipt = new PayloadReceipt(message, item.DatePostedUtc);
                        payloads.Add(receipt);
                        if (progress != null)
                        {
                            progress.Report(receipt);
                        }
                    }
                    catch (SerializationException ex)
                    {
                        throw new InvalidMessageException(Strings.InvalidMessage, ex);
                    }
                    catch (DecoderFallbackException ex)
                    {
                        throw new InvalidMessageException(Strings.InvalidMessage, ex);
                    }
                    catch (OverflowException ex)
                    {
                        throw new InvalidMessageException(Strings.InvalidMessage, ex);
                    }
                    catch (OutOfMemoryException ex)
                    {
                        throw new InvalidMessageException(Strings.InvalidMessage, ex);
                    }
                    catch (InvalidMessageException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // all those platform-specific exceptions that aren't available to portable libraries.
                        throw new InvalidMessageException(Strings.InvalidMessage, ex);
                    }
                }
                catch (InvalidMessageException ex)
                {
                    Debug.WriteLine(ex);

                    // Delete the payload reference since it was an invalid message.
                    Task? nowait = this.DeletePayloadReferenceAsync(item.Location, cancellationToken);
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
        public async Task<IReadOnlyCollection<NotificationPostedReceipt>> PostAsync(Payload message, IReadOnlyCollection<Endpoint> recipients, DateTime expiresUtc, IProgress<long>? bytesCopiedProgress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(message, nameof(message));
            Requires.Argument(expiresUtc.Kind == DateTimeKind.Utc, nameof(expiresUtc), Strings.UTCTimeRequired);
            Requires.NotNullOrEmpty(recipients, nameof(recipients));

            PayloadReference? payloadReference = await this.PostPayloadAsync(message, expiresUtc, bytesCopiedProgress, cancellationToken).ConfigureAwait(false);
            return await this.PostPayloadReferenceAsync(payloadReference, recipients, cancellationToken).ConfigureAwait(false);
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
        public Task DeleteInboxItemAsync(Payload payload, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(payload, nameof(payload));
            Requires.Argument(payload.PayloadReferenceUri != null, "payload", "Original payload reference URI no longer available.");

            return this.DeletePayloadReferenceAsync(payload.PayloadReferenceUri, cancellationToken);
        }

        /// <summary>
        /// Gets the set of identifiers this endpoint claims that are verifiable.
        /// </summary>
        /// <param name="endpoint">The endpoint whose authorized identifiers are to be verified.</param>
        /// <param name="cancellationToken">A general cancellation token on the request.</param>
        /// <returns>A task whose result is the set of verified identifiers.</returns>
        public async Task<IReadOnlyCollection<string>> GetVerifiableIdentifiersAsync(Endpoint endpoint, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(endpoint, nameof(endpoint));

            var verifiedIdentifiers = new List<string>();
            if (endpoint.AuthorizedIdentifiers != null)
            {
                var map = endpoint.AuthorizedIdentifiers.Where(id => id != null).ToDictionary(
                    id => id,
                    id => this.IsVerifiableIdentifierAsync(endpoint, id, cancellationToken));
                await Task.WhenAll(map.Values).ConfigureAwait(false);
                foreach (KeyValuePair<string, Task<bool>> result in map)
                {
                    if (await result.Value.ConfigureAwait(false))
                    {
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
        public virtual async Task<PayloadReference> PostPayloadAsync(Payload message, DateTime expiresUtc, IProgress<long>? bytesCopiedProgress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(message, nameof(message));
            Requires.Argument(expiresUtc.Kind == DateTimeKind.Utc, nameof(expiresUtc), Strings.UTCTimeRequired);
            Verify.Operation(this.CloudBlobStorage != null, "{0} must not be null", nameof(this.CloudBlobStorage));

            cancellationToken.ThrowIfCancellationRequested();

            var plainTextStream = new MemoryStream();
            using var writer = new BinaryWriter(plainTextStream);
            writer.SerializeDataContract(message);
            writer.Flush();
            var plainTextBuffer = plainTextStream.ToArray();
            this.Log("Message plaintext", plainTextBuffer);

            plainTextStream.Position = 0;
            var cipherTextStream = new MemoryStream();
            SymmetricEncryptionVariables? encryptionVariables = await this.CryptoServices.EncryptAsync(plainTextStream, cipherTextStream, cancellationToken: cancellationToken).ConfigureAwait(false);
            this.Log("Message symmetrically encrypted", cipherTextStream.ToArray());
            this.Log("Message symmetric key", encryptionVariables.Key);
            this.Log("Message symmetric IV", encryptionVariables.IV);

            cipherTextStream.Position = 0;
            IHashAlgorithmProvider? hasher = WinRTCrypto.HashAlgorithmProvider.OpenAlgorithm(this.CryptoServices.SymmetricHashAlgorithm);
            var messageHash = hasher.HashData(cipherTextStream.ToArray());
            this.Log("Encrypted message hash", messageHash);

            cipherTextStream.Position = 0;
            Uri blobUri = await this.CloudBlobStorage.UploadMessageAsync(cipherTextStream, expiresUtc, contentType: message.ContentType, bytesCopiedProgress: bytesCopiedProgress, cancellationToken: cancellationToken).ConfigureAwait(false);
            return new PayloadReference(blobUri, messageHash, this.CryptoServices.SymmetricHashAlgorithm.GetHashAlgorithmName(), encryptionVariables.Key, encryptionVariables.IV, expiresUtc);
        }

        /// <summary>
        /// Downloads the message payload referred to by the specified <see cref="PayloadReference"/>.
        /// </summary>
        /// <param name="notification">The payload reference.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task representing the asynchronous operation.</returns>
        public virtual async Task<Payload> DownloadPayloadAsync(PayloadReference notification, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(notification, nameof(notification));
            Verify.Operation(this.HttpClient is object, Strings.PropertyMustBeSetFirst, nameof(this.HttpClient));

            byte[]? messageBuffer = null;
            const int MaxAttempts = 2;
            int retry;
            Exception? exceptionLeadingToRetry = null;
            for (retry = 0; retry < MaxAttempts; retry++)
            {
                exceptionLeadingToRetry = null;
                try
                {
                    HttpResponseMessage? responseMessage = await this.HttpClient.GetAsync(notification.Location, cancellationToken).ConfigureAwait(false);
                    messageBuffer = await responseMessage.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                    // Calculate hash of downloaded message and check that it matches the referenced message hash.
                    if (!this.CryptoServices.IsHashMatchWithTolerantHashAlgorithm(messageBuffer, notification.Hash, CryptoProviderExtensions.ParseHashAlgorithmName(notification.HashAlgorithmName)))
                    {
                        // Sometimes when the hash mismatches, it's because the download was truncated
                        // (from switching out the application and then switching back, for example).
                        // Check for that before throwing.
                        if (responseMessage.Content.Headers.ContentLength.HasValue && responseMessage.Content.Headers.ContentLength.Value > messageBuffer.Length)
                        {
                            // It looks like the message was truncated. Retry.
                            exceptionLeadingToRetry = new InvalidMessageException();
                            continue;
                        }

                        throw new InvalidMessageException();
                    }

                    // Stop retrying. We got something that worked!
                    break;
                }
                catch (HttpRequestException ex)
                {
                    exceptionLeadingToRetry = ex;
                    continue;
                }
            }

            if (exceptionLeadingToRetry != null)
            {
                if (exceptionLeadingToRetry.StackTrace != null)
                {
                    ExceptionDispatchInfo.Capture(exceptionLeadingToRetry).Throw();
                }
                else
                {
                    throw exceptionLeadingToRetry;
                }
            }

            var encryptionVariables = new SymmetricEncryptionVariables(notification.Key, notification.IV);

            using var cipherStream = new MemoryStream(messageBuffer);
            var plainTextStream = new MemoryStream();
            await this.CryptoServices.DecryptAsync(cipherStream, plainTextStream, encryptionVariables, cancellationToken).ConfigureAwait(false);
            plainTextStream.Position = 0;
            using var plainTextReader = new BinaryReader(plainTextStream);
            Payload? message = Utilities.DeserializeDataContract<Payload>(plainTextReader);
            message.PayloadReferenceUri = notification.ReferenceLocation;
            return message;
        }

        /// <summary>
        /// Registers an Android application to receive push notifications for incoming messages.
        /// </summary>
        /// <param name="googlePlayRegistrationId">The Google Cloud Messaging registration identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// A task representing the async operation.
        /// </returns>
        public async Task RegisterGooglePlayPushNotificationAsync(string googlePlayRegistrationId, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNullOrEmpty(googlePlayRegistrationId, nameof(googlePlayRegistrationId));
            Verify.Operation(this.Endpoint is object, Strings.PropertyMustBeSetFirst, nameof(this.Endpoint));
            Verify.Operation(this.HttpClient is object, Strings.PropertyMustBeSetFirst, nameof(this.HttpClient));

            using var request = new HttpRequestMessage(HttpMethod.Put, this.Endpoint.PublicEndpoint.MessageReceivingEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.Endpoint.InboxOwnerCode);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "gcm_registration_id", googlePlayRegistrationId },
            });
            HttpResponseMessage? response = await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Registers an iOS application to receive push notifications for incoming messages.
        /// </summary>
        /// <param name="deviceToken">The Apple-assigned device token to use from the cloud to reach this device.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// A task representing the async operation.
        /// </returns>
        public async Task RegisterApplePushNotificationAsync(string deviceToken, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNullOrEmpty(deviceToken, nameof(deviceToken));
            Verify.Operation(this.Endpoint != null, "Endpoint must be set first.");

            var request = new HttpRequestMessage(HttpMethod.Put, this.Endpoint.PublicEndpoint.MessageReceivingEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.Endpoint.InboxOwnerCode);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "ios_device_token", deviceToken },
            });
            HttpResponseMessage? response = await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Registers a Windows 8 application to receive push notifications for incoming messages.
        /// </summary>
        /// <param name="pushNotificationChannelUri">The push notification channel's ChannelUri property.</param>
        /// <param name="pushContent">Content of the push.</param>
        /// <param name="toastLine1">The first line in the toast notification to send.</param>
        /// <param name="toastLine2">The second line in the toast notification to send.</param>
        /// <param name="tileTemplate">The tile template used by the client app.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// A task representing the async operation.
        /// </returns>
        public async Task RegisterWinPhonePushNotificationAsync(Uri pushNotificationChannelUri, string? pushContent = null, string? toastLine1 = null, string? toastLine2 = null, string? tileTemplate = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(pushNotificationChannelUri, nameof(pushNotificationChannelUri));
            Verify.Operation(this.Endpoint is object, Strings.PropertyMustBeSetFirst, nameof(this.Endpoint));
            Verify.Operation(this.HttpClient is object, Strings.PropertyMustBeSetFirst, nameof(this.HttpClient));

            using var request = new HttpRequestMessage(HttpMethod.Put, this.Endpoint.PublicEndpoint.MessageReceivingEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.Endpoint.InboxOwnerCode);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "wp8_channel_uri", pushNotificationChannelUri.AbsoluteUri },
                { "wp8_channel_content", pushContent ?? string.Empty },
                { "wp8_channel_toast_text1", toastLine1 ?? string.Empty },
                { "wp8_channel_toast_text2", toastLine2 ?? string.Empty },
                { "wp8_channel_tile_template", tileTemplate ?? string.Empty },
            });
            HttpResponseMessage? response = await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Registers a Windows 8 application to receive push notifications for incoming messages.
        /// </summary>
        /// <param name="packageSecurityIdentifier">The package security identifier of the app.</param>
        /// <param name="pushNotificationChannelUri">The push notification channel.</param>
        /// <param name="channelExpiration">When the channel will expire.</param>
        /// <param name="pushContent">Content of the push.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the async operation.</returns>
        public async Task RegisterWindowsPushNotificationChannelAsync(string packageSecurityIdentifier, Uri pushNotificationChannelUri, DateTime channelExpiration, string pushContent, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(pushNotificationChannelUri, nameof(pushNotificationChannelUri));
            Requires.NotNullOrEmpty(packageSecurityIdentifier, nameof(packageSecurityIdentifier));

            using var request = new HttpRequestMessage(HttpMethod.Put, this.Endpoint.PublicEndpoint.MessageReceivingEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.Endpoint.InboxOwnerCode);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "package_security_identifier", packageSecurityIdentifier },
                { "channel_uri", pushNotificationChannelUri.AbsoluteUri },
                { "channel_content", pushContent ?? string.Empty },
                { "expiration", channelExpiration.ToString(CultureInfo.InvariantCulture) },
            });
            HttpResponseMessage? response = await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Downloads a <see cref="PayloadReference"/> that is referenced from an incoming inbox item.
        /// </summary>
        /// <param name="inboxItem">The inbox item that referenced the <see cref="PayloadReference"/>.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task representing the asynchronous operation.</returns>
        protected virtual async Task<PayloadReference?> DownloadPayloadReferenceAsync(IncomingList.IncomingItem inboxItem, CancellationToken cancellationToken)
        {
            Requires.NotNull(inboxItem, nameof(inboxItem));

            HttpResponseMessage? responseMessage = await this.HttpClient.GetAsync(inboxItem.Location, cancellationToken).ConfigureAwait(false);
            if (responseMessage.StatusCode == HttpStatusCode.NotFound)
            {
                // delete inbox item and move on.
                await this.DeletePayloadReferenceAsync(inboxItem.Location, cancellationToken).ConfigureAwait(false);
                this.Log("Missing payload reference.", null);
                return null;
            }

            responseMessage.EnsureSuccessStatusCode();
            Stream? responseStream = await responseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var responseStreamCopy = new MemoryStream();
            await responseStream.CopyToAsync(responseStreamCopy, 4096, cancellationToken).ConfigureAwait(false);
            responseStreamCopy.Position = 0;

            var encryptedKey = await responseStreamCopy.ReadSizeAndBufferAsync(cancellationToken).ConfigureAwait(false);
            var key = WinRTCrypto.CryptographicEngine.Decrypt(this.Endpoint.EncryptionKey, encryptedKey);
            var iv = await responseStreamCopy.ReadSizeAndBufferAsync(cancellationToken).ConfigureAwait(false);
            Stream? ciphertextStream = await responseStreamCopy.ReadSizeAndStreamAsync(cancellationToken).ConfigureAwait(false);
            var encryptedVariables = new SymmetricEncryptionVariables(key, iv);

            var plainTextPayloadStream = new MemoryStream();
            await this.CryptoServices.DecryptAsync(ciphertextStream, plainTextPayloadStream, encryptedVariables, cancellationToken).ConfigureAwait(false);

            plainTextPayloadStream.Position = 0;
            AsymmetricAlgorithm? signingHashAlgorithm = null;   //// Encoding.UTF8.GetString(await plainTextPayloadStream.ReadSizeAndBufferAsync(cancellationToken));
            byte[] signature = await plainTextPayloadStream.ReadSizeAndBufferAsync(cancellationToken).ConfigureAwait(false);
            long payloadStartPosition = plainTextPayloadStream.Position;
            var signedBytes = new byte[plainTextPayloadStream.Length - plainTextPayloadStream.Position];
            await plainTextPayloadStream.ReadAsync(signedBytes, 0, signedBytes.Length).ConfigureAwait(false);
            plainTextPayloadStream.Position = payloadStartPosition;
            using var plainTextPayloadReader = new BinaryReader(plainTextPayloadStream);

            var recipientPublicSigningKeyBuffer = plainTextPayloadReader.ReadSizeAndBuffer();

            var creationDateUtc = DateTime.FromBinary(plainTextPayloadReader.ReadInt64());
            Endpoint? notificationAuthor = Utilities.DeserializeDataContract<Endpoint>(plainTextPayloadReader);
            PayloadReference? messageReference = Utilities.DeserializeDataContract<PayloadReference>(plainTextPayloadReader);
            messageReference.ReferenceLocation = inboxItem.Location;
            if (messageReference.HashAlgorithmName == null)
            {
                messageReference.HashAlgorithmName = Utilities.GuessHashAlgorithmFromLength(messageReference.Hash.Length).GetHashAlgorithmName();
            }

            if (!CryptoProviderExtensions.VerifySignatureWithTolerantHashAlgorithm(notificationAuthor.SigningKeyPublicMaterial, signedBytes, signature, signingHashAlgorithm))
            {
                throw new InvalidMessageException();
            }

            if (!Utilities.AreEquivalent(recipientPublicSigningKeyBuffer, this.Endpoint.PublicEndpoint.SigningKeyPublicMaterial))
            {
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
        protected virtual async Task<IReadOnlyCollection<NotificationPostedReceipt>> PostPayloadReferenceAsync(PayloadReference messageReference, IReadOnlyCollection<Endpoint> recipients, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(messageReference, nameof(messageReference));
            Requires.NotNullOrEmpty(recipients, nameof(recipients));

            // Kick off individual tasks concurrently for each recipient.
            // Each recipient requires cryptography (CPU intensive) to be performed, so don't block the calling thread.
            var postTasks = recipients.Select(recipient => Task.Run(() => this.PostPayloadReferenceAsync(messageReference, recipient, cancellationToken))).ToList();
            return await Task.WhenAll(postTasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Shares the reference to a message payload with the specified recipient.
        /// </summary>
        /// <param name="messageReference">The payload reference to share.</param>
        /// <param name="recipient">The recipient that should be notified of the message.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task representing the asynchronous operation.</returns>
        protected virtual async Task<NotificationPostedReceipt> PostPayloadReferenceAsync(PayloadReference messageReference, Endpoint recipient, CancellationToken cancellationToken)
        {
            Requires.NotNull(recipient, nameof(recipient));
            Requires.NotNull(messageReference, nameof(messageReference));

            cancellationToken.ThrowIfCancellationRequested();

            // Prepare the payload.
            using var plainTextPayloadStream = new MemoryStream();
            using var plainTextPayloadWriter = new BinaryWriter(plainTextPayloadStream);

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

            byte[] notificationSignature = WinRTCrypto.CryptographicEngine.Sign(this.Endpoint.SigningKey, plainTextPayloadStream.ToArray());
            using var signedPlainTextPayloadStream = new MemoryStream((int)plainTextPayloadStream.Length + notificationSignature.Length + 4);
            ////await signedPlainTextPayloadStream.WriteSizeAndBufferAsync(Encoding.UTF8.GetBytes(this.CryptoServices.HashAlgorithmName), cancellationToken);
            await signedPlainTextPayloadStream.WriteSizeAndBufferAsync(notificationSignature, cancellationToken).ConfigureAwait(false);
            plainTextPayloadStream.Position = 0;
            await plainTextPayloadStream.CopyToAsync(signedPlainTextPayloadStream, 4096, cancellationToken).ConfigureAwait(false);
            signedPlainTextPayloadStream.Position = 0;
            var cipherTextStream = new MemoryStream();
            SymmetricEncryptionVariables? encryptedVariables = await this.CryptoServices.EncryptAsync(signedPlainTextPayloadStream, cipherTextStream, cancellationToken: cancellationToken).ConfigureAwait(false);
            this.Log("Message invite ciphertext", cipherTextStream.ToArray());
            this.Log("Message invite key", encryptedVariables.Key);
            this.Log("Message invite IV", encryptedVariables.IV);

            var builder = new UriBuilder(recipient.MessageReceivingEndpoint);
            var lifetimeInMinutes = (int)(messageReference.ExpiresUtc - DateTime.UtcNow).TotalMinutes;
            builder.Query += "&lifetime=" + lifetimeInMinutes.ToString(CultureInfo.InvariantCulture);

            var postContent = new MemoryStream();
            ICryptographicKey? encryptionKey = CryptoSettings.EncryptionAlgorithm.ImportPublicKey(
                recipient.EncryptionKeyPublicMaterial,
                CryptoSettings.PublicKeyFormat);
            var encryptedKey = WinRTCrypto.CryptographicEngine.Encrypt(encryptionKey, encryptedVariables.Key);
            this.Log("Message invite encrypted key", encryptedKey);
            await postContent.WriteSizeAndBufferAsync(encryptedKey, cancellationToken).ConfigureAwait(false);
            await postContent.WriteSizeAndBufferAsync(encryptedVariables.IV, cancellationToken).ConfigureAwait(false);
            cipherTextStream.Position = 0;
            await postContent.WriteSizeAndStreamAsync(cipherTextStream, cancellationToken).ConfigureAwait(false);
            await postContent.FlushAsync().ConfigureAwait(false);
            postContent.Position = 0;

            using (HttpResponseMessage? response = await this.HttpClient.PostAsync(builder.Uri, new StreamContent(postContent), cancellationToken).ConfigureAwait(false))
            {
                if (response.Content != null)
                {
                    // Just to help in debugging.
                    string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }

                response.EnsureSuccessStatusCode();
                var receipt = new NotificationPostedReceipt(recipient, response.Headers.Date);
                return receipt;
            }
        }

        /// <summary>
        /// Checks whether the specified identifier yields an endpoint equivalent to this one.
        /// </summary>
        /// <param name="claimingEndpoint">The endpoint that claims to be resolvable from a given identifier.</param>
        /// <param name="claimedIdentifier">The identifier to check.</param>
        /// <param name="cancellationToken">A general cancellation token on the request.</param>
        /// <returns>A task whose result is <c>true</c> if the identifier verified correctly; otherwise <c>false</c>.</returns>
        private async Task<bool> IsVerifiableIdentifierAsync(Endpoint claimingEndpoint, string claimedIdentifier, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(claimingEndpoint, nameof(claimingEndpoint));
            Requires.NotNullOrEmpty(claimedIdentifier, nameof(claimedIdentifier));

            Endpoint cachedEndpoint;
            lock (this.resolvedIdentifiersCache)
            {
                if (this.resolvedIdentifiersCache.TryGetValue(claimedIdentifier, out cachedEndpoint))
                {
                    return cachedEndpoint.Equals(claimingEndpoint);
                }
            }

            Endpoint? matchingEndpoint = await Utilities.FastestQualifyingResultAsync(
                this.AddressBooks,
                (ct, addressBook) => addressBook.LookupAsync(claimedIdentifier, ct),
                resolvedEndpoint => claimingEndpoint.Equals(resolvedEndpoint),
                cancellationToken).ConfigureAwait(false);

            if (matchingEndpoint != null)
            {
                lock (this.resolvedIdentifiersCache)
                {
                    if (!this.resolvedIdentifiersCache.ContainsKey(claimedIdentifier))
                    {
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
        private async Task DeletePayloadReferenceAsync(Uri payloadReferenceLocation, CancellationToken cancellationToken)
        {
            Requires.NotNull(payloadReferenceLocation, nameof(payloadReferenceLocation));
            Verify.Operation(this.HttpClient is object, Strings.PropertyMustBeSetFirst, nameof(this.HttpClient));
            Verify.Operation(this.Endpoint?.InboxOwnerCode is object, Strings.PropertyMustBeSetFirst, nameof(this.Endpoint));

            var deleteEndpoint = new UriBuilder(this.Endpoint.PublicEndpoint.MessageReceivingEndpoint);
            deleteEndpoint.Query = "notification=" + Uri.EscapeDataString(payloadReferenceLocation.AbsoluteUri);
            using (HttpResponseMessage? response = await this.HttpClient.DeleteAsync(deleteEndpoint.Uri, this.Endpoint.InboxOwnerCode, cancellationToken).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
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
        private async Task<IReadOnlyList<IncomingList.IncomingItem>?> DownloadIncomingItemsAsync(bool longPoll, CancellationToken cancellationToken)
        {
            var deserializer = new DataContractJsonSerializer(typeof(IncomingList));
            Uri? requestUri = this.Endpoint.PublicEndpoint.MessageReceivingEndpoint;
            HttpClient? httpClient = this.HttpClient;
            if (longPoll)
            {
                requestUri = new Uri(requestUri.AbsoluteUri + "?longPoll=true");
                httpClient = this.httpClientLongPoll;
            }

            HttpResponseMessage? responseMessage = await httpClient.GetAsync(requestUri, this.Endpoint.InboxOwnerCode, cancellationToken).ConfigureAwait(false);
            responseMessage.EnsureSuccessStatusCode();
            Stream? responseStream = await responseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var inboxResults = (IncomingList)deserializer.ReadObject(responseStream);

            return inboxResults.Items;
        }

        /// <summary>Logs a message.</summary>
        /// <param name="caption">A description of what the contents of the <paramref name="buffer"/> are.</param>
        /// <param name="buffer">The buffer.</param>
        private void Log([Localizable(false)] string caption, byte[]? buffer)
        {
            if (this.TraceSource is TraceSource traceSource)
            {
                if (buffer is object)
                {
                    traceSource.TraceEvent(TraceEventType.Verbose, 0, caption + ": {0}", Convert.ToBase64String(buffer));
                }
                else
                {
                    traceSource.TraceEvent(TraceEventType.Verbose, 0, caption);
                }
            }
        }

        /// <summary>
        /// A message payload and the time notification of it was received by the cloud inbox.
        /// </summary>
        public class PayloadReceipt
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="PayloadReceipt"/> class.
            /// </summary>
            /// <param name="payload">The payload itself.</param>
            /// <param name="dateNotificationPosted">The date the cloud inbox received notification of the payload.</param>
            public PayloadReceipt(Payload payload, DateTimeOffset dateNotificationPosted)
            {
                Requires.NotNull(payload, nameof(payload));
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
        public class NotificationPostedReceipt
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="NotificationPostedReceipt"/> class.
            /// </summary>
            /// <param name="recipient">The inbox that received the notification.</param>
            /// <param name="cloudInboxReceiptTimestamp">The timestamp included in the HTTP response from the server.</param>
            public NotificationPostedReceipt(Endpoint recipient, DateTimeOffset? cloudInboxReceiptTimestamp)
            {
                Requires.NotNull(recipient, nameof(recipient));

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
