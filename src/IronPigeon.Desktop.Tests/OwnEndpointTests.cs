namespace IronPigeon.Tests {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using NUnit.Framework;

	[TestFixture]
	public class OwnEndpointTests {
		[Test]
		public void CtorInvalidArgs() {
			Assert.Throws<ArgumentNullException>(() => new OwnEndpoint(null, Valid.ReceivingEndpoint.SigningKeyPrivateMaterial, Valid.ReceivingEndpoint.EncryptionKeyPrivateMaterial));
			Assert.Throws<ArgumentNullException>(() => new OwnEndpoint(Valid.PublicEndpoint, null, Valid.ReceivingEndpoint.EncryptionKeyPrivateMaterial));
			Assert.Throws<ArgumentNullException>(() => new OwnEndpoint(Valid.PublicEndpoint, Valid.ReceivingEndpoint.SigningKeyPrivateMaterial, null));
		}

		[Test]
		public void Ctor() {
			var ownContact = new OwnEndpoint(Valid.ReceivingEndpoint.PublicEndpoint, Valid.ReceivingEndpoint.SigningKeyPrivateMaterial, Valid.ReceivingEndpoint.EncryptionKeyPrivateMaterial);
			Assert.That(ownContact.PublicEndpoint, Is.SameAs(Valid.ReceivingEndpoint.PublicEndpoint));
			Assert.That(ownContact.EncryptionKeyPrivateMaterial, Is.SameAs(Valid.ReceivingEndpoint.EncryptionKeyPrivateMaterial));
			Assert.That(ownContact.SigningKeyPrivateMaterial, Is.SameAs(Valid.ReceivingEndpoint.SigningKeyPrivateMaterial));
		}

		[Test]
		public void CreateAddressBookEntryNullInput() {
			var ownContact = new OwnEndpoint(Valid.ReceivingEndpoint.PublicEndpoint, Valid.ReceivingEndpoint.SigningKeyPrivateMaterial, Valid.ReceivingEndpoint.EncryptionKeyPrivateMaterial);
			Assert.Throws<ArgumentNullException>(() => ownContact.CreateAddressBookEntry(null));
		}

		[Test]
		public void CreateAddressBookEntry() {
			var ownContact = Valid.ReceivingEndpoint;
			CryptoSettings cryptoServices = new CryptoSettings(SecurityLevel.Minimum);
			var entry = ownContact.CreateAddressBookEntry(cryptoServices);
			Assert.That(entry.Signature, Is.Not.Null.And.Not.Empty);
			Assert.That(entry.SerializedEndpoint, Is.Not.Null.And.Not.Empty);
		}
	}
}
