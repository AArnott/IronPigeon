namespace IronPigeon.Tests {
	using System;
	using System.IO;

	internal static class Valid {
		internal const string ContentType = "some type";
		internal static readonly byte[] Hash = new byte[1];
		internal static readonly byte[] Key = new byte[1];
		internal static readonly byte[] IV = new byte[1];

		internal static readonly Uri Location = new Uri("http://localhost/");
		internal static readonly DateTime ExpirationUtc = DateTime.UtcNow.AddDays(1);
		internal static readonly byte[] MessageContent = new byte[] { 0x11, 0x22, 0x33 };
		internal static readonly Payload Message = new Payload(MessageContent, ContentType);

		internal static readonly string ContactIdentifier = "some identifier";
		internal static readonly Uri MessageReceivingEndpoint = new Uri("http://localhost/inbox/someone");
		internal static readonly OwnEndpoint ReceivingEndpoint = GenerateOwnEndpoint();
		internal static readonly Endpoint PublicEndpoint = GenerateOwnEndpoint().PublicEndpoint;
		internal static readonly Endpoint[] OneEndpoint = new Endpoint[] { PublicEndpoint };
		internal static readonly Endpoint[] EmptyEndpoints = new Endpoint[0];

		internal static OwnEndpoint GenerateOwnEndpoint(ICryptoProvider cryptoProvider = null) {
			cryptoProvider = cryptoProvider ?? new Mocks.MockCryptoProvider();

			var ownContact = cryptoProvider.GenerateNewEndpoint();
			ownContact.PublicEndpoint.MessageReceivingEndpoint = MessageReceivingEndpoint;
			return ownContact;
		}
	}
}
