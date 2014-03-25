//-----------------------------------------------------------------------
// <copyright file="PclCryptoProvider.cs" company="Andrew Arnott">
//     Copyright (c) Andrew Arnott. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace IronPigeon
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using PCLCrypto;
    using Validation;

    /// <summary>
    /// An <see cref="ICryptoProvider"/> instance based on PclCrypto.
    /// </summary>
    [Export(typeof(ICryptoProvider))]
    [Shared]
    public class PclCryptoProvider : CryptoProviderBase
    {
        /// <summary>
        /// The asymmetric encryption algorithm provider to use.
        /// </summary>
        protected static readonly IAsymmetricKeyAlgorithmProvider EncryptionProvider = WinRTCrypto.AsymmetricKeyAlgorithmProvider.OpenAlgorithm(AsymmetricAlgorithm.RsaOaepSha1);

        /// <summary>
        /// The symmetric encryption algorithm provider to use.
        /// </summary>
        protected static readonly ISymmetricKeyAlgorithmProvider SymmetricAlgorithm = WinRTCrypto.SymmetricKeyAlgorithmProvider.OpenAlgorithm(PCLCrypto.SymmetricAlgorithm.AesCbcPkcs7);

        /// <summary>
        /// Initializes a new instance of the <see cref="PclCryptoProvider"/> class.
        /// </summary>
        public PclCryptoProvider()
            : this(SecurityLevel.Maximum)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PclCryptoProvider"/> class.
        /// </summary>
        /// <param name="securityLevel">The security level to apply to this instance.  The default is <see cref="SecurityLevel.Maximum"/>.</param>
        public PclCryptoProvider(SecurityLevel securityLevel)
        {
            Requires.NotNull(securityLevel, "securityLevel");
            securityLevel.Apply(this);
        }

        /// <summary>
        /// Gets the length (in bits) of the symmetric encryption cipher block.
        /// </summary>
        public override int SymmetricEncryptionBlockSize
        {
            get { return (int)SymmetricAlgorithm.BlockLength * 8; }
        }

        /// <summary>
        /// Derives a cryptographically strong key from the specified password.
        /// </summary>
        /// <param name="password">The user-supplied password.</param>
        /// <param name="salt">The salt.</param>
        /// <param name="iterations">The rounds of computation to use in deriving a stronger key. The larger this is, the longer attacks will take.</param>
        /// <param name="keySizeInBytes">The desired key size in bytes.</param>
        /// <returns>The generated key.</returns>
        public override byte[] DeriveKeyFromPassword(string password, byte[] salt, int iterations, int keySizeInBytes)
        {
            Requires.NotNullOrEmpty(password, "password");
            Requires.NotNull(salt, "salt");
            Requires.Range(iterations > 0, "iterations");
            Requires.Range(keySizeInBytes > 0, "keySizeInBytes");

            byte[] passwordBuffer = WinRTCrypto.CryptographicBuffer.ConvertStringToBinary(password, Encoding.UTF8);
            byte[] saltBuffer = salt;

            IKeyDerivationAlgorithmProvider keyDerivationProvider =
                WinRTCrypto.KeyDerivationAlgorithmProvider.OpenAlgorithm(KeyDerivationAlgorithm.Pbkdf2Sha1);
            IKeyDerivationParameters pbkdf2Parms =
                WinRTCrypto.KeyDerivationParameters.BuildForPbkdf2(saltBuffer, iterations);

            // create a key based on original key and derivation parameters
            ICryptographicKey keyOriginal = keyDerivationProvider.CreateKey(passwordBuffer);
            byte[] keyMaterial = WinRTCrypto.CryptographicEngine.DeriveKeyMaterial(keyOriginal, pbkdf2Parms, keySizeInBytes);
            return keyMaterial;
        }

        /// <summary>
        /// Computes the authentication code for the contents of a stream given the specified symmetric key.
        /// </summary>
        /// <param name="data">The data to compute the HMAC for.</param>
        /// <param name="key">The key to use in hashing.</param>
        /// <param name="hashAlgorithmName">The hash algorithm to use.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The authentication code.</returns>
        public override async Task<byte[]> ComputeAuthenticationCodeAsync(Stream data, byte[] key, string hashAlgorithmName, CancellationToken cancellationToken)
        {
            Requires.NotNull(data, "data");
            Requires.NotNull(key, "key");
            Requires.NotNullOrEmpty(hashAlgorithmName, "hashAlgorithmName");

            var algorithm = this.GetHmacAlgorithmProvider(hashAlgorithmName);
            var hasher = algorithm.CreateHash(key);

            var cryptoStream = new CryptoStream(Stream.Null, hasher, CryptoStreamMode.Write);
            await data.CopyToAsync(cryptoStream, 4096, cancellationToken);
            cryptoStream.FlushFinalBlock();

            return hasher.GetValueAndReset();
        }

        /// <summary>
        /// Symmetrically encrypts the specified buffer using a randomly generated key.
        /// </summary>
        /// <param name="data">The data to encrypt.</param>
        /// <param name="encryptionVariables">Optional encryption variables to use; or <c>null</c> to use randomly generated ones.</param>
        /// <returns>
        /// The result of the encryption.
        /// </returns>
        public override SymmetricEncryptionResult Encrypt(byte[] data, SymmetricEncryptionVariables encryptionVariables)
        {
            Requires.NotNull(data, "data");

            encryptionVariables = this.ThisOrNewEncryptionVariables(encryptionVariables);
            var symmetricKey = SymmetricAlgorithm.CreateSymmetricKey(encryptionVariables.Key);
            var cipherTextBuffer = WinRTCrypto.CryptographicEngine.Encrypt(symmetricKey, data, encryptionVariables.IV);
            return new SymmetricEncryptionResult(encryptionVariables, cipherTextBuffer);
        }

        /// <summary>
        /// Symmetrically encrypts a stream.
        /// </summary>
        /// <param name="plaintext">The stream of plaintext to encrypt.</param>
        /// <param name="ciphertext">The stream to receive the ciphertext.</param>
        /// <param name="encryptionVariables">An optional key and IV to use. May be <c>null</c> to use randomly generated values.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that completes when encryption has completed, whose result is the key and IV to use to decrypt the ciphertext.</returns>
        public override async Task<SymmetricEncryptionVariables> EncryptAsync(Stream plaintext, Stream ciphertext, SymmetricEncryptionVariables encryptionVariables, CancellationToken cancellationToken)
        {
            Requires.NotNull(plaintext, "plaintext");
            Requires.NotNull(ciphertext, "ciphertext");

            encryptionVariables = this.ThisOrNewEncryptionVariables(encryptionVariables);
            var key = SymmetricAlgorithm.CreateSymmetricKey(encryptionVariables.Key);
            using (var encryptor = WinRTCrypto.CryptographicEngine.CreateEncryptor(key, encryptionVariables.IV))
            {
                var cryptoStream = new CryptoStream(ciphertext, encryptor, CryptoStreamMode.Write);
                await plaintext.CopyToAsync(cryptoStream, 4096, cancellationToken);
                cryptoStream.FlushFinalBlock();
            }

            return encryptionVariables;
        }

        /// <summary>
        /// Symmetrically decrypts a stream.
        /// </summary>
        /// <param name="ciphertext">The stream of ciphertext to decrypt.</param>
        /// <param name="plaintext">The stream to receive the plaintext.</param>
        /// <param name="encryptionVariables">The key and IV to use.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public override async Task DecryptAsync(Stream ciphertext, Stream plaintext, SymmetricEncryptionVariables encryptionVariables, CancellationToken cancellationToken)
        {
            Requires.NotNull(ciphertext, "ciphertext");
            Requires.NotNull(plaintext, "plaintext");
            Requires.NotNull(encryptionVariables, "encryptionVariables");

            var key = SymmetricAlgorithm.CreateSymmetricKey(encryptionVariables.Key);
            using (var decryptor = WinRTCrypto.CryptographicEngine.CreateDecryptor(key, encryptionVariables.IV))
            {
                var cryptoStream = new CryptoStream(plaintext, decryptor, CryptoStreamMode.Write);
                await ciphertext.CopyToAsync(cryptoStream, 4096, cancellationToken);
                cryptoStream.FlushFinalBlock();
            }
        }

        /// <summary>
        /// Symmetrically decrypts a buffer using the specified key.
        /// </summary>
        /// <param name="data">The encrypted data and the key and IV used to encrypt it.</param>
        /// <returns>
        /// The decrypted buffer.
        /// </returns>
        public override byte[] Decrypt(SymmetricEncryptionResult data)
        {
            var symmetricKey = SymmetricAlgorithm.CreateSymmetricKey(data.Key);
            return WinRTCrypto.CryptographicEngine.Decrypt(symmetricKey, data.Ciphertext, data.IV);
        }

        /// <summary>
        /// Asymmetrically encrypts the specified buffer using the provided public key.
        /// </summary>
        /// <param name="encryptionPublicKey">The public key used to encrypt the buffer.</param>
        /// <param name="data">The buffer to encrypt.</param>
        /// <returns>
        /// The ciphertext.
        /// </returns>
        public override byte[] Encrypt(byte[] encryptionPublicKey, byte[] data)
        {
            var key = EncryptionProvider.ImportPublicKey(encryptionPublicKey, CryptographicPublicKeyBlobType.Capi1PublicKey);
            return WinRTCrypto.CryptographicEngine.Encrypt(key, data, null);
        }

        /// <summary>
        /// Asymmetrically decrypts the specified buffer using the provided private key.
        /// </summary>
        /// <param name="decryptionPrivateKey">The private key used to decrypt the buffer.</param>
        /// <param name="data">The buffer to decrypt.</param>
        /// <returns>
        /// The plaintext.
        /// </returns>
        public override byte[] Decrypt(byte[] decryptionPrivateKey, byte[] data)
        {
            var key = EncryptionProvider.ImportKeyPair(decryptionPrivateKey, CryptographicPrivateKeyBlobType.Capi1PrivateKey);
            return WinRTCrypto.CryptographicEngine.Decrypt(key, data, null);
        }

        /// <summary>
        /// Generates a key pair for asymmetric cryptography.
        /// </summary>
        /// <param name="keyPair">Receives the serialized key pair (includes private key).</param>
        /// <param name="publicKey">Receives the public key.</param>
        public override void GenerateSigningKeyPair(out byte[] keyPair, out byte[] publicKey)
        {
            var signer = WinRTCrypto.AsymmetricKeyAlgorithmProvider.OpenAlgorithm(this.SigningAlgorithm);
            var key = signer.CreateKeyPair(this.SignatureAsymmetricKeySize);
            keyPair = key.Export(CryptographicPrivateKeyBlobType.Capi1PrivateKey);
            publicKey = key.ExportPublicKey(CryptographicPublicKeyBlobType.Capi1PublicKey);
        }

        /// <summary>
        /// Generates a key pair for asymmetric cryptography.
        /// </summary>
        /// <param name="keyPair">Receives the serialized key pair (includes private key).</param>
        /// <param name="publicKey">Receives the public key.</param>
        public override void GenerateEncryptionKeyPair(out byte[] keyPair, out byte[] publicKey)
        {
            var key = EncryptionProvider.CreateKeyPair(this.EncryptionAsymmetricKeySize);
            keyPair = key.Export(CryptographicPrivateKeyBlobType.Capi1PrivateKey);
            publicKey = key.ExportPublicKey(CryptographicPublicKeyBlobType.Capi1PublicKey);
        }

        /// <summary>
        /// Gets the HMAC algorithm provider for the given hash algorithm.
        /// </summary>
        /// <param name="hashAlgorithm">The hash algorithm (SHA1, SHA256, etc.)</param>
        /// <returns>The algorithm provider.</returns>
        protected virtual IMacAlgorithmProvider GetHmacAlgorithmProvider(string hashAlgorithm)
        {
            switch (hashAlgorithm)
            {
                case "SHA1":
                    return WinRTCrypto.MacAlgorithmProvider.OpenAlgorithm(MacAlgorithm.HmacSha1);
                case "SHA256":
                    return WinRTCrypto.MacAlgorithmProvider.OpenAlgorithm(MacAlgorithm.HmacSha256);
                case "SHA384":
                    return WinRTCrypto.MacAlgorithmProvider.OpenAlgorithm(MacAlgorithm.HmacSha384);
                case "SHA512":
                    return WinRTCrypto.MacAlgorithmProvider.OpenAlgorithm(MacAlgorithm.HmacSha512);
                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Generates a new set of encryption variables.
        /// </summary>
        /// <returns>A set of encryption variables.</returns>
        private SymmetricEncryptionVariables NewSymmetricEncryptionVariables()
        {
            byte[] key = WinRTCrypto.CryptographicBuffer.GenerateRandom((uint)this.SymmetricEncryptionKeySize / 8);
            byte[] iv = WinRTCrypto.CryptographicBuffer.GenerateRandom((uint)SymmetricAlgorithm.BlockLength);
            return new SymmetricEncryptionVariables(key, iv);
        }

        /// <summary>
        /// Returns the specified encryption variables if they are non-null, or generates new ones.
        /// </summary>
        /// <param name="encryptionVariables">The encryption variables.</param>
        /// <returns>A valid set of encryption variables.</returns>
        private SymmetricEncryptionVariables ThisOrNewEncryptionVariables(SymmetricEncryptionVariables encryptionVariables)
        {
            if (encryptionVariables == null)
            {
                return this.NewSymmetricEncryptionVariables();
            }
            else
            {
                Requires.Argument(encryptionVariables.Key.Length == this.SymmetricEncryptionKeySize / 8, "key", "Incorrect length.");
                Requires.Argument(encryptionVariables.IV.Length == this.SymmetricEncryptionBlockSize / 8, "iv", "Incorrect length.");
                return encryptionVariables;
            }
        }
    }
}
