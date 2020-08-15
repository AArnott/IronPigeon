// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft;
    using PCLCrypto;

    /// <summary>
    /// Extension methods to the <see cref="CryptoSettings"/> interface.
    /// </summary>
    public static class CryptoProviderExtensions
    {
        /// <summary>
        /// Creates a web safe base64 thumbprint of some buffer.
        /// </summary>
        /// <param name="cryptoProvider">The crypto provider.</param>
        /// <param name="buffer">The buffer.</param>
        /// <returns>A string representation of a hash of the <paramref name="buffer"/>.</returns>
        public static string CreateWebSafeBase64Thumbprint(this CryptoSettings cryptoProvider, byte[] buffer)
        {
            Requires.NotNull(cryptoProvider, nameof(cryptoProvider));
            Requires.NotNull(buffer, nameof(buffer));

            IHashAlgorithmProvider? hasher = WinRTCrypto.HashAlgorithmProvider.OpenAlgorithm(cryptoProvider.SymmetricHashAlgorithm);
            var hash = hasher.HashData(buffer);
            return Utilities.ToBase64WebSafe(hash);
        }

        /// <summary>
        /// Determines whether a given thumbprint matches the actual hash of the specified buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="allegedHashWebSafeBase64Thumbprint">The web-safe base64 encoding of the thumbprint that the specified buffer's thumbprint is expected to match.</param>
        /// <returns><c>true</c> if the thumbprints match; <c>false</c> otherwise.</returns>
        /// <exception cref="System.NotSupportedException">If the length of the thumbprint is not consistent with any supported hash algorithm.</exception>
        public static bool IsThumbprintMatch(byte[] buffer, string allegedHashWebSafeBase64Thumbprint)
        {
            Requires.NotNull(buffer, nameof(buffer));
            Requires.NotNullOrEmpty(allegedHashWebSafeBase64Thumbprint, nameof(allegedHashWebSafeBase64Thumbprint));

            byte[] allegedThumbprint = Convert.FromBase64String(Utilities.FromBase64WebSafe(allegedHashWebSafeBase64Thumbprint));
            HashAlgorithm hashAlgorithm = Utilities.GuessHashAlgorithmFromLength(allegedThumbprint.Length);

            IHashAlgorithmProvider? hasher = WinRTCrypto.HashAlgorithmProvider.OpenAlgorithm(hashAlgorithm);
            var actualThumbprint = hasher.HashData(buffer);
            return Utilities.AreEquivalent(actualThumbprint, allegedThumbprint);
        }

        /// <summary>
        /// Symmetrically encrypts the specified buffer using a randomly generated key.
        /// </summary>
        /// <param name="cryptoProvider">The crypto provider.</param>
        /// <param name="data">The data to encrypt.</param>
        /// <param name="encryptionVariables">Optional encryption variables to use; or <c>null</c> to use randomly generated ones.</param>
        /// <returns>
        /// The result of the encryption.
        /// </returns>
        public static SymmetricEncryptionResult Encrypt(this CryptoSettings cryptoProvider, byte[] data, SymmetricEncryptionVariables? encryptionVariables = null)
        {
            Requires.NotNull(data, nameof(data));

            encryptionVariables = ThisOrNewEncryptionVariables(cryptoProvider, encryptionVariables);
            ICryptographicKey? symmetricKey = CryptoSettings.SymmetricAlgorithm.CreateSymmetricKey(encryptionVariables.Key);
            var cipherTextBuffer = WinRTCrypto.CryptographicEngine.Encrypt(symmetricKey, data, encryptionVariables.IV);
            return new SymmetricEncryptionResult(encryptionVariables, cipherTextBuffer);
        }

        /// <summary>
        /// Symmetrically encrypts a stream.
        /// </summary>
        /// <param name="cryptoProvider">The crypto provider.</param>
        /// <param name="plaintext">The stream of plaintext to encrypt.</param>
        /// <param name="ciphertext">The stream to receive the ciphertext.</param>
        /// <param name="encryptionVariables">An optional key and IV to use. May be <c>null</c> to use randomly generated values.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>
        /// A task that completes when encryption has completed, whose result is the key and IV to use to decrypt the ciphertext.
        /// </returns>
        public static async Task<SymmetricEncryptionVariables> EncryptAsync(this CryptoSettings cryptoProvider, Stream plaintext, Stream ciphertext, SymmetricEncryptionVariables? encryptionVariables = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(plaintext, nameof(plaintext));
            Requires.NotNull(ciphertext, nameof(ciphertext));

            encryptionVariables = ThisOrNewEncryptionVariables(cryptoProvider, encryptionVariables);
            ICryptographicKey? key = CryptoSettings.SymmetricAlgorithm.CreateSymmetricKey(encryptionVariables.Key);
            using (ICryptoTransform? encryptor = WinRTCrypto.CryptographicEngine.CreateEncryptor(key, encryptionVariables.IV))
            {
                var cryptoStream = new CryptoStream(ciphertext, encryptor, CryptoStreamMode.Write);
                await plaintext.CopyToAsync(cryptoStream, 4096, cancellationToken).ConfigureAwait(false);
                cryptoStream.FlushFinalBlock();
            }

            return encryptionVariables;
        }

        /// <summary>
        /// Symmetrically decrypts a stream.
        /// </summary>
        /// <param name="cryptoProvider">The crypto provider.</param>
        /// <param name="ciphertext">The stream of ciphertext to decrypt.</param>
        /// <param name="plaintext">The stream to receive the plaintext.</param>
        /// <param name="encryptionVariables">The key and IV to use.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// </returns>
        public static async Task DecryptAsync(this CryptoSettings cryptoProvider, Stream ciphertext, Stream plaintext, SymmetricEncryptionVariables encryptionVariables, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(ciphertext, nameof(ciphertext));
            Requires.NotNull(plaintext, nameof(plaintext));
            Requires.NotNull(encryptionVariables, nameof(encryptionVariables));

            ICryptographicKey? key = CryptoSettings.SymmetricAlgorithm.CreateSymmetricKey(encryptionVariables.Key);
            using (ICryptoTransform? decryptor = WinRTCrypto.CryptographicEngine.CreateDecryptor(key, encryptionVariables.IV))
            {
                using var cryptoStream = new CryptoStream(plaintext, decryptor, CryptoStreamMode.Write);
                await ciphertext.CopyToAsync(cryptoStream, 4096, cancellationToken).ConfigureAwait(false);
                cryptoStream.FlushFinalBlock();
            }
        }

        /// <summary>
        /// Symmetrically decrypts a buffer using the specified key.
        /// </summary>
        /// <param name="cryptoProvider">The crypto provider.</param>
        /// <param name="data">The encrypted data and the key and IV used to encrypt it.</param>
        /// <returns>
        /// The decrypted buffer.
        /// </returns>
        public static byte[] Decrypt(this CryptoSettings cryptoProvider, SymmetricEncryptionResult data)
        {
            ICryptographicKey? symmetricKey = CryptoSettings.SymmetricAlgorithm.CreateSymmetricKey(data.Key);
            return WinRTCrypto.CryptographicEngine.Decrypt(symmetricKey, data.Ciphertext, data.IV);
        }

        /// <summary>
        /// Gets the name of the hash algorithm.
        /// </summary>
        /// <param name="algorithm">The algorithm.</param>
        /// <returns>A non-empty string.</returns>
        public static string GetHashAlgorithmName(this HashAlgorithm algorithm)
        {
            return algorithm.ToString();
        }

        /// <summary>
        /// Parses the name of the hash algorithm into an enum.
        /// </summary>
        /// <param name="algorithmName">Name of the algorithm. Null is allowed.</param>
        /// <returns>The parsed form, or null if <paramref name="algorithmName"/> is null.</returns>
        public static HashAlgorithm? ParseHashAlgorithmName(string algorithmName)
        {
            if (algorithmName == null)
            {
                return null;
            }

            return (HashAlgorithm)Enum.Parse(typeof(HashAlgorithm), algorithmName, true);
        }

        /// <summary>
        /// Computes the hash of the specified buffer and checks for a match to an expected hash.
        /// </summary>
        /// <param name="cryptoProvider">The crypto provider.</param>
        /// <param name="data">The data to hash.</param>
        /// <param name="expectedHash">The expected hash.</param>
        /// <param name="hashAlgorithm">The hash algorithm.</param>
        /// <returns>
        ///   <c>true</c> if the hashes came out equal; <c>false</c> otherwise.
        /// </returns>
        internal static bool IsHashMatchWithTolerantHashAlgorithm(this CryptoSettings cryptoProvider, byte[] data, byte[] expectedHash, HashAlgorithm? hashAlgorithm)
        {
            Requires.NotNull(cryptoProvider, nameof(cryptoProvider));
            Requires.NotNull(data, nameof(data));
            Requires.NotNull(expectedHash, nameof(expectedHash));

            if (!hashAlgorithm.HasValue)
            {
                hashAlgorithm = Utilities.GuessHashAlgorithmFromLength(expectedHash.Length);
            }

            IHashAlgorithmProvider? hasher = WinRTCrypto.HashAlgorithmProvider.OpenAlgorithm(hashAlgorithm.Value);
            byte[] actualHash = hasher.HashData(data);
            return Utilities.AreEquivalent(expectedHash, actualHash);
        }

        /// <summary>
        /// Verifies the asymmetric signature of some data blob.
        /// </summary>
        /// <param name="signingPublicKey">The public key used to verify the signature.</param>
        /// <param name="data">The data that was signed.</param>
        /// <param name="signature">The signature.</param>
        /// <param name="signingAlgorithm">The signing algorithm.</param>
        /// <returns>
        /// A value indicating whether the signature is valid.
        /// </returns>
        internal static bool VerifySignatureWithTolerantHashAlgorithm(byte[] signingPublicKey, byte[] data, byte[] signature, AsymmetricAlgorithm? signingAlgorithm = null)
        {
            if (signingAlgorithm.HasValue)
            {
                ICryptographicKey? key = WinRTCrypto.AsymmetricKeyAlgorithmProvider.OpenAlgorithm(signingAlgorithm.Value)
                    .ImportPublicKey(signingPublicKey, CryptoSettings.PublicKeyFormat);
                return WinRTCrypto.CryptographicEngine.VerifySignature(key, data, signature);
            }

            ICryptographicKey? key1 = WinRTCrypto.AsymmetricKeyAlgorithmProvider.OpenAlgorithm(AsymmetricAlgorithm.RsaSignPkcs1Sha1)
                .ImportPublicKey(signingPublicKey, CryptoSettings.PublicKeyFormat);
            ICryptographicKey? key2 = WinRTCrypto.AsymmetricKeyAlgorithmProvider.OpenAlgorithm(AsymmetricAlgorithm.RsaSignPkcs1Sha256)
                .ImportPublicKey(signingPublicKey, CryptoSettings.PublicKeyFormat);
            return WinRTCrypto.CryptographicEngine.VerifySignature(key1, data, signature)
                || WinRTCrypto.CryptographicEngine.VerifySignature(key2, data, signature);
        }

        /// <summary>
        /// Gets the signature provider.
        /// </summary>
        /// <param name="hashAlgorithm">The hash algorithm to use.</param>
        /// <returns>The asymmetric key provider.</returns>
        /// <exception cref="System.NotSupportedException">Thrown if the arguments are not supported.</exception>
        internal static AsymmetricAlgorithm GetSignatureProvider(string hashAlgorithm)
        {
            switch (hashAlgorithm)
            {
                case "SHA1":
                    return AsymmetricAlgorithm.RsaSignPkcs1Sha1;
                case "SHA256":
                    return AsymmetricAlgorithm.RsaSignPkcs1Sha256;
                case "SHA384":
                    return AsymmetricAlgorithm.RsaSignPkcs1Sha384;
                case "SHA512":
                    return AsymmetricAlgorithm.RsaSignPkcs1Sha512;
                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Generates a new set of encryption variables.
        /// </summary>
        /// <param name="cryptoProvider">The crypto provider.</param>
        /// <returns>
        /// A set of encryption variables.
        /// </returns>
        private static SymmetricEncryptionVariables NewSymmetricEncryptionVariables(CryptoSettings cryptoProvider)
        {
            byte[] key = WinRTCrypto.CryptographicBuffer.GenerateRandom(cryptoProvider.SymmetricKeySize / 8);
            byte[] iv = WinRTCrypto.CryptographicBuffer.GenerateRandom(CryptoSettings.SymmetricAlgorithm.BlockLength);
            return new SymmetricEncryptionVariables(key, iv);
        }

        /// <summary>
        /// Returns the specified encryption variables if they are non-null, or generates new ones.
        /// </summary>
        /// <param name="cryptoProvider">The crypto provider.</param>
        /// <param name="encryptionVariables">The encryption variables.</param>
        /// <returns>
        /// A valid set of encryption variables.
        /// </returns>
        private static SymmetricEncryptionVariables ThisOrNewEncryptionVariables(CryptoSettings cryptoProvider, SymmetricEncryptionVariables encryptionVariables)
        {
            if (encryptionVariables == null)
            {
                return NewSymmetricEncryptionVariables(cryptoProvider);
            }
            else
            {
                Requires.Argument(encryptionVariables.Key.Length == cryptoProvider.SymmetricKeySize / 8, "key", Strings.IncorrectLength);
                Requires.Argument(encryptionVariables.IV.Length == CryptoSettings.SymmetricAlgorithm.BlockLength, "iv", Strings.IncorrectLength);
                return encryptionVariables;
            }
        }
    }
}
