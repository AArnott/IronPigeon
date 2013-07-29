namespace IronPigeon.Tests.Providers {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using IronPigeon.Providers;
	using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

	[TestClass]
	public class WinRTCryptoProviderTests : CryptoProviderTests {
		private WinRTCryptoProvider provider;

		protected override ICryptoProvider CryptoProvider {
			get { return this.provider; }
		}

		[TestInitialize]
		public void Setup() {
			this.provider = new WinRTCryptoProvider();
		}
	}
}
