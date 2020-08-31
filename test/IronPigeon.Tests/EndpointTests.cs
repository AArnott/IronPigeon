// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests
{
    using System;
    using Xunit;
    using Xunit.Abstractions;

    public class EndpointTests : TestBase
    {
        public EndpointTests(ITestOutputHelper logger)
            : base(logger)
        {
        }

        [Fact]
        public void CtorTests()
        {
            Assert.Throws<ArgumentNullException>("messageReceivingEndpoint", () => new Endpoint(null!, Valid.PublicEndpoint.AuthenticatingKeyInputs, Valid.PublicEndpoint.EncryptionKeyInputs));
            Assert.Throws<ArgumentNullException>("signingKeyInputs", () => new Endpoint(Valid.PublicEndpoint.MessageReceivingEndpoint, null!, Valid.PublicEndpoint.EncryptionKeyInputs));
            Assert.Throws<ArgumentNullException>("encryptionKeyInputs", () => new Endpoint(Valid.PublicEndpoint.MessageReceivingEndpoint, Valid.PublicEndpoint.AuthenticatingKeyInputs, null!));
        }

        [Fact]
        public void Equals_Tests()
        {
            var contact1 = new Endpoint(Valid.MessageReceivingEndpoint, Valid.SigningKeyInputs, Valid.DecryptionKeyInputs);
            Assert.False(contact1.Equals(null));
            Assert.True(contact1.Equals(contact1));
            Assert.False(contact1.Equals(Valid.PublicEndpoint));

            contact1 = new Endpoint(Valid.PublicEndpoint.MessageReceivingEndpoint, Valid.PublicEndpoint.AuthenticatingKeyInputs, Valid.PublicEndpoint.EncryptionKeyInputs);
            Assert.Equal(Valid.PublicEndpoint, contact1);
        }
    }
}
