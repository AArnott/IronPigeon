// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests.Mocks
{
    using System.Threading;
    using System.Threading.Tasks;
    using IronPigeon.Relay;

    internal class EndpointInboxFactoryMock : IEndpointInboxFactory
    {
        private readonly InboxCreationResponse response;

        internal EndpointInboxFactoryMock(InboxCreationResponse response)
        {
            this.response = response;
        }

        public Task<InboxCreationResponse> CreateInboxAsync(CancellationToken cancellationToken = default) => Task.FromResult(this.response);
    }
}
