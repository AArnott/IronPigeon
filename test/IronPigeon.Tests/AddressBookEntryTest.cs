// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class AddressBookEntryTest
    {
        private const string HashAlgorithm = "SHA1";
        private static readonly byte[] SerializedEndpoint = new byte[] { 0x1, 0x2 };
        private static readonly byte[] Signature = new byte[] { 0x3, 0x4 };
        private CryptoSettings desktopCryptoProvider;

        public AddressBookEntryTest()
        {
            this.desktopCryptoProvider = TestUtilities.CreateAuthenticCryptoProvider();
        }

        [Fact]
        public void Ctor_PropertyGetters()
        {
            Assert.Throws<ArgumentNullException>("serializedEndpoint", () => new AddressBookEntry(null!, HashAlgorithm, Signature));
            Assert.Throws<ArgumentNullException>("hashAlgorithmName", () => new AddressBookEntry(SerializedEndpoint, null!, Signature));
            Assert.Throws<ArgumentNullException>("signature", () => new AddressBookEntry(SerializedEndpoint, HashAlgorithm, null!));

            var abe = new AddressBookEntry(SerializedEndpoint, HashAlgorithm, Signature);
            Assert.Same(SerializedEndpoint, abe.SerializedEndpoint);
            Assert.Equal(HashAlgorithm, abe.HashAlgorithmName);
            Assert.Same(Signature, abe.Signature);
        }

        [Fact]
        public void Serializability()
        {
            var entry = new AddressBookEntry(SerializedEndpoint, HashAlgorithm, Signature);

            using var ms = new MemoryStream();
            var serializer = new DataContractSerializer(typeof(AddressBookEntry));
            serializer.WriteObject(ms, entry);
            ms.Position = 0;
            var deserializedEntry = (AddressBookEntry)serializer.ReadObject(ms);

            Assert.Equal(entry.SerializedEndpoint, deserializedEntry.SerializedEndpoint);
            Assert.Equal(entry.Signature, deserializedEntry.Signature);
        }

        [Fact]
        public void ExtractEndpoint()
        {
            var ownContact = new OwnEndpoint(Valid.ReceivingEndpoint.SigningKey, Valid.ReceivingEndpoint.EncryptionKey, DateTime.UtcNow, Valid.MessageReceivingEndpoint);
            var cryptoServices = new CryptoSettings(SecurityLevel.Minimum);
            AddressBookEntry? entry = ownContact.CreateAddressBookEntry(cryptoServices);

            Endpoint? endpoint = entry.ExtractEndpoint();
            Assert.Equal(ownContact.PublicEndpoint, endpoint);
        }

        [Fact]
        public void ExtractEndpointDetectsTampering()
        {
            OwnEndpoint? ownContact = Valid.GenerateOwnEndpoint(this.desktopCryptoProvider);
            AddressBookEntry? entry = ownContact.CreateAddressBookEntry(this.desktopCryptoProvider);

            var untamperedEndpoint = entry.SerializedEndpoint.CopyBuffer();
            for (int i = 0; i < 100; i++)
            {
                TestUtilities.ApplyFuzzing(entry.SerializedEndpoint, 1);
                Assert.Throws<BadAddressBookEntryException>(() => entry.ExtractEndpoint());
                untamperedEndpoint.CopyBuffer(entry.SerializedEndpoint);
            }
        }
    }
}
