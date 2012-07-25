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
			var emptyBuffer = new byte[0];
			var nonEmptyBuffer = new byte[1];
			var location = new Uri("http://localhost");
			string contentType = "some type";
			Assert.Throws<ArgumentNullException>(() => new PayloadReference(null, nonEmptyBuffer, nonEmptyBuffer, nonEmptyBuffer, DateTime.UtcNow, contentType));
			Assert.Throws<ArgumentNullException>(() => new PayloadReference(location, null, nonEmptyBuffer, nonEmptyBuffer, DateTime.UtcNow, contentType));
			Assert.Throws<ArgumentNullException>(() => new PayloadReference(location, nonEmptyBuffer, null, nonEmptyBuffer, DateTime.UtcNow, contentType));
			Assert.Throws<ArgumentNullException>(() => new PayloadReference(location, nonEmptyBuffer, nonEmptyBuffer, null, DateTime.UtcNow, contentType));
			Assert.Throws<ArgumentException>(() => new PayloadReference(location, nonEmptyBuffer, nonEmptyBuffer, nonEmptyBuffer, DateTime.Now, contentType)); // throw due to Local time.
			Assert.Throws<ArgumentNullException>(() => new PayloadReference(location, nonEmptyBuffer, nonEmptyBuffer, nonEmptyBuffer, DateTime.UtcNow, null));
			Assert.Throws<ArgumentException>(() => new PayloadReference(location, emptyBuffer, nonEmptyBuffer, nonEmptyBuffer, DateTime.UtcNow, contentType));
			Assert.Throws<ArgumentException>(() => new PayloadReference(location, nonEmptyBuffer, emptyBuffer, nonEmptyBuffer, DateTime.UtcNow, contentType));
			Assert.Throws<ArgumentException>(() => new PayloadReference(location, nonEmptyBuffer, nonEmptyBuffer, emptyBuffer, DateTime.UtcNow, contentType));
			Assert.Throws<ArgumentException>(() => new PayloadReference(location, nonEmptyBuffer, nonEmptyBuffer, nonEmptyBuffer, DateTime.UtcNow, string.Empty));
		}

		[Test]
		public void Ctor() {
			var location = new Uri("http://localhost");
			var hash = new byte[1];
			var key = new byte[1];
			var iv = new byte[1];
			var expiresUtc = DateTime.UtcNow;
			string contentType = "some type";
			var reference = new PayloadReference(location, hash, key, iv, expiresUtc, contentType);
			Assert.That(reference.Location, Is.SameAs(location));
			Assert.That(reference.Hash, Is.SameAs(hash));
			Assert.That(reference.Key, Is.SameAs(key));
			Assert.That(reference.IV, Is.SameAs(iv));
			Assert.That(reference.ExpiresUtc, Is.EqualTo(expiresUtc));
			Assert.That(reference.ContentType, Is.EqualTo(contentType));
		}
	}
}
