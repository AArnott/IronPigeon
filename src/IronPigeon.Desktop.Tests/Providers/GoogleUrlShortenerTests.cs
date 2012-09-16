namespace IronPigeon.Tests.Providers {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net.Http;
	using System.Text;
	using System.Threading.Tasks;
	using IronPigeon.Providers;
	using NUnit.Framework;

	[TestFixture]
	public class GoogleUrlShortenerTests {
		private IUrlShortener shortener;

		[SetUp]
		public void SetUp() {
			var shortener = new GoogleUrlShortener();
			this.shortener = shortener;
			shortener.HttpClient = new HttpClient(Mocks.HttpMessageHandlerRecorder.CreatePlayback());
		}

		[Test]
		public void ShortenAsyncNull() {
			Assert.Throws<ArgumentNullException>(() => this.shortener.ShortenAsync(null).GetAwaiter().GetResult());
		}

		[Test]
		public void ShortenAsync() {
			Uri shortUrl = this.shortener.ShortenAsync(new Uri("http://www.google.com/")).GetAwaiter().GetResult();
			Assert.AreEqual("http://goo.gl/fbsS", shortUrl.AbsoluteUri);
		}

		[Test]
		public void ShortenExcludeFragmentAsync() {
			var shortUrl =
				this.shortener.ShortenExcludeFragmentAsync(new Uri("http://www.google.com/#hashtest")).GetAwaiter().GetResult();
			Assert.AreEqual("http://goo.gl/fbsS#hashtest", shortUrl.AbsoluteUri);
		}

		[Test]
		public void ShortenExcludeFragmentAsyncNoFragment() {
			var shortUrl =
				this.shortener.ShortenExcludeFragmentAsync(new Uri("http://www.google.com/")).GetAwaiter().GetResult();
			Assert.AreEqual("http://goo.gl/fbsS", shortUrl.AbsoluteUri);
		}
	}
}
