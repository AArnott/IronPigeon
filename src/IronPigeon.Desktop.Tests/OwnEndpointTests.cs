// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    public class OwnEndpointTests
    {
        [Test]
        public void CtorInvalidArgs()
        {
            Assert.Throws<ArgumentNullException>(() => new OwnEndpoint(null, Valid.ReceivingEndpoint.EncryptionKey));
            Assert.Throws<ArgumentNullException>(() => new OwnEndpoint(Valid.ReceivingEndpoint.SigningKey, null));
        }

        [Test]
        public void Ctor()
        {
            var ownContact = new OwnEndpoint(Valid.ReceivingEndpoint.SigningKey, Valid.ReceivingEndpoint.EncryptionKey);
            ownContact.PublicEndpoint.MessageReceivingEndpoint = Valid.ReceivingEndpoint.PublicEndpoint.MessageReceivingEndpoint;
            Assert.That(ownContact.PublicEndpoint, Is.EqualTo(Valid.ReceivingEndpoint.PublicEndpoint));
            Assert.That(ownContact.EncryptionKeyPrivateMaterial, Is.EqualTo(Valid.ReceivingEndpoint.EncryptionKeyPrivateMaterial));
            Assert.That(ownContact.SigningKeyPrivateMaterial, Is.EqualTo(Valid.ReceivingEndpoint.SigningKeyPrivateMaterial));
        }

        [Test]
        public void CreateAddressBookEntryNullInput()
        {
            var ownContact = new OwnEndpoint(Valid.ReceivingEndpoint.SigningKey, Valid.ReceivingEndpoint.EncryptionKey);
            Assert.Throws<ArgumentNullException>(() => ownContact.CreateAddressBookEntry(null));
        }

        [Test]
        public void CreateAddressBookEntry()
        {
            var ownContact = Valid.ReceivingEndpoint;
            CryptoSettings cryptoServices = new CryptoSettings(SecurityLevel.Minimum);
            var entry = ownContact.CreateAddressBookEntry(cryptoServices);
            Assert.That(entry.Signature, Is.Not.Null.And.Not.Empty);
            Assert.That(entry.SerializedEndpoint, Is.Not.Null.And.Not.Empty);
        }
    }
}
