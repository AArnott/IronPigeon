namespace IronPigeon.Tests {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using NUnit.Framework;

	[TestFixture]
	public class ChannelTests {
		[Test]
		public void Ctor() {
			new Channel();
		}

		[Test]
		public void PostAsyncBadArgs() {
			var channel = new Channel();
			Assert.Throws<ArgumentNullException>(() => channel.PostAsync(null, Constants.OneValidRecipient));
			Assert.Throws<ArgumentNullException>(() => channel.PostAsync(Constants.ValidMessage, null));
			Assert.Throws<ArgumentException>(() => channel.PostAsync(Constants.ValidMessage, Constants.EmptyRecipients));
		}
	}
}
