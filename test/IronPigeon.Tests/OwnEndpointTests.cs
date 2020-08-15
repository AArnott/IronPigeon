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

    public class OwnEndpointTests
    {
        [Fact]
        public void CtorInvalidArgs()
        {
            Assert.Throws<ArgumentNullException>(() => new OwnEndpoint(null!, Valid.ReceivingEndpoint.EncryptionKey!));
            Assert.Throws<ArgumentNullException>(() => new OwnEndpoint(Valid.ReceivingEndpoint.SigningKey, null!));
        }

        [Fact]
        public void Ctor()
        {
            var ownContact = new OwnEndpoint(Valid.ReceivingEndpoint.SigningKey, Valid.ReceivingEndpoint.EncryptionKey!);
            ownContact.PublicEndpoint.MessageReceivingEndpoint = Valid.ReceivingEndpoint.PublicEndpoint.MessageReceivingEndpoint;
            Assert.Equal(Valid.ReceivingEndpoint.PublicEndpoint, ownContact.PublicEndpoint);
            Assert.Equal(Valid.ReceivingEndpoint.EncryptionKeyPrivateMaterial, ownContact.EncryptionKeyPrivateMaterial);
            Assert.Equal(Valid.ReceivingEndpoint.SigningKeyPrivateMaterial, ownContact.SigningKeyPrivateMaterial);
        }

        [Fact]
        public void CreateAddressBookEntryNullInput()
        {
            var ownContact = new OwnEndpoint(Valid.ReceivingEndpoint.SigningKey, Valid.ReceivingEndpoint.EncryptionKey!);
            Assert.Throws<ArgumentNullException>(() => ownContact.CreateAddressBookEntry(null!));
        }

        [Fact]
        public void CreateAddressBookEntry()
        {
            OwnEndpoint? ownContact = Valid.ReceivingEndpoint;
            CryptoSettings cryptoServices = new CryptoSettings(SecurityLevel.Minimum);
            AddressBookEntry? entry = ownContact.CreateAddressBookEntry(cryptoServices);
            Assert.NotEqual(0, entry.Signature?.Length);
            Assert.NotEqual(0, entry.SerializedEndpoint?.Length);
        }
    }
}
