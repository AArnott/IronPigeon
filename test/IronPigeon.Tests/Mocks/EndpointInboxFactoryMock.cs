// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests.Mocks
{
    using System.Threading;
    using System.Threading.Tasks;
    using IronPigeon.Relay;

    internal class EndpointInboxFactoryMock : IEndpointInboxFactory
    {
        private int counter;

        public Task<InboxCreationResponse> CreateInboxAsync(CancellationToken cancellationToken = default)
        {
            int counter = Interlocked.Increment(ref this.counter);
            return Task.FromResult(new InboxCreationResponse(new System.Uri($"http://localhost/inbox/some{counter}"), $"code{counter}"));
        }
    }
}
