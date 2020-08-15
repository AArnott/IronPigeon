// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;

    [SuppressMessage("Microsoft.StyleCop.CSharp.OrderingRules", "SA1202:ElementsMustBeOrderedByAccess", Justification = "Declaration order impacts execution order.")]
    internal static class Invalid
    {
        private static readonly byte[] EmptyBuffer = new byte[0];

        internal static readonly byte[] Hash = EmptyBuffer;
        internal static readonly byte[] Key = EmptyBuffer;
        internal static readonly byte[] IV = EmptyBuffer;

        internal static readonly string ContentType = string.Empty;
        internal static readonly DateTime ExpirationUtc = DateTime.Now;
    }
}
