namespace IronPigeon.Tests {
	using System;
#if NETFX_CORE || WINDOWS_PHONE
	using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
	using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

	[TestClass]
	public abstract class CryptoProviderTests {
		private const string EncryptCompatTestCipher = "KnJEHw3HYQVgwCDPoYWI3kiMhhFJs20HL2+6L38ub6oXT17SQnNTdV9o+8BW4KTSdNspCall94jWiAAQqLn8Jumv74JnbuuClZkwClkjZOr43EjNUvDqCZFPPbhP3C8BqNRZkVZrJKizYZxUF3mPjoE1Wle4m1w+u9f7GD3pPiFqusSG63puHYMaaie3W9vVD3YF8+wraqNi9lpH9MFCtRMaTL/3AXAXLTgf6vQm8PvMkDLeu+vQ9IHi8385zYTE0V1OwIBTVFaN0tjT2VQMvLhwbdmZRk7l5XYfvx4OgZXZtNcARwgU184sOmYnHJPgQElrWMaV2ikv9T3qkL/HPtqEOsZMReCz8NykRhKi7WgYjXiHsj/XK4N5I7scrZikn25VRJSbcyFmSLXhmcuwIXJBdwdq9yPwWROxN66ge71VlWbRVwsieeKUne+MhSPCw6BnIkZsT0tfJ0/fiAZyFU/MefH4CiLs2nMIhilQv7FfTLRB1qOuj6cTKJkLiW+s97sRSd8HH1ifxfjfW87LPphxyLDNW2FmsHF0y7GlbRO8bgIyXVVh2HeMqULodq5jHU7vDhvgxwRpJVSwLKKYmaQoBvClMN976mW6HcPa8jnidxclnbHqk1qEHinNLVlLm+Y0wPy7sb+3MUODVjMd5Fc1wY8DM/wJsJRvxcKUAIN0jAsxlLGLVSn9SpZGpOKpMLu0koJp+x05QViS0NQbk4xn9lwVkWjeMKFgYZbEX8L+gbJYC9fcH5R6+Zp5HueGHDXs0+7L/FvApX1Olk7okqpkv4xa/bKZo04vSJQinxG4crHN0zSmu/BA6UfRrPKlIYZXtiKd4S92GUYrIKsYeThx4+AWysAZjc57vI4J545akZC6tIQ+Zb2yTbDgpl69CDOMwKsAnLRw4YE9UYupcniNkeCHPyKbcVMx//u0TTu0qg47i7EwncHkTjB/09GiBPxq7vtv7XMuQutu/ZYbT7dvD2ExIv7DfipgckLuSiNh3DgAbXwRor8u9RySiMdJ6FlBTvwHxDkv/a36rdiPyLv/kgyEiFgJr6B7liIyyfOllWLOvU8hfEMcRrgzur0M4EpEGVplywzSJbZQwEqbdqhkQkrmzk+S+eTCbeX/XeVMUPTLE/r3T1b2tUlOTE903FxmOzx6UXWVQuTz+B1/oJEqAw0n5MyjrGeR/0ah+I6+MersDv24o9qC1yxIE2rJkW3piFZwOTOQyiUnKRykNtsMsnANpWtLj2B1d1H2MjlCb3uswXmvOI/myLK6LnjRYvVVWlQ/RL4Goqa8jnGOPg5d3RHU3OCfHZceD1iKjSmV+4Wt34hi4YFJWW6UCzun";
		private const string EncryptCompatTestKey = "moqF3FLtXrgsBgZh482WlUjGmbk0EKwkJKTJ1txp/HU=";
		private const string EncryptCompatTestIV = "3Y4T+8qiTbskHIzaUzAqTg==";
		private const string EncryptCompatTestPlaintext = "9aRJ8S7c6YpScuqCe8Y7BVuYIR7uFDw7YLMlaqzKDfDHsQQlcfHE6Alw0diSCRUym3koR9hSoeBoDpB6tn/diGX5usnCFZRq0zkUfUNjqFee888zocUYdRAjvWNgSjKKk0lzmE5LcIXlvnOfPSOdBWU53MKvquaf4US5nlgxCokGien58BAIU4NJTeNReDfshrj9J21v2uhj07cKkdEstLPqmLM6oxVVMIk/ZV0CTmgMzBegM+Mbl64BlhxY/sGxHZgwZ+CDsuOjqPfkL3WLYIVO/cS6jIHQzgAEOAz4/DHHjUjE0uaIiRo0/YrsNmg5dTaXCgH895AXX5jff3nWqqzFHKEsx42exXSqhY/35JeYy8PWYfEvy5a4rHJOqQmgXVLdJhnGDa2o+ESNUXEAH3sARrO1/0m6llELdBoaUBxqCSc9Gb590r0VLmitonkkt433TWJW6l4IIo/Iu5mTjagnI0hKwwa9gnT2oNHhDfPX5ryNIwK6FB7JNPcutRYkgqC/hn8x2vOGOsrtX0qMJxWb8NTcBWqlbQ4pgI8Ojs1e0R0xIYZ0pgzJBug3zLTUPQzhksRCR3w1GRhwCFljeKv9j40tWgWOJ0rHE/+CbqBaVUODLPwIG4qg+whuBJ2zZY48OjwmpuIGU2t6ejR9s6t3FrMW4Euv9XQDjF79ICOsFK1wRTNkM65gfNmRjqFVI0hc9u7qnIVDjUTIIhqDCTNIzWWGDG53E+IEtWRxHmfTrmkWoCo0wngoLbfumxGU3HOmScA7zhbZ5OIL5eYg0W/d9XSxbGgJEwvtXt+2iXjRcNjxy9Sml99SPLxHKdyRB1tIEKWc4nAKnTa1TXA3I1tqOnQNLwaaN12VXjCswekuXFaDbnjSNI1o5i2e8EpuGxlg3O5og9dVrdlIB4Q/XttMh/AatLXjmAbmpUnZ+0YSJkMWTOT7xdl+LsCeiSwJKYhRUmlaVGdkfETngd6OfLKTNeFAKBL2UMEXm4X0RQ7Z3HvBh6ncuNHcss+gE0T68LIOClRLyQB2WgKnQrnhbzc1DqRizPQItnwPZHVCVjx9HarT5jq6jhN7SyynNXYY+0OYawWflyO9BVTXO2WKMwTFKiUODYj8e4CR5oHHYYEG92gyqeo33oWHFeHm4lKu8Rcn4gpQCu27m++FDrGh9aPS0UT8V4kZjUyghvemFpkt4vVpoH92tex6dw77ss/U5SA+ExAOsZmNDlS+sIP2FIVWVyu7N1UjWxxXgeB7Htt8zKHXaAKIHzBb3JEruAVohGKIpFqkGK3KGrj0tjC4Eu95QuTiHquDzTj6X1vD+KMX7/x983qA1w==";

