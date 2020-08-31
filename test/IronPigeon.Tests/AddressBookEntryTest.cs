// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests
{
    using System;
    using Xunit;
    using Xunit.Abstractions;

    public class AddressBookEntryTest : TestBase
    {
        private static readonly byte[] SerializedEndpoint = new byte[] { 0x1, 0x2 };
        private static readonly byte[] Signature = new byte[] { 0x3, 0x4 };

        public AddressBookEntryTest(ITestOutputHelper logger)
            : base(logger)
        {
        }

        [Fact]
        public void Ctor_InvalidInputs()
        {
            Assert.Throws<ArgumentNullException>("endpoint", () => new AddressBookEntry(null!));
        }

        [Fact]
        public void Ctor_Serialized()
        {
            var abe = new AddressBookEntry(SerializedEndpoint, Signature);
            Assert.True(Utilities.AreEquivalent(SerializedEndpoint, abe.SerializedEndpoint.Span));
            Assert.True(Utilities.AreEquivalent(Signature, abe.Signature.Span));
        }

        [Fact]
        public void Ctor_OwnEndpoint()
        {
            var abe = new AddressBookEntry(Valid.ReceivingEndpoint);
            Assert.NotEqual(0, abe.SerializedEndpoint.Length);
            Assert.NotEqual(0, abe.Signature.Length);
        }

        [Fact]
        public void Serializability()
        {
            AddressBookEntry entry = new AddressBookEntry(SerializedEndpoint, Signature);
            AddressBookEntry deserializedEntry = SerializeRoundTrip(entry);
            Assert.Equal(entry.SerializedEndpoint, deserializedEntry.SerializedEndpoint);
            Assert.Equal(entry.Signature, deserializedEntry.Signature);
        }

        [Fact]
        public void ExtractEndpoint()
        {
            AddressBookEntry entry = new AddressBookEntry(Valid.ReceivingEndpoint);
            Endpoint endpoint = entry.ExtractEndpoint();
            Assert.Equal(Valid.PublicEndpoint, endpoint);
        }

        [Fact]
        public void ExtractEndpointDetectsTampering()
        {
            AddressBookEntry entry = new AddressBookEntry(Valid.ReceivingEndpoint);

            byte[] untamperedEndpoint = entry.SerializedEndpoint.ToArray();
            byte[] fuzzedEndpoint = new byte[untamperedEndpoint.Length];
            for (int i = 0; i < 100; i++)
            {
                untamperedEndpoint.CopyTo(fuzzedEndpoint, 0);
                TestUtilities.ApplyFuzzing(fuzzedEndpoint, 1);
                var fuzzedEntry = new AddressBookEntry(fuzzedEndpoint, entry.Signature);
                Assert.Throws<BadAddressBookEntryException>(() => fuzzedEntry.ExtractEndpoint());
            }
        }
    }
}
