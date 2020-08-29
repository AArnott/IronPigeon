// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using IronPigeon.Relay;
    using IronPigeon.Tests.Mocks;
    using Moq;

    internal static class Valid
    {
        internal const string ContactIdentifier = "some identifier";
        internal const string ContentType = "some type";
        internal const string HashAlgorithmName = "SHA1";
        internal static readonly byte[] Hash = new byte[1];
        internal static readonly byte[] Key = new byte[1];
        internal static readonly byte[] IV = new byte[1];

        internal static readonly Uri Location = new Uri("http://localhost/");
        internal static readonly DateTime ExpirationUtc = DateTime.UtcNow.AddDays(1);
        internal static readonly byte[] MessageContent = new byte[] { 0x11, 0x22, 0x33 };
        internal static readonly Payload Message = new Payload(MessageContent, ContentType);

        internal static readonly Uri MessageReceivingEndpoint = new Uri("http://localhost/inbox/someone");
        internal static readonly OwnEndpoint ReceivingEndpoint = GenerateOwnEndpoint();
        internal static readonly Endpoint PublicEndpoint = GenerateOwnEndpoint().PublicEndpoint!;
        internal static readonly Endpoint[] OneEndpoint = new Endpoint[] { PublicEndpoint! };
        internal static readonly Endpoint[] EmptyEndpoints = Array.Empty<Endpoint>();

        internal static OwnEndpoint GenerateOwnEndpoint(CryptoSettings? cryptoProvider = null)
        {
            cryptoProvider = cryptoProvider ?? new CryptoSettings(SecurityLevel.Minimum);

            var inboxFactory = new EndpointInboxFactoryMock(
                new InboxCreationResponse(MessageReceivingEndpoint.AbsoluteUri, "some owner code"));
            var endpointServices = new OwnEndpointServices
            {
                Channel = new Channel { CryptoServices = cryptoProvider },
                EndpointInboxFactory = inboxFactory,
            };

            OwnEndpoint? ownContact = endpointServices.CreateAsync().Result;
            return ownContact;
        }
    }
}
