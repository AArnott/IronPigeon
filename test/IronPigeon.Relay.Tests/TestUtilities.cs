// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public static class TestUtilities
{
    internal static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> asyncEnumerable, CancellationToken cancellationToken)
    {
        var list = new List<T>();
        await foreach (T item in asyncEnumerable.WithCancellation(cancellationToken))
        {
            list.Add(item);
            cancellationToken.ThrowIfCancellationRequested();
        }

        return list;
    }
}
