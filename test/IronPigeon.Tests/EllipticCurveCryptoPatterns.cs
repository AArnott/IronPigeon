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
    using Xunit;

    /// <summary>
    /// This test class doesn't test IronPigeon, but rather demonstrates how EC crypto works.
    /// </summary>
    [Trait("TestCategory", "EC")]
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
        [Fact]
        public async Task ECAsymmetricSigningAndEncryption()
        {
            using var bob = new ECDsaCng(521);
            var bobPublic = CngKey.Import(bob.Key.Export(CngKeyBlobFormat.EccPublicBlob), CngKeyBlobFormat.EccPublicBlob);
            using var alice = new ECDsaCng(521);
            var alicePublic = CngKey.Import(alice.Key.Export(CngKeyBlobFormat.EccPublicBlob), CngKeyBlobFormat.EccPublicBlob);

            // Bob formulates request.
            using var bobRequest = new MemoryStream();
            using var bobDH = ECDiffieHellman.Create();
            {
                byte[] bobPublicDH = bobDH.PublicKey.ToByteArray();
                byte[] bobSignedDH = bob.SignData(bobPublicDH);
                await bobRequest.WriteSizeAndBufferAsync(bobPublicDH, CancellationToken.None);
                await bobRequest.WriteSizeAndBufferAsync(bobSignedDH, CancellationToken.None);
                bobRequest.Position = 0;
            }

            // Alice reads request.
            using var aliceResponse = new MemoryStream();
            byte[] aliceKeyMaterial;
            using var aliceDH = new ECDiffieHellmanCng();
            {
                byte[] bobPublicDH = await bobRequest.ReadSizeAndBufferAsync(CancellationToken.None);
                byte[] bobSignedDH = await bobRequest.ReadSizeAndBufferAsync(CancellationToken.None);
                using var bobDsa = new ECDsaCng(bobPublic);
                Assert.True(bobDsa.VerifyData(bobPublicDH, bobSignedDH));
                ECDiffieHellmanPublicKey? bobDHPK = ECDiffieHellmanCngPublicKey.FromByteArray(bobPublicDH, CngKeyBlobFormat.EccPublicBlob);
                aliceKeyMaterial = aliceDH.DeriveKeyMaterial(bobDHPK);

                await aliceResponse.WriteSizeAndBufferAsync(aliceDH.PublicKey.ToByteArray(), CancellationToken.None);
                await aliceResponse.WriteSizeAndBufferAsync(alice.SignData(aliceDH.PublicKey.ToByteArray()), CancellationToken.None);

                // Alice also adds a secret message.
                using (var aes = SymmetricAlgorithm.Create())
                {
                    using (ICryptoTransform? encryptor = aes.CreateEncryptor(aliceKeyMaterial, new byte[aes.BlockSize / 8]))
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
                using var aliceDsa = new ECDsaCng(alicePublic);
                Assert.True(aliceDsa.VerifyData(alicePublicDH, aliceSignedDH));
                ECDiffieHellmanPublicKey? aliceDHPK = ECDiffieHellmanCngPublicKey.FromByteArray(alicePublicDH, CngKeyBlobFormat.EccPublicBlob);
                bobKeyMaterial = bobDH.DeriveKeyMaterial(aliceDHPK);

                // And Bob reads Alice's secret message.
                using (var aes = SymmetricAlgorithm.Create())
                {
                    using (ICryptoTransform? decryptor = aes.CreateDecryptor(aliceKeyMaterial, new byte[aes.BlockSize / 8]))
                    {
                        using var plaintext = new MemoryStream();
                        Stream? substream = await aliceResponse.ReadSizeAndStreamAsync(CancellationToken.None);
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

            Assert.Equal<byte>(aliceKeyMaterial, bobKeyMaterial);
        }

        [Fact]
        public void ParameterizedAlgorithms()
        {
            using var aa = ECDiffieHellman.Create();
            ECParameters pub = aa.ExportParameters(includePrivateParameters: false);
            ECParameters priv = aa.ExportParameters(includePrivateParameters: true);
            Console.WriteLine(priv);
            Console.WriteLine(pub);
        }
    }
}
