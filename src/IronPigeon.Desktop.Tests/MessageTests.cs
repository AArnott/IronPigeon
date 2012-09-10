namespace IronPigeon.Tests {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using NUnit.Framework;

	[TestFixture]
	public class MessageTests {
		[Test]
		public void CtorInvalidArgs() {
			Assert.Throws<ArgumentNullException>(() => new Payload(null, Valid.ContentType));
			Assert.Throws<ArgumentNullException>(() => new Payload(Valid.MessageContent, null));
			Assert.Throws<ArgumentException>(() => new Payload(Valid.MessageContent, string.Empty));
		}

		[Test]
		public void Ctor() {
			var message = new Payload(Valid.MessageContent, Valid.ContentType);
			Assert.That(message.Content, Is.SameAs(Valid.MessageContent));
			Assert.That(message.ContentType, Is.EqualTo(Valid.ContentType));
		}
	}
}
