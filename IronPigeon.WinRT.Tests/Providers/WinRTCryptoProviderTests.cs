namespace IronPigeon.Tests.Providers {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using IronPigeon.Providers;
	using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

	[TestClass]
	public class WinRTCryptoProviderTests {
		[TestMethod]
		public void Ctor() {
			var provider = new WinRTCryptoProvider();
		}
	}
}
