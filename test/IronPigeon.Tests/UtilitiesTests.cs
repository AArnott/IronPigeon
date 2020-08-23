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
    using Microsoft;
    using Xunit;

    public class UtilitiesTests
    {
        [Fact]
        public void Base64WebSafe()
        {
            var buffer = new byte[15];
            new Random().NextBytes(buffer);

            string expectedBase64 = Convert.ToBase64String(buffer);

            string web64 = Utilities.ToBase64WebSafe(buffer);
            string actualBase64 = Utilities.FromBase64WebSafe(web64);

            Assert.Equal(expectedBase64, actualBase64);

            byte[] decoded = Convert.FromBase64String(actualBase64);
            Assert.True(Utilities.AreEquivalent(buffer, decoded));
        }

        [Fact]
        public async Task ReadStreamWithProgress()
        {
            var updates = new List<long>();
            using var largeStream = new MemoryStream(new byte[1024 * 1024]);
            var progress = new MockProgress<long>(u => updates.Add(u));
            Stream? progressStream = largeStream.ReadStreamWithProgress(progress);
            await progressStream.CopyToAsync(Stream.Null);
            Assert.NotEmpty(updates);
            for (int i = 1; i < updates.Count; i++)
            {
                Assert.True(updates[i] >= updates[i - 1]);
            }

            Assert.Equal(largeStream.Length, updates[updates.Count - 1]);
        }

        private class MockProgress<T> : IProgress<T>
        {
            private readonly Action<T> report;

            internal MockProgress(Action<T> report)
            {
                Requires.NotNull(report, nameof(report));

                this.report = report;
            }

            public void Report(T value)
            {
                this.report(value);
            }
        }
    }
}
