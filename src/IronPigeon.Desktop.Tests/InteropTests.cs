namespace IronPigeon.Tests {
	using System;
	using System.Collections.Generic;
	using System.Composition.Hosting;
	using System.Linq;
	using System.Net.Http;
	using System.Text;
	using System.Threading.Tasks;

	using IronPigeon.Providers;

	using Validation;
	using Xunit;

	public class InteropTests {
		private Mocks.LoggerMock logger;

		public void Setup() {
			this.logger = new Mocks.LoggerMock();
		}

		[Fact]
		public async Task CrossSecurityLevelAddressBookExchange() {
			var lowLevelCrypto = new DesktopCryptoProvider(new Level1());
			var lowLevelEndpoint = Valid.GenerateOwnEndpoint(lowLevelCrypto);

			var highLevelCrypto = new DesktopCryptoProvider(new Level2());
			var highLevelEndpoint = Valid.GenerateOwnEndpoint(highLevelCrypto);

			await this.TestSendAndReceiveAsync(lowLevelCrypto, lowLevelEndpoint, highLevelCrypto, highLevelEndpoint);
			await this.TestSendAndReceiveAsync(highLevelCrypto, highLevelEndpoint, lowLevelCrypto, lowLevelEndpoint);
		}

		private async Task TestSendAndReceiveAsync(
			ICryptoProvider senderCrypto, OwnEndpoint senderEndpoint, ICryptoProvider receiverCrypto, OwnEndpoint receiverEndpoint) {
			var inboxMock = new Mocks.InboxHttpHandlerMock(new[] { receiverEndpoint.PublicEndpoint });
			var cloudStorage = new Mocks.CloudBlobStorageProviderMock();

			await this.SendMessageAsync(cloudStorage, inboxMock, senderCrypto, senderEndpoint, receiverEndpoint.PublicEndpoint);
			await this.ReceiveMessageAsync(cloudStorage, inboxMock, receiverCrypto, receiverEndpoint);
		}

		private async Task SendMessageAsync(Mocks.CloudBlobStorageProviderMock cloudStorage, Mocks.InboxHttpHandlerMock inboxMock, ICryptoProvider senderCrypto, OwnEndpoint senderEndpoint, Endpoint receiverEndpoint) {
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

		private async Task ReceiveMessageAsync(Mocks.CloudBlobStorageProviderMock cloudStorage, Mocks.InboxHttpHandlerMock inboxMock, ICryptoProvider receiverCrypto, OwnEndpoint receiverEndpoint) {
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

		/// <summary>
		/// A security level using SHA1 and RSA keys just under 1024 in size.
		/// </summary>
		private class Level1 : SecurityLevel {
			/// <summary>
			/// Gets the name of the hash algorithm to use for symmetric signatures.
			/// </summary>
			/// <value>
			/// The name of the hash algorithm.
			/// </value>
			public override string SymmetricHashAlgorithmName {
				get { return "SHA1"; }
			}

			/// <summary>
			/// Gets the name of the hash algorithm to use for asymmetric signatures.
			/// </summary>
			/// <value>
			/// The name of the hash algorithm.
			/// </value>
			public override string AsymmetricHashAlgorithmName {
				get { return "SHA1"; }
			}

			/// <summary>
			/// Gets the name of the symmetric algorithm to use.
			/// </summary>
			public override EncryptionConfiguration SymmetricEncryptionConfiguration {
				get { return new EncryptionConfiguration("Rijndael", "CBC", "PKCS7"); }
			}

			/// <summary>
			/// Gets the size of the encryption asymmetric key.
			/// </summary>
			/// <value>
			/// The size of the encryption asymmetric key.
			/// </value>
			public override int EncryptionAsymmetricKeySize {
				get { return 1016; }
			}

			/// <summary>
			/// Gets the size of the signature asymmetric key.
			/// </summary>
			/// <value>
			/// The size of the signature asymmetric key.
			/// </value>
			public override int SignatureAsymmetricKeySize {
				get { return 1016; }
			}

			/// <summary>
			/// Gets the size of the BLOB symmetric key.
			/// </summary>
			/// <value>
			/// The size of the BLOB symmetric key.
			/// </value>
			public override int BlobSymmetricKeySize {
				get { return 128; }
			}

			/// <inheritdoc/>
			public override int ECDiffieHellmanKeySize {
				get { return 256; }
			}
		}

		/// <summary>
		/// A security level using SHA156 and RSA keys of 1024 in size.
		/// </summary>
		private class Level2 : SecurityLevel {
			/// <summary>
			/// Gets the name of the hash algorithm to use for symmetric signatures.
			/// </summary>
			/// <value>
			/// The name of the hash algorithm.
			/// </value>
			public override string SymmetricHashAlgorithmName {
				get { return "SHA256"; }
			}

			/// <summary>
			/// Gets the name of the hash algorithm to use for asymmetric signatures.
			/// </summary>
			/// <value>
			/// The name of the hash algorithm.
			/// </value>
			public override string AsymmetricHashAlgorithmName {
				get { return "SHA256"; }
			}

			/// <summary>
			/// Gets the name of the symmetric algorithm to use.
			/// </summary>
			public override EncryptionConfiguration SymmetricEncryptionConfiguration {
				get { return new EncryptionConfiguration("Rijndael", "CBC", "PKCS7"); }
			}

			/// <summary>
			/// Gets the size of the encryption asymmetric key.
			/// </summary>
			/// <value>
			/// The size of the encryption asymmetric key.
			/// </value>
			public override int EncryptionAsymmetricKeySize {
				get { return 1024; }
			}

			/// <summary>
			/// Gets the size of the signature asymmetric key.
			/// </summary>
			/// <value>
			/// The size of the signature asymmetric key.
			/// </value>
			public override int SignatureAsymmetricKeySize {
				get { return 1024; }
			}

			/// <summary>
			/// Gets the size of the BLOB symmetric key.
			/// </summary>
			/// <value>
			/// The size of the BLOB symmetric key.
			/// </value>
			public override int BlobSymmetricKeySize {
				get { return 192; }
			}

			/// <inheritdoc/>
			public override int ECDiffieHellmanKeySize {
				get { return 521; }
			}
		}
	}
}
