// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests.Mocks
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft;

    internal class HttpMessageHandlerMock : HttpMessageHandler
    {
        private readonly List<Func<HttpRequestMessage, Task<HttpResponseMessage?>>> handlers = new List<Func<HttpRequestMessage, Task<HttpResponseMessage?>>>();

        internal HttpMessageHandlerMock()
        {
        }

        internal void ClearHandlers()
        {
            this.handlers.Clear();
        }

        internal void RegisterHandler(Func<HttpRequestMessage, Task<HttpResponseMessage?>> handler)
        {
            Requires.NotNull(handler, nameof(handler));
            this.handlers.Add(handler);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            foreach (Func<HttpRequestMessage, Task<HttpResponseMessage?>>? handler in this.handlers)
            {
                HttpResponseMessage? result = await handler(request);
                if (result != null)
                {
                    return result;
                }
            }

            throw new InvalidOperationException("No handler registered for request " + request.RequestUri);
        }
    }
}
