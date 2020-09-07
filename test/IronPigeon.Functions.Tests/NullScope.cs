// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

using System;

internal class NullScope : IDisposable
{
    private NullScope()
    {
    }

    public static NullScope Instance { get; } = new NullScope();

    public void Dispose()
    {
    }
}
