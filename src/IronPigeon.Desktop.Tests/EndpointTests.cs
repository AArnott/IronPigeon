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

    public class EndpointTests
    {
        [Fact]
        public void DefaultContactCtor()
        {
            var contact = new Endpoint();
            Assert.Null(contact.MessageReceivingEndpoint);
            Assert.Null(contact.EncryptionKeyPublicMaterial);
            Assert.Null(contact.SigningKeyPublicMaterial);
            var skew = TimeSpan.FromMinutes(1);
            Assert.InRange(contact.CreatedOnUtc, DateTime.UtcNow - skew, DateTime.UtcNow + skew);
        }

        [Fact]
        public void Equals()
        {
            var contact1 = new Endpoint();
            Assert.False(contact1.Equals(null));
            Assert.True(contact1.Equals(contact1));
            Assert.False(contact1.Equals(Valid.PublicEndpoint));

            contact1.MessageReceivingEndpoint = Valid.PublicEndpoint.MessageReceivingEndpoint;
            contact1.SigningKeyPublicMaterial = Valid.PublicEndpoint.SigningKeyPublicMaterial;
            contact1.EncryptionKeyPublicMaterial = Valid.PublicEndpoint.EncryptionKeyPublicMaterial;
            Assert.Equal(Valid.PublicEndpoint, contact1);

            contact1.MessageReceivingEndpoint = null;
            Assert.NotEqual(Valid.PublicEndpoint, contact1);
            contact1.MessageReceivingEndpoint = Valid.PublicEndpoint.MessageReceivingEndpoint;

            contact1.SigningKeyPublicMaterial = null;
            Assert.NotEqual(Valid.PublicEndpoint, contact1);
            contact1.SigningKeyPublicMaterial = Valid.PublicEndpoint.SigningKeyPublicMaterial;

            contact1.EncryptionKeyPublicMaterial = null;
            Assert.NotEqual(Valid.PublicEndpoint, contact1);
            contact1.EncryptionKeyPublicMaterial = Valid.PublicEndpoint.EncryptionKeyPublicMaterial;
        }
    }
}