		protected abstract ICryptoProvider CryptoProvider { get; }

		[TestMethod]
		public void SymmetricEncryptionCompatTest() {
			var encryptionResult = new SymmetricEncryptionResult(
				Convert.FromBase64String(EncryptCompatTestKey),
				Convert.FromBase64String(EncryptCompatTestIV),
				Convert.FromBase64String(EncryptCompatTestCipher));
			byte[] plaintext = this.CryptoProvider.Decrypt(encryptionResult);
			byte[] expectedPlaintext = Convert.FromBase64String(EncryptCompatTestPlaintext);
			CollectionAssert.AreEqual(expectedPlaintext, plaintext);
		}

		[TestMethod]
		public void SymmetricEncryptionRoundtrip() {
			var rng = new Random();
			byte[] plaintext = new byte[10000];
			rng.NextBytes(plaintext);
			var cipherPacket = this.CryptoProvider.Encrypt(plaintext);
			byte[] decryptedPlaintext = this.CryptoProvider.Decrypt(cipherPacket);
			CollectionAssert.AreEqual(plaintext, decryptedPlaintext);
		}

		[TestMethod]
		public void SymmetricEncryptionRoundtripExplicitKeyAndIV() {
			byte[] key = new byte[this.CryptoProvider.SymmetricEncryptionKeySize / 8];
			byte[] iv = new byte[this.CryptoProvider.SymmetricEncryptionBlockSize / 8];
			byte[] plaintext = new byte[10000];

			var rng = new Random();
			rng.NextBytes(key);
			rng.NextBytes(iv);
			rng.NextBytes(plaintext);

			var cipherPacket = this.CryptoProvider.Encrypt(plaintext, key, iv);
			CollectionAssert.AreEqual(key, cipherPacket.Key);
			CollectionAssert.AreEqual(iv, cipherPacket.IV);

			byte[] decryptedPlaintext = this.CryptoProvider.Decrypt(cipherPacket);
			CollectionAssert.AreEqual(plaintext, decryptedPlaintext);
		}

		[TestMethod]
		public void AsymmetricSignatures() {
			var data = new byte[] { 0x1, 0x2, 0x3 };
			var tamperedData = new byte[] { 0x1, 0x2, 0x4 };
			byte[] keyPair, publicKey;
			this.CryptoProvider.GenerateSigningKeyPair(out keyPair, out publicKey);
			byte[] signature = this.CryptoProvider.Sign(data, keyPair);
			Assert.IsTrue(this.CryptoProvider.VerifySignature(publicKey, data, signature, this.CryptoProvider.AsymmetricHashAlgorithmName));
			Assert.IsFalse(this.CryptoProvider.VerifySignature(publicKey, tamperedData, signature, this.CryptoProvider.AsymmetricHashAlgorithmName));
		}

		[TestMethod]
		public void AsymmetricSignaturesMinimumSecurity() {
			var data = new byte[] { 0x1, 0x2, 0x3 };
			var tamperedData = new byte[] { 0x1, 0x2, 0x4 };
			byte[] keyPair, publicKey;
			this.CryptoProvider.ApplySecurityLevel(SecurityLevel.Minimum);
			this.CryptoProvider.GenerateSigningKeyPair(out keyPair, out publicKey);
			byte[] signature = this.CryptoProvider.Sign(data, keyPair);
			Assert.IsTrue(this.CryptoProvider.VerifySignature(publicKey, data, signature, this.CryptoProvider.AsymmetricHashAlgorithmName));
			Assert.IsFalse(this.CryptoProvider.VerifySignature(publicKey, tamperedData, signature, this.CryptoProvider.AsymmetricHashAlgorithmName));
		}
	}
}
