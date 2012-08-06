namespace IronPigeon.Tests {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
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
	}
}
