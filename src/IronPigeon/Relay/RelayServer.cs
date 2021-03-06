﻿// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using MessagePack;
    using Microsoft;

    /// <summary>
    /// A class that requests push notifications from a message receiving endpoint.
    /// </summary>
    public class RelayServer
    {
        private readonly HttpClient httpClient;
        private readonly OwnEndpoint ownEndpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="RelayServer"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use.</param>
        /// <param name="ownEndpoint">The endpoint to register notifications for.</param>
        public RelayServer(HttpClient httpClient, OwnEndpoint ownEndpoint)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this.ownEndpoint = ownEndpoint ?? throw new ArgumentNullException(nameof(ownEndpoint));
        }

        /// <summary>
        /// Gets or sets the timeout for typical HTTP requests.
        /// </summary>
        /// <value>The default value is 100 seconds.</value>
        public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(100);

        /// <summary>
        /// Registers an Android application to receive push notifications for incoming messages.
        /// </summary>
        /// <param name="googlePlayRegistrationId">The Google Cloud Messaging registration identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// A task representing the async operation.
        /// </returns>
        public async Task RegisterGooglePlayPushNotificationAsync(string googlePlayRegistrationId, CancellationToken cancellationToken = default)
        {
            Requires.NotNullOrEmpty(googlePlayRegistrationId, nameof(googlePlayRegistrationId));

            using var httpTimeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            httpTimeoutTokenSource.CancelAfter(this.HttpTimeout);

            using var request = new HttpRequestMessage(HttpMethod.Put, this.ownEndpoint.PublicEndpoint.MessageReceivingEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.ownEndpoint.InboxOwnerCode);
            var contentDictionary = new Dictionary<string, string>
            {
                { "gcm_registration_id", googlePlayRegistrationId },
            };
            request.Content = new FormUrlEncodedContent(contentDictionary!);
            HttpResponseMessage response = await this.httpClient.SendAsync(request, httpTimeoutTokenSource.Token).ConfigureAwait(false);
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
        public async Task RegisterApplePushNotificationAsync(string deviceToken, CancellationToken cancellationToken = default)
        {
            Requires.NotNullOrEmpty(deviceToken, nameof(deviceToken));

            using var httpTimeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            httpTimeoutTokenSource.CancelAfter(this.HttpTimeout);

            using var request = new HttpRequestMessage(HttpMethod.Put, this.ownEndpoint.PublicEndpoint.MessageReceivingEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.ownEndpoint.InboxOwnerCode);
            var contentDictionary = new Dictionary<string, string>
            {
                { "ios_device_token", deviceToken },
            };
            request.Content = new FormUrlEncodedContent(contentDictionary!);
            HttpResponseMessage response = await this.httpClient.SendAsync(request, httpTimeoutTokenSource.Token).ConfigureAwait(false);
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
        public async Task RegisterWindowsPushNotificationChannelAsync(string packageSecurityIdentifier, Uri pushNotificationChannelUri, DateTime channelExpiration, string pushContent, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(pushNotificationChannelUri, nameof(pushNotificationChannelUri));
            Requires.NotNullOrEmpty(packageSecurityIdentifier, nameof(packageSecurityIdentifier));

            using var httpTimeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            httpTimeoutTokenSource.CancelAfter(this.HttpTimeout);

            using var request = new HttpRequestMessage(HttpMethod.Put, this.ownEndpoint.PublicEndpoint.MessageReceivingEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.ownEndpoint.InboxOwnerCode);
            var contentDictionary = new Dictionary<string, string>
            {
                { "package_security_identifier", packageSecurityIdentifier },
                { "channel_uri", pushNotificationChannelUri.AbsoluteUri },
                { "channel_content", pushContent ?? string.Empty },
                { "expiration", channelExpiration.ToString(CultureInfo.InvariantCulture) },
            };
            request.Content = new FormUrlEncodedContent(contentDictionary!);
            HttpResponseMessage? response = await this.httpClient.SendAsync(request, httpTimeoutTokenSource.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Posts a <see cref="InboxItemEnvelope"/> to a <see cref="Endpoint.MessageReceivingEndpoint"/>.
        /// </summary>
        /// <param name="receivingEndpoint">The endpoint to receive the notification.</param>
        /// <param name="inboxPayload">The serialized <see cref="InboxItemEnvelope"/>.</param>
        /// <param name="expiresUtc">An expiration after which the relay server may delete the notification.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A receipt from the relay server acknowledging receipt.</returns>
        public async Task<NotificationPostedReceipt> PostInboxItemAsync(Endpoint receivingEndpoint, ReadOnlyMemory<byte> inboxPayload, DateTime? expiresUtc, CancellationToken cancellationToken)
        {
            Requires.NotNull(receivingEndpoint, nameof(receivingEndpoint));

            using var httpTimeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            httpTimeoutTokenSource.CancelAfter(this.HttpTimeout);

            var builder = new UriBuilder(receivingEndpoint.MessageReceivingEndpoint);
            if (expiresUtc.HasValue)
            {
                long lifetimeInMinutes = (long)(expiresUtc.Value - DateTime.UtcNow).TotalMinutes;
                if (builder.Query.Length > 0)
                {
                    builder.Query += "&";
                }

                builder.Query += "lifetime=" + lifetimeInMinutes.ToString(CultureInfo.InvariantCulture);
            }

            using HttpContent content = new ByteArrayContent(inboxPayload.AsOrCreateArray())
            {
                Headers =
                {
                    ContentLength = inboxPayload.Length,
                },
            };
            using HttpResponseMessage response = await this.httpClient.PostAsync(builder.Uri, content, httpTimeoutTokenSource.Token).ConfigureAwait(false);
            if (response.Content is object)
            {
                // Just to help in debugging.
                string responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            response.EnsureSuccessStatusCode();
            var receipt = new NotificationPostedReceipt(receivingEndpoint);
            return receipt;
        }

        /// <summary>
        /// Downloads inbox items from the server.
        /// </summary>
        /// <param name="longPoll"><c>true</c> to asynchronously wait for messages if there are none immediately available for download.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task whose result is the list of downloaded inbox items.</returns>
        public async IAsyncEnumerable<IncomingInboxItem> DownloadIncomingItemsAsync(bool longPoll, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Verify.Operation(this.httpClient is object, Strings.PropertyMustBeSetFirst, nameof(this.httpClient));
            Verify.Operation(this.ownEndpoint.InboxOwnerCode is object, Strings.PropertyMustBeSetFirst, nameof(this.ownEndpoint));

            Uri requestUri = this.ownEndpoint.PublicEndpoint.MessageReceivingEndpoint;
            CancellationTokenSource? httpTimeoutTokenSource = null;
            try
            {
                if (longPoll)
                {
                    requestUri = new Uri(requestUri.AbsoluteUri + "?longPoll=true");
                }
                else
                {
                    httpTimeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    httpTimeoutTokenSource.CancelAfter(this.HttpTimeout);
                    cancellationToken = httpTimeoutTokenSource.Token;
                }

                using HttpResponseMessage responseMessage = await this.httpClient.GetAsync(requestUri, this.ownEndpoint.InboxOwnerCode, cancellationToken).ConfigureAwait(false);
                responseMessage.EnsureSuccessStatusCode();
                Stream responseStream = await responseMessage.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var streamReader = new MessagePackStreamReader(responseStream);
                ReadOnlySequence<byte>? serializedIncomingInboxItem;
                while ((serializedIncomingInboxItem = await streamReader.ReadAsync(cancellationToken).ConfigureAwait(false)).HasValue)
                {
                    IncomingInboxItem incomingInboxItem = MessagePackSerializer.Deserialize<IncomingInboxItem>(serializedIncomingInboxItem.Value, Utilities.MessagePackSerializerOptions, cancellationToken);
                    yield return incomingInboxItem;
                }
            }
            finally
            {
                httpTimeoutTokenSource?.Dispose();
            }
        }

        /// <summary>
        /// Deletes an entry from an inbox's incoming item list.
        /// </summary>
        /// <param name="inboxItem">The incoming payload reference to delete.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task representing the asynchronous operation.</returns>
        public async Task DeleteInboxItemAsync(IncomingInboxItem inboxItem, CancellationToken cancellationToken)
        {
            Requires.NotNull(inboxItem, nameof(inboxItem));
            Verify.Operation(this.ownEndpoint.InboxOwnerCode is object, Strings.PropertyMustBeSetFirst, nameof(this.ownEndpoint));

            using (HttpResponseMessage? response = await this.httpClient.DeleteAsync(inboxItem.Identity, this.ownEndpoint.InboxOwnerCode, cancellationToken).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    // Good enough.
                    return;
                }

                response.EnsureSuccessStatusCode();
            }
        }
    }
}
