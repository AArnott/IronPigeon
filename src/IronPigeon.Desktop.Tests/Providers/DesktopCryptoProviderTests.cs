namespace IronPigeon.Tests.Providers {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using IronPigeon.Providers;
	using NUnit.Framework;

	[TestFixture]
	public class DesktopCryptoProviderTests {
		[Test]
		public void HashAlgorithmName() {
			var provider = new DesktopCryptoProvider();
			Assert.That(provider.HashAlgorithmName, Is.EqualTo("SHA256")); // default
			provider.HashAlgorithmName = "SHA111";
			Assert.That(provider.HashAlgorithmName, Is.EqualTo("SHA111"));
		}
	}
}
