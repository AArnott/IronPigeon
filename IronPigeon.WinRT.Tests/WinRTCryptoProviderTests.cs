namespace IronPigeon.WinRT.Tests {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

	[TestClass]
	public class WinRTCryptoProviderTests {
		[TestMethod]
		public void Ctor() {
			var provider = new WinRTCryptoProvider();
		}
	}
}
