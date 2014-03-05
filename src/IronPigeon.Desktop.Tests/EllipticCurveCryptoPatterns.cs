namespace IronPigeon.Tests {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.VisualStudio.TestTools.UnitTesting;

	/// <summary>
	/// This test class doesn't test IronPigeon, but rather demonstrates how EC crypto works.
	/// </summary>
	[TestClass]
	public class EllipticCurveCryptoPatterns {
		[TestMethod, TestCategory("EC")]
		public async Task PerfectForwardSecrecy() {
			var bob = new ECDsaCng(521);
			var bobPublic = CngKey.Import(bob.Key.Export(CngKeyBlobFormat.EccPublicBlob), CngKeyBlobFormat.EccPublicBlob);
			var alice = new ECDsaCng(521);
			var alicePublic = CngKey.Import(alice.Key.Export(CngKeyBlobFormat.EccPublicBlob), CngKeyBlobFormat.EccPublicBlob);

			// Bob formulates request.
			var bobRequest = new MemoryStream();
			var bobDH = ECDiffieHellman.Create();
			{
				byte[] bobPublicDH = bobDH.PublicKey.ToByteArray();
				byte[] bobSignedDH = bob.SignData(bobPublicDH);
				await bobRequest.WriteSizeAndBufferAsync(bobPublicDH, CancellationToken.None);
				await bobRequest.WriteSizeAndBufferAsync(bobSignedDH, CancellationToken.None);
				bobRequest.Position = 0;
			}

			// Alice reads request.
			var aliceResponse = new MemoryStream();
			byte[] aliceKeyMaterial;
			var aliceDH = new ECDiffieHellmanCng();
			{
				byte[] bobPublicDH = await bobRequest.ReadSizeAndBufferAsync(CancellationToken.None);
				byte[] bobSignedDH = await bobRequest.ReadSizeAndBufferAsync(CancellationToken.None);
				var bobDsa = new ECDsaCng(bobPublic);
				Assert.IsTrue(bobDsa.VerifyData(bobPublicDH, bobSignedDH));
				var bobDHPK = ECDiffieHellmanCngPublicKey.FromByteArray(bobPublicDH, CngKeyBlobFormat.EccPublicBlob);
				aliceKeyMaterial = aliceDH.DeriveKeyMaterial(bobDHPK);

				await aliceResponse.WriteSizeAndBufferAsync(aliceDH.PublicKey.ToByteArray(), CancellationToken.None);
				await aliceResponse.WriteSizeAndBufferAsync(alice.SignData(aliceDH.PublicKey.ToByteArray()), CancellationToken.None);
				aliceResponse.Position = 0;
			}

			// Bob reads response
			byte[] bobKeyMaterial;
			{
				byte[] alicePublicDH = await aliceResponse.ReadSizeAndBufferAsync(CancellationToken.None);
				byte[] aliceSignedDH = await aliceResponse.ReadSizeAndBufferAsync(CancellationToken.None);
				var aliceDsa = new ECDsaCng(alicePublic);
				Assert.IsTrue(aliceDsa.VerifyData(alicePublicDH, aliceSignedDH));
				var aliceDHPK = ECDiffieHellmanCngPublicKey.FromByteArray(alicePublicDH, CngKeyBlobFormat.EccPublicBlob);
				bobKeyMaterial = bobDH.DeriveKeyMaterial(aliceDHPK);
			}

			CollectionAssert.AreEqual(aliceKeyMaterial, bobKeyMaterial);
		}

		[TestMethod, TestCategory("EC")]
		public void ParameterizedAlgorithms() {
			var aa = (ECDiffieHellmanCng)AsymmetricAlgorithm.Create("ECDiffieHellman");
			string priv = aa.PublicKey.ToXmlString();
			string pub = aa.ToXmlString(ECKeyXmlFormat.Rfc4050);
			Console.WriteLine(priv);
			Console.WriteLine(pub);
		}
	}
}
