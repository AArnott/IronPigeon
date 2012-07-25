namespace IronPigeon.Tests {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using NUnit.Framework;

	[TestFixture]
	public class SymmetricEncryptionResultTests {
		private static readonly byte[] EmptyBuffer = new byte[0];
		private static readonly byte[] NonEmptyBuffer = new byte[1];

		[Test]
		public void CtorThrowsOnNullBuffer() {
			Assert.Throws<ArgumentNullException>(() => new SymmetricEncryptionResult(null, NonEmptyBuffer, NonEmptyBuffer));
			Assert.Throws<ArgumentNullException>(() => new SymmetricEncryptionResult(NonEmptyBuffer, null, NonEmptyBuffer));
			Assert.Throws<ArgumentNullException>(() => new SymmetricEncryptionResult(NonEmptyBuffer, NonEmptyBuffer, null));
		}

		[Test]
		public void CtorThrowsOnEmptyBuffer() {
			Assert.Throws<ArgumentException>(() => new SymmetricEncryptionResult(EmptyBuffer, NonEmptyBuffer, NonEmptyBuffer));
			Assert.Throws<ArgumentException>(() => new SymmetricEncryptionResult(NonEmptyBuffer, EmptyBuffer, NonEmptyBuffer));
			Assert.Throws<ArgumentException>(() => new SymmetricEncryptionResult(NonEmptyBuffer, NonEmptyBuffer, EmptyBuffer));
		}

		[Test]
		public void CtorAcceptsValidArguments() {
			var key = new byte[1];
			var iv = new byte[1];
			var ciphertext = new byte[1];
			var result = new SymmetricEncryptionResult(key, iv, ciphertext);
			Assert.That(result.Key, Is.SameAs(key));
			Assert.That(result.IV, Is.SameAs(iv));
			Assert.That(result.Ciphertext, Is.SameAs(ciphertext));
		}
	}
}
