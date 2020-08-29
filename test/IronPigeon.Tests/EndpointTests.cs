// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests
{
    using System;
    using Xunit;

    public class EndpointTests
    {
        [Fact]
        public void CtorTests()
        {
            Assert.Throws<ArgumentNullException>("messageReceivingEndpoint", () => new Endpoint(DateTime.UtcNow, null!, Valid.PublicEndpoint.SigningKeyPublicMaterial, Valid.PublicEndpoint.EncryptionKeyPublicMaterial, Array.Empty<string>()));
            Assert.Throws<ArgumentNullException>("signingKeyPublicMaterial", () => new Endpoint(DateTime.UtcNow, Valid.PublicEndpoint.MessageReceivingEndpoint, null!, Valid.PublicEndpoint.EncryptionKeyPublicMaterial, Array.Empty<string>()));
            Assert.Throws<ArgumentNullException>("encryptionKeyPublicMaterial", () => new Endpoint(DateTime.UtcNow, Valid.PublicEndpoint.MessageReceivingEndpoint, Valid.PublicEndpoint.SigningKeyPublicMaterial, null!, Array.Empty<string>()));
        }

        [Fact]
        public void Equals_Tests()
        {
            var contact1 = new Endpoint(DateTime.UtcNow, Valid.MessageReceivingEndpoint, new byte[1], new byte[1], Array.Empty<string>());
            Assert.False(contact1.Equals(null));
            Assert.True(contact1.Equals(contact1));
            Assert.False(contact1.Equals(Valid.PublicEndpoint));

            contact1 = new Endpoint(DateTime.UtcNow, Valid.PublicEndpoint.MessageReceivingEndpoint, Valid.PublicEndpoint.SigningKeyPublicMaterial, Valid.PublicEndpoint.EncryptionKeyPublicMaterial, Array.Empty<string>());
            Assert.Equal(Valid.PublicEndpoint, contact1);
        }
    }
}
