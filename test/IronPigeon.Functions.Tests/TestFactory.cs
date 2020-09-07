// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

public static class TestFactory
{
    public static HttpRequest CreateHttpRequest() => new DefaultHttpContext
    {
        Request =
        {
            Scheme = "http",
            Host = new HostString("localhost"),
        },
    }.Request;

    public static HttpRequest CreateHttpRequest(string queryStringKey, string queryStringValue)
    {
        HttpRequest request = CreateHttpRequest();
        request.Query = new QueryCollection(
            new Dictionary<string, StringValues>
            {
                { queryStringKey, queryStringValue },
            });
        return request;
    }
}
