namespace IronPigeon.Tests {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using NUnit.Framework;

	[TestFixture]
	public class PayloadReferenceTests {
		[Test]
		public void CtorInvalidInputs() {
			Assert.Throws<ArgumentNullException>(() => new PayloadReference(null, Constants.NonEmptyBuffer, Constants.NonEmptyBuffer, Constants.NonEmptyBuffer, DateTime.UtcNow, Constants.ValidContentType));
			Assert.Throws<ArgumentNullException>(() => new PayloadReference(Constants.ValidLocation, null, Constants.NonEmptyBuffer, Constants.NonEmptyBuffer, DateTime.UtcNow, Constants.ValidContentType));
			Assert.Throws<ArgumentNullException>(() => new PayloadReference(Constants.ValidLocation, Constants.NonEmptyBuffer, null, Constants.NonEmptyBuffer, DateTime.UtcNow, Constants.ValidContentType));
			Assert.Throws<ArgumentNullException>(() => new PayloadReference(Constants.ValidLocation, Constants.NonEmptyBuffer, Constants.NonEmptyBuffer, null, DateTime.UtcNow, Constants.ValidContentType));
			Assert.Throws<ArgumentException>(() => new PayloadReference(Constants.ValidLocation, Constants.NonEmptyBuffer, Constants.NonEmptyBuffer, Constants.NonEmptyBuffer, DateTime.Now, Constants.ValidContentType)); // throw due to Local time.
			Assert.Throws<ArgumentNullException>(() => new PayloadReference(Constants.ValidLocation, Constants.NonEmptyBuffer, Constants.NonEmptyBuffer, Constants.NonEmptyBuffer, DateTime.UtcNow, null));
			Assert.Throws<ArgumentException>(() => new PayloadReference(Constants.ValidLocation, Constants.EmptyBuffer, Constants.NonEmptyBuffer, Constants.NonEmptyBuffer, DateTime.UtcNow, Constants.ValidContentType));
			Assert.Throws<ArgumentException>(() => new PayloadReference(Constants.ValidLocation, Constants.NonEmptyBuffer, Constants.EmptyBuffer, Constants.NonEmptyBuffer, DateTime.UtcNow, Constants.ValidContentType));
			Assert.Throws<ArgumentException>(() => new PayloadReference(Constants.ValidLocation, Constants.NonEmptyBuffer, Constants.NonEmptyBuffer, Constants.EmptyBuffer, DateTime.UtcNow, Constants.ValidContentType));
			Assert.Throws<ArgumentException>(() => new PayloadReference(Constants.ValidLocation, Constants.NonEmptyBuffer, Constants.NonEmptyBuffer, Constants.NonEmptyBuffer, DateTime.UtcNow, string.Empty));
		}

		[Test]
		public void Ctor() {
			var hash = new byte[1];
			var key = new byte[1];
			var iv = new byte[1];
			var reference = new PayloadReference(Constants.ValidLocation, hash, key, iv, Constants.ValidExpirationUtc, Constants.ValidContentType);
			Assert.That(reference.Location, Is.SameAs(Constants.ValidLocation));
			Assert.That(reference.Hash, Is.SameAs(hash));
			Assert.That(reference.Key, Is.SameAs(key));
			Assert.That(reference.IV, Is.SameAs(iv));
			Assert.That(reference.ExpiresUtc, Is.EqualTo(Constants.ValidExpirationUtc));
			Assert.That(reference.ContentType, Is.EqualTo(Constants.ValidContentType));
		}
	}
}
