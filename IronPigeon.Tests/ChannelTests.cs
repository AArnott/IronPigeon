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
			Assert.Throws<ArgumentNullException>(() => channel.PostAsync(null, Constants.ValidExpirationUtc, Constants.ValidContentType, Constants.OneValidRecipient));
			Assert.Throws<ArgumentException>(() => channel.PostAsync(Constants.ValidStream, DateTime.Now, Constants.ValidContentType, Constants.OneValidRecipient));
			Assert.Throws<ArgumentNullException>(() => channel.PostAsync(Constants.ValidStream, Constants.ValidExpirationUtc, null, Constants.OneValidRecipient));
			Assert.Throws<ArgumentException>(() => channel.PostAsync(Constants.ValidStream, Constants.ValidExpirationUtc, string.Empty, Constants.OneValidRecipient));
			Assert.Throws<ArgumentNullException>(() => channel.PostAsync(Constants.ValidStream, Constants.ValidExpirationUtc, Constants.ValidContentType, null));
			Assert.Throws<ArgumentException>(() => channel.PostAsync(Constants.ValidStream, Constants.ValidExpirationUtc, Constants.ValidContentType, Constants.EmptyRecipients));
		}
	}
}
