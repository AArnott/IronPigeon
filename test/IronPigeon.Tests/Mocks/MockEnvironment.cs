// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests.Mocks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using IronPigeon;
    using IronPigeon.Tests.Mocks;

    internal class MockEnvironment : IDisposable
    {
        private readonly CryptoSettings defaultCryptoSettings = CryptoSettings.Testing;
        private readonly HttpMessageHandlerMock httpHandler = new HttpMessageHandlerMock();

        internal MockEnvironment()
        {
            this.CloudStorage.AddHttpHandler(this.httpHandler);
            this.InboxServer.Register(this.httpHandler);
            this.HttpClient = new HttpClient(this.httpHandler);
        }

        internal CloudBlobStorageProviderMock CloudStorage { get; } = new CloudBlobStorageProviderMock();

        internal InboxHttpHandlerMock InboxServer { get; } = new InboxHttpHandlerMock();

        internal HttpClient HttpClient { get; }

        public void Dispose()
        {
            this.HttpClient.Dispose();
            this.httpHandler.Dispose();
        }

        internal Task<OwnEndpoint> CreateOwnEndpointAsync(CancellationToken cancellationToken) => this.CreateOwnEndpointAsync(this.defaultCryptoSettings, cancellationToken);

        internal async Task<OwnEndpoint> CreateOwnEndpointAsync(CryptoSettings cryptoSettings, CancellationToken cancellationToken)
        {
            return await OwnEndpoint.CreateAsync(cryptoSettings, this.InboxServer, cancellationToken);
        }

        internal Channel CreateChannel(OwnEndpoint endpoint, CryptoSettings? cryptoSettings = null) => new Channel(this.HttpClient, endpoint, this.CloudStorage, cryptoSettings ?? this.defaultCryptoSettings);

        internal Task<Channel> CreateChannelAsync(CancellationToken cancellationToken) => this.CreateChannelAsync(this.defaultCryptoSettings, cancellationToken);

        internal async Task<Channel> CreateChannelAsync(CryptoSettings cryptoSettings, CancellationToken cancellationToken)
        {
            OwnEndpoint endpoint = await this.CreateOwnEndpointAsync(cryptoSettings, cancellationToken);
            return this.CreateChannel(endpoint);
        }
    }
}
