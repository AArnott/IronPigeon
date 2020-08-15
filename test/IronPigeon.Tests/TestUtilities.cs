// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using IronPigeon.Providers;
    using Microsoft;

    internal static class TestUtilities
    {
        internal static void ApplyFuzzing(byte[] buffer, int bytesToChange)
        {
            var random = new Random();
            for (int i = 0; i < bytesToChange; i++)
            {
                int index = random.Next(buffer.Length);
                buffer[index] = (byte)unchecked(buffer[index] + 0x1);
            }
        }

        internal static byte[] CopyBuffer(this byte[] buffer)
        {
            Requires.NotNull(buffer, nameof(buffer));

            var copy = new byte[buffer.Length];
            Array.Copy(buffer, copy, buffer.Length);
            return copy;
        }

        internal static void CopyBuffer(this byte[] buffer, byte[] to)
        {
            Requires.NotNull(buffer, nameof(buffer));
            Requires.NotNull(to, nameof(to));
            Requires.Argument(buffer.Length == to.Length, "to", "Lengths do not match");

            Array.Copy(buffer, to, buffer.Length);
        }

        internal static CryptoSettings CreateAuthenticCryptoProvider()
        {
            return new CryptoSettings(SecurityLevel.Minimum);
        }
    }
}
