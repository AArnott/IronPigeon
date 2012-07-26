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
			Assert.Throws<ArgumentNullException>(() => new Message(null, Constants.ValidExpirationUtc, Constants.ValidContentType));
			Assert.Throws<ArgumentException>(() => new Message(Constants.ValidStream, DateTime.Now, Constants.ValidContentType));
			Assert.Throws<ArgumentNullException>(() => new Message(Constants.ValidStream, Constants.ValidExpirationUtc, null));
			Assert.Throws<ArgumentException>(() => new Message(Constants.ValidStream, Constants.ValidExpirationUtc, string.Empty));
		}
	}
}
