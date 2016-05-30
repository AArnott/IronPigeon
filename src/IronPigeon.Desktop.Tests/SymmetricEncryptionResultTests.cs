// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class SymmetricEncryptionResultTests
    {
        private static readonly byte[] EmptyBuffer = new byte[0];
        private static readonly byte[] NonEmptyBuffer = new byte[1];

        [Fact]
        public void CtorThrowsOnNullBuffer()
        {
            Assert.Throws<ArgumentNullException>(() => new SymmetricEncryptionResult(null, NonEmptyBuffer, NonEmptyBuffer));
            Assert.Throws<ArgumentNullException>(() => new SymmetricEncryptionResult(NonEmptyBuffer, null, NonEmptyBuffer));
            Assert.Throws<ArgumentNullException>(() => new SymmetricEncryptionResult(NonEmptyBuffer, NonEmptyBuffer, null));
        }

        [Fact]
        public void CtorThrowsOnEmptyBuffer()
        {
            Assert.Throws<ArgumentException>(() => new SymmetricEncryptionResult(EmptyBuffer, NonEmptyBuffer, NonEmptyBuffer));
            Assert.Throws<ArgumentException>(() => new SymmetricEncryptionResult(NonEmptyBuffer, EmptyBuffer, NonEmptyBuffer));
            Assert.Throws<ArgumentException>(() => new SymmetricEncryptionResult(NonEmptyBuffer, NonEmptyBuffer, EmptyBuffer));
        }

        [Fact]
        public void CtorAcceptsValidArguments()
        {
            var key = new byte[1];
            var iv = new byte[1];
            var ciphertext = new byte[1];
            var result = new SymmetricEncryptionResult(key, iv, ciphertext);
            Assert.Same(key, result.Key);
            Assert.Same(iv, result.IV);
            Assert.Same(ciphertext, result.Ciphertext);
        }
    }
}
