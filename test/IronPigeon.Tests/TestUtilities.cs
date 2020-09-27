// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

internal static class TestUtilities
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

    internal static void ApplyFuzzing(byte[] buffer, int bytesToChange)
    {
        var random = new Random();
        for (int i = 0; i < bytesToChange; i++)
        {
            int index = random.Next(buffer.Length);
            buffer[index] = (byte)unchecked(buffer[index] + random.Next(1, 255));
        }
    }
}
