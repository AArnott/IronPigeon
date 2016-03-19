// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests
{
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
    public class EllipticCurveCryptoPatterns
    {
        /// <summary>
        /// Demonstrates using elliptic curve cryptography to
        /// establish an encrypted and signed channel between two parties
        /// using asymmetric cryptography.
        /// This is NOT a demonstration of perfect forward secrecy because
        /// the key used to encrypt the channel can be determined later
        /// by compromising one of the private keys.
        /// To be perfect forward secrecy, Bob and Alice would likely have
        /// a long-lived, persisted and shared key pair for authentication,
        /// and then create another (ephemeral) key pair for encryption.
        /// This encryption key pair would be (ideally) short-lived and never
        /// persisted (so that it cannot be compromised). If it is compromised
        /// only those messages it was used to encrypt are compromised.
        /// If the long-lived (authentication) keys are compromised, they cannot be used to
        /// recover any encryption keys because encryption keys were never transmitted.
        /// </summary>
        [TestMethod, TestCategory("EC")]
        public async Task ECAsymmetricSigningAndEncryption()
        {
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

                // Alice also adds a secret message.
                using (var aes = SymmetricAlgorithm.Create())
                {
                    using (var encryptor = aes.CreateEncryptor(aliceKeyMaterial, new byte[aes.BlockSize / 8]))
                    {
                        var cipherText = new MemoryStream();
                        using (var cryptoStream = new CryptoStream(cipherText, encryptor, CryptoStreamMode.Write))
                        {
                            cryptoStream.Write(new byte[] { 0x1, 0x3, 0x2 }, 0, 3);
                            cryptoStream.FlushFinalBlock();
                            cipherText.Position = 0;
                            await aliceResponse.WriteSizeAndStreamAsync(cipherText, CancellationToken.None);
                        }
                    }
                }

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

                // And Bob reads Alice's secret message.
                using (var aes = SymmetricAlgorithm.Create())
                {
                    using (var decryptor = aes.CreateDecryptor(aliceKeyMaterial, new byte[aes.BlockSize / 8]))
                    {
                        var plaintext = new MemoryStream();
                        var substream = await aliceResponse.ReadSizeAndStreamAsync(CancellationToken.None);
                        using (var cryptoStream = new CryptoStream(substream, decryptor, CryptoStreamMode.Read))
                        {
                            await cryptoStream.CopyToAsync(plaintext);
                            plaintext.Position = 0;
                            byte[] secretMessage = new byte[1024];
                            int readBytes = plaintext.Read(secretMessage, 0, secretMessage.Length);
                        }
                    }
                }
            }

            CollectionAssert.AreEqual(aliceKeyMaterial, bobKeyMaterial);
        }

        [TestMethod, TestCategory("EC")]
        public void ParameterizedAlgorithms()
        {
            var aa = (ECDiffieHellmanCng)AsymmetricAlgorithm.Create("ECDiffieHellman");
            string priv = aa.PublicKey.ToXmlString();
            string pub = aa.ToXmlString(ECKeyXmlFormat.Rfc4050);
            Console.WriteLine(priv);
            Console.WriteLine(pub);
        }
    }
}
