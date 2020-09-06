// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests
{
    using System;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;

    public class OwnEndpointTests : TestBase, IAsyncLifetime
    {
        private readonly Mocks.MockEnvironment environment = new Mocks.MockEnvironment();
        private OwnEndpoint endpoint = null!; // InitializeAsync

        public OwnEndpointTests(ITestOutputHelper logger)
            : base(logger)
        {
        }

        public async Task InitializeAsync()
        {
            this.endpoint = await this.environment.CreateOwnEndpointAsync(this.TimeoutToken);
        }

        public Task DisposeAsync()
        {
            this.environment.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public void CtorInvalidArgs()
        {
            Assert.Throws<ArgumentNullException>("messageReceivingEndpoint", () => new OwnEndpoint(null!, this.endpoint.SigningKeyInputs, this.endpoint.DecryptionKeyInputs));
            Assert.Throws<ArgumentNullException>("signingKeyInputs", () => new OwnEndpoint(Valid.SampleMessageReceivingEndpoint, null!, this.endpoint.DecryptionKeyInputs));
            Assert.Throws<ArgumentNullException>("decryptionKeyInputs", () => new OwnEndpoint(Valid.SampleMessageReceivingEndpoint, this.endpoint.SigningKeyInputs, null!));
        }

        [Fact]
        public void Ctor()
        {
            var ownContact = new OwnEndpoint(this.endpoint.MessageReceivingEndpoint, this.endpoint.SigningKeyInputs, this.endpoint.DecryptionKeyInputs, this.endpoint.InboxOwnerCode);
            Assert.Equal(this.endpoint.PublicEndpoint, ownContact.PublicEndpoint);
            Assert.Equal(this.endpoint.DecryptionKeyInputs, ownContact.DecryptionKeyInputs);
            Assert.Equal(this.endpoint.SigningKeyInputs, ownContact.SigningKeyInputs);
            Assert.Equal(this.endpoint.InboxOwnerCode, ownContact.InboxOwnerCode);
        }

        [Fact]
        public void CreateAddressBookEntry()
        {
            AddressBookEntry? entry = new AddressBookEntry(this.endpoint);
            Assert.NotEqual(0, entry.Signature.Length);
            Assert.NotEqual(0, entry.SerializedEndpoint.Length);
        }
    }
}
