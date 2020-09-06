// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests
{
    using System;
    using Xunit;
    using Xunit.Abstractions;

    public class EndpointTests : TestBase
    {
        private readonly Mocks.MockEnvironment environment = new Mocks.MockEnvironment();
        private readonly OwnEndpoint endpoint;

        public EndpointTests(ITestOutputHelper logger)
            : base(logger)
        {
            this.endpoint = this.environment.CreateOwnEndpointAsync(this.TimeoutToken).Result;
        }

        [Fact]
        public void CtorTests()
        {
            Assert.Throws<ArgumentNullException>("messageReceivingEndpoint", () => new Endpoint(null!, this.endpoint.PublicEndpoint.AuthenticatingKeyInputs, this.endpoint.PublicEndpoint.EncryptionKeyInputs));
            Assert.Throws<ArgumentNullException>("signingKeyInputs", () => new Endpoint(this.endpoint.PublicEndpoint.MessageReceivingEndpoint, null!, this.endpoint.PublicEndpoint.EncryptionKeyInputs));
            Assert.Throws<ArgumentNullException>("encryptionKeyInputs", () => new Endpoint(this.endpoint.PublicEndpoint.MessageReceivingEndpoint, this.endpoint.PublicEndpoint.AuthenticatingKeyInputs, null!));
        }

        [Fact]
        public void Equals_Tests()
        {
            var contact1 = new Endpoint(Valid.SampleMessageReceivingEndpoint, this.endpoint.SigningKeyInputs.PublicKey, this.endpoint.DecryptionKeyInputs.PublicKey);
            Assert.False(contact1.Equals(null));
            Assert.True(contact1.Equals(contact1));
            Assert.False(contact1.Equals(this.endpoint.PublicEndpoint));

            contact1 = new Endpoint(this.endpoint.PublicEndpoint.MessageReceivingEndpoint, this.endpoint.PublicEndpoint.AuthenticatingKeyInputs, this.endpoint.PublicEndpoint.EncryptionKeyInputs);
            Assert.Equal(this.endpoint.PublicEndpoint, contact1);
        }
    }
}
