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

		[SetUp]
		public void Setup() {
			this.logger = new Mocks.LoggerMock();
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

				var sentMessage = Valid.Message;
				await this.SendMessageAsync(cloudStorage, inboxMock, sender, receiver.PublicEndpoint, sentMessage);
				var messages = await this.ReceiveMessageAsync(cloudStorage, inboxMock, receiver);

				Assert.That(messages.Count, Is.EqualTo(1));
				var receivedMessage = messages.Single();
				Assert.That(receivedMessage.ContentType, Is.EqualTo(sentMessage.ContentType));
				Assert.That(receivedMessage.Content, Is.EqualTo(sentMessage.Content));
			}).GetAwaiter().GetResult();
		}

		private async Task SendMessageAsync(Mocks.CloudBlobStorageProviderMock cloudBlobStorage, Mocks.InboxHttpHandlerMock inboxMock, OwnEndpoint sender, Endpoint receiver, Message message) {
			Requires.NotNull(cloudBlobStorage, "cloudBlobStorage");
			Requires.NotNull(sender, "sender");
			Requires.NotNull(message, "message");

			var httpHandler = new Mocks.HttpMessageHandlerMock();

			cloudBlobStorage.AddHttpHandler(httpHandler);
			inboxMock.Register(httpHandler);

			var channel = new Channel() {
				HttpMessageHandler = httpHandler,
				CloudBlobStorage = cloudBlobStorage,
				CryptoServices = new Mocks.MockCryptoProvider(),
				Endpoint = sender,
				Logger = this.logger,
			};

			await channel.PostAsync(Valid.Message, new[] { receiver }, Valid.ExpirationUtc);
		}

		private async Task<IReadOnlyCollection<Message>> ReceiveMessageAsync(Mocks.CloudBlobStorageProviderMock cloudBlobStorage, Mocks.InboxHttpHandlerMock inboxMock, OwnEndpoint receiver) {
			Requires.NotNull(cloudBlobStorage, "cloudBlobStorage");
			Requires.NotNull(receiver, "receiver");

			var httpHandler = new Mocks.HttpMessageHandlerMock();

			cloudBlobStorage.AddHttpHandler(httpHandler);
			inboxMock.Register(httpHandler);

			var channel = new Channel {
				HttpMessageHandler = httpHandler,
				CloudBlobStorage = cloudBlobStorage,
				CryptoServices = new Mocks.MockCryptoProvider(),
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
