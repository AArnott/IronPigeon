namespace IronPigeon.WinPhone8.Tests {
	using System;

	using IronPigeon.WinPhone8.Providers;

	using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

	[TestClass]
	public class WinPhone8CryptoProviderTests {
		private WinPhone8CryptoProvider provider;

		[TestInitialize]
		public void Setup() {
			this.provider = new WinPhone8CryptoProvider();
		}

		[TestMethod]
		public void SymmetricEncryptionRoundtrip() {
			var rng = new Random();
			byte[] plaintext = new byte[10000];
			rng.NextBytes(plaintext);
			var cipherPacket = this.provider.Encrypt(plaintext);
			byte[] decryptedPlaintext = this.provider.Decrypt(cipherPacket);
			CollectionAssert.AreEqual(plaintext, decryptedPlaintext);
		}
	}
}
