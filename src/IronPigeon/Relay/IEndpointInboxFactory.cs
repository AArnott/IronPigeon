// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A local service that requests new inboxes at a remote message relay service.
    /// </summary>
    public interface IEndpointInboxFactory
    {
        /// <summary>
        /// Creates an inbox at a message relay service.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the inbox creation request from the server.</returns>
        Task<InboxCreationResponse> CreateInboxAsync(CancellationToken cancellationToken = default(CancellationToken));
    }
}
