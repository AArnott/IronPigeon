namespace IronPigeon.Tests {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net.Http;
	using System.Text;
	using System.Threading.Tasks;

	using IronPigeon.Providers;
	using PCLCrypto;
	using Validation;
	using Xunit;

	public class InteropTests {
		private Mocks.LoggerMock logger;

		public void Setup() {
			this.logger = new Mocks.LoggerMock();
		}

		[Fact]
		public async Task CrossSecurityLevelAddressBookExchange() {
			var lowLevelCrypto = new CryptoSettings(SecurityLevel.Minimum);
			var lowLevelEndpoint = Valid.GenerateOwnEndpoint(lowLevelCrypto);

			var highLevelCrypto = new CryptoSettings(SecurityLevel.Minimum) { AsymmetricKeySize = 2048 };
			var highLevelEndpoint = Valid.GenerateOwnEndpoint(highLevelCrypto);

			await this.TestSendAndReceiveAsync(lowLevelCrypto, lowLevelEndpoint, highLevelCrypto, highLevelEndpoint);
			await this.TestSendAndReceiveAsync(highLevelCrypto, highLevelEndpoint, lowLevelCrypto, lowLevelEndpoint);
		}

		private async Task TestSendAndReceiveAsync(
			CryptoSettings senderCrypto, OwnEndpoint senderEndpoint, CryptoSettings receiverCrypto, OwnEndpoint receiverEndpoint) {
			var inboxMock = new Mocks.InboxHttpHandlerMock(new[] { receiverEndpoint.PublicEndpoint });
			var cloudStorage = new Mocks.CloudBlobStorageProviderMock();

			await this.SendMessageAsync(cloudStorage, inboxMock, senderCrypto, senderEndpoint, receiverEndpoint.PublicEndpoint);
			await this.ReceiveMessageAsync(cloudStorage, inboxMock, receiverCrypto, receiverEndpoint);
		}

		private async Task SendMessageAsync(Mocks.CloudBlobStorageProviderMock cloudStorage, Mocks.InboxHttpHandlerMock inboxMock, CryptoSettings senderCrypto, OwnEndpoint senderEndpoint, Endpoint receiverEndpoint) {
			Requires.NotNull(cloudStorage, "cloudStorage");
			Requires.NotNull(senderCrypto, "senderCrypto");
			Requires.NotNull(senderEndpoint, "senderEndpoint");
			Requires.NotNull(receiverEndpoint, "receiverEndpoint");

			var httpHandler = new Mocks.HttpMessageHandlerMock();

			cloudStorage.AddHttpHandler(httpHandler);

			inboxMock.Register(httpHandler);

			var sentMessage = Valid.Message;

			var channel = new Channel() {
				HttpClient = new HttpClient(httpHandler),
				CloudBlobStorage = cloudStorage,
				CryptoServices = senderCrypto,
				Endpoint = senderEndpoint,
				Logger = this.logger,
			};

			await channel.PostAsync(sentMessage, new[] { receiverEndpoint }, Valid.ExpirationUtc);
		}

		private async Task ReceiveMessageAsync(Mocks.CloudBlobStorageProviderMock cloudStorage, Mocks.InboxHttpHandlerMock inboxMock, CryptoSettings receiverCrypto, OwnEndpoint receiverEndpoint) {
			Requires.NotNull(cloudStorage, "cloudStorage");
			Requires.NotNull(receiverCrypto, "receiverCrypto");
			Requires.NotNull(receiverEndpoint, "receiverEndpoint");

			var httpHandler = new Mocks.HttpMessageHandlerMock();

			cloudStorage.AddHttpHandler(httpHandler);
			inboxMock.Register(httpHandler);

			var channel = new Channel {
				HttpClient = new HttpClient(httpHandler),
				HttpClientLongPoll = new HttpClient(httpHandler),
				CloudBlobStorage = cloudStorage,
				CryptoServices = receiverCrypto,
				Endpoint = receiverEndpoint,
				Logger = this.logger,
			};

			var messages = await channel.ReceiveAsync();
			Assert.Equal(1, messages.Count);
			Assert.Equal(Valid.Message, messages[0].Payload);
		}
	}
}
