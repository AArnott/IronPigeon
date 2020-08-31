// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests
{
    using System;
    using Xunit;

    public class OwnEndpointTests
    {
        [Fact]
        public void CtorInvalidArgs()
        {
            Assert.Throws<ArgumentNullException>("messageReceivingEndpoint", () => new OwnEndpoint(null!, Valid.ReceivingEndpoint.SigningKeyInputs, Valid.ReceivingEndpoint.DecryptionKeyInputs));
            Assert.Throws<ArgumentNullException>("signingKeyInputs", () => new OwnEndpoint(Valid.MessageReceivingEndpoint, null!, Valid.ReceivingEndpoint.DecryptionKeyInputs));
            Assert.Throws<ArgumentNullException>("decryptionKeyInputs", () => new OwnEndpoint(Valid.MessageReceivingEndpoint, Valid.ReceivingEndpoint.SigningKeyInputs, null!));
        }

        [Fact]
        public void Ctor()
        {
            var ownContact = new OwnEndpoint(Valid.ReceivingEndpoint.MessageReceivingEndpoint, Valid.ReceivingEndpoint.SigningKeyInputs, Valid.ReceivingEndpoint.DecryptionKeyInputs, Valid.ReceivingEndpoint.InboxOwnerCode);
            Assert.Equal(Valid.ReceivingEndpoint.PublicEndpoint, ownContact.PublicEndpoint);
            Assert.Equal(Valid.ReceivingEndpoint.DecryptionKeyInputs, ownContact.DecryptionKeyInputs);
            Assert.Equal(Valid.ReceivingEndpoint.SigningKeyInputs, ownContact.SigningKeyInputs);
            Assert.Equal(Valid.ReceivingEndpoint.InboxOwnerCode, ownContact.InboxOwnerCode);
        }

        [Fact]
        public void CreateAddressBookEntry()
        {
            OwnEndpoint? ownContact = Valid.ReceivingEndpoint;
            AddressBookEntry? entry = new AddressBookEntry(ownContact);
            Assert.NotEqual(0, entry.Signature.Length);
            Assert.NotEqual(0, entry.SerializedEndpoint.Length);
        }
    }
}
