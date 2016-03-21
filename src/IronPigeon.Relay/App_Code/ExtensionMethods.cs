// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;

    internal static class ExtensionMethods
    {
        /// <summary>
        /// Gets the client disconnected token.
        /// </summary>
        /// <param name="response">The response.</param>
        /// <returns>The token that is cancelled when the client disconnects.</returns>
        internal static CancellationToken GetClientDisconnectedToken(this HttpResponseBase response)
        {
            return response.ClientDisconnectedToken;
        }
    }
}