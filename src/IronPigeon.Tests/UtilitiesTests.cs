namespace IronPigeon.Tests {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
#if NETFX_CORE || WINDOWS_PHONE
	using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
	using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif
	using Validation;

	[TestClass]
	public class UtilitiesTests {
		[TestMethod]
		public void Base64WebSafe() {
			var buffer = new byte[15];
			new Random().NextBytes(buffer);

			string expectedBase64 = Convert.ToBase64String(buffer);

			string web64 = Utilities.ToBase64WebSafe(buffer);
			string actualBase64 = Utilities.FromBase64WebSafe(web64);

			Assert.AreEqual(expectedBase64, actualBase64);

			byte[] decoded = Convert.FromBase64String(actualBase64);
			Assert.IsTrue(Utilities.AreEquivalent(buffer, decoded));
		}

		[TestMethod]
		public async Task ReadStreamWithProgress() {
			var updates = new List<int>();
			var largeStream = new MemoryStream(new byte[1024 * 1024]);
			var progress = new MockProgress<int>(u => updates.Add(u));
			var progressStream = largeStream.ReadStreamWithProgress(progress);
			await progressStream.CopyToAsync(Stream.Null);
			Assert.AreNotEqual(0, updates.Count);
			for (int i = 1; i < updates.Count; i++) {
				Assert.IsTrue(updates[i] >= updates[i - 1]);
			}

			Assert.AreEqual(largeStream.Length, updates[updates.Count - 1]);
		}

		private class MockProgress<T> : IProgress<T> {
			private readonly Action<T> report;

			internal MockProgress(Action<T> report) {
				Requires.NotNull(report, "report");

				this.report = report;
			}
			
			public void Report(T value) {
				this.report(value);
			}
		}
	}
}
