namespace IronPigeon.Tests {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using NUnit.Framework;

	[TestFixture]
	public class EndpointTests {
		[Test]
		public void DefaultContactCtor() {
			var contact = new Endpoint();
			Assert.That(contact.MessageReceivingEndpoint, Is.Null);
			Assert.That(contact.EncryptionKeyPublicMaterial, Is.Null);
			Assert.That(contact.SigningKeyPublicMaterial, Is.Null);
			Assert.That(contact.CreatedOnUtc, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromMinutes(1)));
		}

		[Test]
		public void Equals() {
			var contact1 = new Endpoint();
			Assert.That(contact1.Equals(null), Is.False);
			Assert.That(contact1.Equals(contact1), Is.True);
			Assert.That(contact1.Equals(Valid.PublicEndpoint), Is.False);

			contact1.MessageReceivingEndpoint = Valid.PublicEndpoint.MessageReceivingEndpoint;
			contact1.SigningKeyPublicMaterial = Valid.PublicEndpoint.SigningKeyPublicMaterial;
			contact1.EncryptionKeyPublicMaterial = Valid.PublicEndpoint.EncryptionKeyPublicMaterial;
			contact1.HashAlgorithmName = Valid.PublicEndpoint.HashAlgorithmName;
			Assert.That(contact1, Is.EqualTo(Valid.PublicEndpoint));

			contact1.MessageReceivingEndpoint = null;
			Assert.That(contact1, Is.Not.EqualTo(Valid.PublicEndpoint));
			contact1.MessageReceivingEndpoint = Valid.PublicEndpoint.MessageReceivingEndpoint;

			contact1.SigningKeyPublicMaterial = null;
			Assert.That(contact1, Is.Not.EqualTo(Valid.PublicEndpoint));
			contact1.SigningKeyPublicMaterial = Valid.PublicEndpoint.SigningKeyPublicMaterial;

			contact1.EncryptionKeyPublicMaterial = null;
			Assert.That(contact1, Is.Not.EqualTo(Valid.PublicEndpoint));
			contact1.EncryptionKeyPublicMaterial = Valid.PublicEndpoint.EncryptionKeyPublicMaterial;
		}
	}
}
