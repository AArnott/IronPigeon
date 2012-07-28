namespace IronPigeon.Tests {
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Net.Http;
	using System.Text;
	using System.Threading.Tasks;
	using NUnit.Framework;

	[TestFixture]
	public class ChannelTests {
		private Mocks.LoggerMock logger;

		private DesktopCryptoProvider desktopCryptoProvider;

		[SetUp]
		public void Setup() {
			this.logger = new Mocks.LoggerMock();
			this.desktopCryptoProvider = new DesktopCryptoProvider {
				EncryptionAsymmetricKeySize = 512, // use small key sizes so tests run faster
				SignatureAsymmetricKeySize = 512,
				BlobSymmetricKeySize = 128,
			};
		}

		[TearDown]
		public void Teardown() {
			if (TestContext.CurrentContext.Result.Status == TestStatus.Failed) {
				Console.WriteLine(this.logger.Messages);
			}
		}

		[Test]
		public void Ctor() {
			new Channel();
		}

		[Test]
		public void HttpMessageHandler() {
			var channel = new Channel();
			Assert.That(channel.HttpMessageHandler, Is.InstanceOf<HttpClientHandler>());
			var handler = new HttpClientHandler();
			channel.HttpMessageHandler = handler;
			Assert.That(channel.HttpMessageHandler, Is.SameAs(handler));
		}

		[Test]
		public void PostAsyncBadArgs() {
			var channel = new Channel();
			Assert.Throws<ArgumentNullException>(() => channel.PostAsync(null, Valid.OneEndpoint, Valid.ExpirationUtc).GetAwaiter().GetResult());
			Assert.Throws<ArgumentNullException>(() => channel.PostAsync(Valid.Message, null, Valid.ExpirationUtc).GetAwaiter().GetResult());
			Assert.Throws<ArgumentException>(() => channel.PostAsync(Valid.Message, Valid.EmptyEndpoints, Valid.ExpirationUtc).GetAwaiter().GetResult());
			Assert.Throws<ArgumentException>(() => channel.PostAsync(Valid.Message, Valid.OneEndpoint, Invalid.ExpirationUtc).GetAwaiter().GetResult());
		}

		[Test]
		public void PostAndReceiveAsync() {
			Task.Run(async delegate {
				var sender = Valid.GenerateOwnEndpoint();
				var receiver = Valid.GenerateOwnEndpoint();

				var cloudStorage = new Mocks.CloudBlobStorageProviderMock();
				var inboxMock = new Mocks.InboxHttpHandlerMock(new[] { receiver.PublicEndpoint });
				var cryptoProvider = new Mocks.MockCryptoProvider();

				var sentMessage = Valid.Message;
				await this.SendMessageAsync(cloudStorage, inboxMock, cryptoProvider, sender, receiver.PublicEndpoint, sentMessage);
				var messages = await this.ReceiveMessageAsync(cloudStorage, inboxMock, new Mocks.MockCryptoProvider(), receiver);

				Assert.That(messages.Count, Is.EqualTo(1));
				var receivedMessage = messages.Single();
				Assert.That(receivedMessage.ContentType, Is.EqualTo(sentMessage.ContentType));
				Assert.That(receivedMessage.Content, Is.EqualTo(sentMessage.Content));
			}).GetAwaiter().GetResult();
		}

		[Test]
		public void PayloadReferenceTamperingTests() {
			Task.Run(async delegate {
				var sender = Valid.GenerateOwnEndpoint(desktopCryptoProvider);
				var receiver = Valid.GenerateOwnEndpoint(desktopCryptoProvider);

				for (int i = 0; i < 100; i++) {
					var cloudStorage = new Mocks.CloudBlobStorageProviderMock();
					var inboxMock = new Mocks.InboxHttpHandlerMock(new[] { receiver.PublicEndpoint });

					var sentMessage = Valid.Message;
					await this.SendMessageAsync(cloudStorage, inboxMock, desktopCryptoProvider, sender, receiver.PublicEndpoint, sentMessage);

					// Tamper with the payload reference.
					ApplyFuzzing(inboxMock.Inboxes[receiver.PublicEndpoint][0].Item2, 1);

					Assert.Throws<InvalidMessageException>(() => this.ReceiveMessageAsync(cloudStorage, inboxMock, desktopCryptoProvider, receiver).GetAwaiter().GetResult()); ;
				}
			}).GetAwaiter().GetResult();
		}

		[Test]
		public void PayloadTamperingTests() {
			Task.Run(async delegate {
				var sender = Valid.GenerateOwnEndpoint(desktopCryptoProvider);
				var receiver = Valid.GenerateOwnEndpoint(desktopCryptoProvider);

				for (int i = 0; i < 100; i++) {
					var cloudStorage = new Mocks.CloudBlobStorageProviderMock();
					var inboxMock = new Mocks.InboxHttpHandlerMock(new[] { receiver.PublicEndpoint });

					var sentMessage = Valid.Message;
					await this.SendMessageAsync(cloudStorage, inboxMock, desktopCryptoProvider, sender, receiver.PublicEndpoint, sentMessage);

					// Tamper with the payload itself.
					ApplyFuzzing(cloudStorage.Blobs.Single().Value, 1);

					Assert.Throws<InvalidMessageException>(() => this.ReceiveMessageAsync(cloudStorage, inboxMock, desktopCryptoProvider, receiver).GetAwaiter().GetResult()); ;
				}
			}).GetAwaiter().GetResult();
		}

		private static void ApplyFuzzing(byte[] buffer, int bytesToChange) {
			var random = new Random();
			for (int i = 0; i < bytesToChange; i++) {
				int index = random.Next(buffer.Length);
				buffer[index] = (byte)unchecked(buffer[index] + 0x1);
			}
		}

		private async Task SendMessageAsync(Mocks.CloudBlobStorageProviderMock cloudBlobStorage, Mocks.InboxHttpHandlerMock inboxMock, ICryptoProvider cryptoProvider, OwnEndpoint sender, Endpoint receiver, Message message) {
			Requires.NotNull(cloudBlobStorage, "cloudBlobStorage");
			Requires.NotNull(sender, "sender");
			Requires.NotNull(message, "message");

			var httpHandler = new Mocks.HttpMessageHandlerMock();

			cloudBlobStorage.AddHttpHandler(httpHandler);
			inboxMock.Register(httpHandler);

			var channel = new Channel() {
				HttpMessageHandler = httpHandler,
				CloudBlobStorage = cloudBlobStorage,
				CryptoServices = cryptoProvider,
				Endpoint = sender,
				Logger = this.logger,
			};

			await channel.PostAsync(Valid.Message, new[] { receiver }, Valid.ExpirationUtc);
		}

		private async Task<IReadOnlyCollection<Message>> ReceiveMessageAsync(Mocks.CloudBlobStorageProviderMock cloudBlobStorage, Mocks.InboxHttpHandlerMock inboxMock, ICryptoProvider cryptoProvider, OwnEndpoint receiver) {
			Requires.NotNull(cloudBlobStorage, "cloudBlobStorage");
			Requires.NotNull(receiver, "receiver");

			var httpHandler = new Mocks.HttpMessageHandlerMock();

			cloudBlobStorage.AddHttpHandler(httpHandler);
			inboxMock.Register(httpHandler);

			var channel = new Channel {
				HttpMessageHandler = httpHandler,
				CloudBlobStorage = cloudBlobStorage,
				CryptoServices = cryptoProvider,
				Endpoint = receiver,
				Logger = this.logger,
			};

			var progressMessage = new TaskCompletionSource<Message>();
			var progress = new Progress<Message>(m => progressMessage.SetResult(m));

			var messages = await channel.ReceiveAsync(progress);
			Assert.That(messages.Count, Is.EqualTo(1));
			await progressMessage.Task;
			Assert.That(progressMessage.Task.Result, Is.SameAs(messages.Single()));
			return messages;
		}
	}
}
