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
			Assert.That(contact.SigningKeyThumbprint, Is.Null);
		}
	}
}
