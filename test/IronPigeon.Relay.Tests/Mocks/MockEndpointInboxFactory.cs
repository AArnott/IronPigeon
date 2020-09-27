// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using IronPigeon.Relay;

internal class MockEndpointInboxFactory : IEndpointInboxFactory
{
    public Task<InboxCreationResponse> CreateInboxAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new InboxCreationResponse(new Uri("https://some/uri"), "abc"));
    }
}
