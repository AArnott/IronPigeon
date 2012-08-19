namespace IronPigeon.Tests.Providers {
	using IronPigeon.Providers;
	using IronPigeon.Tests;
	using NUnit.Framework;

	[TestFixture]
	public class TwitterAddressBookTests {
		private TwitterAddressBook twitter;

		[SetUp]
		public void SetUp() {
			this.twitter = new TwitterAddressBook();
			this.twitter.CryptoServices = TestUtilities.CreateAuthenticCryptoProvider();
			this.twitter.HttpMessageHandler = Mocks.HttpMessageHandlerRecorder.CreatePlayback();
		}

		[Test]
		public void LookupEntryAsyncNonExistentUser() {
			var endpoint = this.twitter.LookupAsync("@NonExistentUser2394872352").Result;
			Assert.That(endpoint, Is.Null);
		}

		[Test]
		public void LookupEntryAsyncValidUserWithNoEntry() {
			var endpoint = this.twitter.LookupAsync("@shanselman").Result;
			Assert.That(endpoint, Is.Null);
		}

		[Test]
		public void LookupEntryAsyncExistingUser() {
			this.twitter.CryptoServices.ApplySecurityLevel(SecurityLevel.Recommended);
			var endpoint = this.twitter.LookupAsync("@PrivacyNotFound").Result;
			Assert.That(endpoint, Is.Not.Null);
		}

		/// <summary>
		/// Verifies that the #fragment in the URL is verified to match the thumbprint of the downloaded address book entry.
		/// </summary>
		[Test]
		public void LookupEntryAsyncExistingUserReplacedEndpoint() {
			Assert.Throws<BadAddressBookEntryException>(() => this.twitter.LookupAsync("@PrivacyNotFound").GetAwaiter().GetResult());
		}
	}
}
