namespace IronPigeon.Tests {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using NUnit.Framework;

	[TestFixture]
	public class AddressBookTests {
		private ICryptoProvider desktopCryptoProvider;

		[SetUp]
		public void Setup() {
			this.desktopCryptoProvider = TestUtilities.CreateAuthenticCryptoProvider();
		}

		[Test]
		public void ExtractEndpointWithoutCrypto() {
			var addressBook = new Mocks.AddressBookMock();
			var entry = new AddressBookEntry();
			Assert.Throws<InvalidOperationException>(() => addressBook.ExtractEndpoint(entry));
		}

		[Test]
		public void ExtractEndpointNullArgument() {
			var addressBook = new Mocks.AddressBookMock();
			addressBook.CryptoServices = new Mocks.MockCryptoProvider();
			Assert.Throws<ArgumentNullException>(() => addressBook.ExtractEndpoint(null));
		}

		[Test]
		public void ExtractEndpoint() {
			var ownContact = new OwnEndpoint(Valid.ReceivingEndpoint.PublicEndpoint, Valid.ReceivingEndpoint.SigningKeyPrivateMaterial, Valid.ReceivingEndpoint.EncryptionKeyPrivateMaterial);
			var cryptoServices = new Mocks.MockCryptoProvider();
			var entry = ownContact.CreateAddressBookEntry(cryptoServices);

			var addressBook = new Mocks.AddressBookMock();
			addressBook.CryptoServices = cryptoServices;
			var endpoint = addressBook.ExtractEndpoint(entry);
			Assert.That(endpoint, Is.EqualTo(ownContact.PublicEndpoint));
		}

		[Test]
		public void ExtractEndpointDetectsTampering() {
			var ownContact = Valid.GenerateOwnEndpoint(this.desktopCryptoProvider);
			var entry = ownContact.CreateAddressBookEntry(this.desktopCryptoProvider);

			var addressBook = new Mocks.AddressBookMock();
			addressBook.CryptoServices = this.desktopCryptoProvider;

			var untamperedEndpoint = entry.SerializedEndpoint.CopyBuffer();
			for (int i = 0; i < 100; i++) {
				TestUtilities.ApplyFuzzing(entry.SerializedEndpoint, 1);
				Assert.Throws<BadAddressBookEntryException>(() => addressBook.ExtractEndpoint(entry));
				untamperedEndpoint.CopyBuffer(entry.SerializedEndpoint);
			}
		}
	}
}
