namespace IronPigeon.Tests {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using System.Web;
	using NUnit.Framework;

	[TestFixture]
	public class UtilitiesTests {
		[Test]
		public void Base64WebSafe() {
			var buffer = new byte[15];
			new Random().NextBytes(buffer);

			string expectedBase64 = Convert.ToBase64String(buffer);

			string web64 = Utilities.ToBase64WebSafe(buffer);
			string actualBase64 = Utilities.FromBase64WebSafe(web64);

			Assert.That(actualBase64, Is.EqualTo(expectedBase64));

			byte[] decoded = Convert.FromBase64String(actualBase64);
			Assert.That(decoded, Is.EqualTo(buffer));
		}

		[Test]
		public void CreateWebSafeBase64Thumbprint() {
			var buffer = new byte[] { 0x1 };
			var mockCrypto = new Mocks.MockCryptoProvider();
			Assert.Throws<ArgumentNullException>(() => CryptoProviderExtensions.CreateWebSafeBase64Thumbprint(null, buffer));
			Assert.Throws<ArgumentNullException>(() => CryptoProviderExtensions.CreateWebSafeBase64Thumbprint(mockCrypto, null));

			string thumbprint = CryptoProviderExtensions.CreateWebSafeBase64Thumbprint(mockCrypto, buffer);
			Assert.That(thumbprint, Is.EqualTo(Utilities.ToBase64WebSafe(mockCrypto.Hash(buffer))));
		}

		[Test]
		public void UrlEncode() {
			var data = new Dictionary<string, string> { { "a", "b" }, { "a=b&c", "e=f&g" }, };
			string urlEncoded = data.UrlEncode();
			Assert.That(urlEncoded, Is.EqualTo("a=b&a%3Db%26c=e%3Df%26g"));

			var decoded = HttpUtility.ParseQueryString(urlEncoded);
			Assert.That(decoded.Count, Is.EqualTo(data.Count));
			foreach (string key in decoded) {
				Assert.That(data[key], Is.EqualTo(decoded[key]));
			}
		}
	}
}
