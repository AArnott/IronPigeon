// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using PCLCrypto;

    /// <summary>
    /// Configuration for common crypto operations.
    /// </summary>
    public class CryptoSettings
    {
        /// <summary>
        /// The format public key are shared in.
        /// </summary>
        public static readonly CryptographicPublicKeyBlobType PublicKeyFormat = CryptographicPublicKeyBlobType.Capi1PublicKey;

        /// <summary>
        /// The signing algorithm to use.
        /// </summary>
        public static readonly IAsymmetricKeyAlgorithmProvider SigningAlgorithm = WinRTCrypto.AsymmetricKeyAlgorithmProvider.OpenAlgorithm(AsymmetricAlgorithm.RsaSignPkcs1Sha256);

        /// <summary>
        /// The encryption algorithm to use.
        /// </summary>
        public static readonly IAsymmetricKeyAlgorithmProvider EncryptionAlgorithm = WinRTCrypto.AsymmetricKeyAlgorithmProvider.OpenAlgorithm(AsymmetricAlgorithm.RsaOaepSha1);

        /// <summary>
        /// Gets The symmetric encryption algorithm provider to use.
        /// </summary>
        public static readonly ISymmetricKeyAlgorithmProvider SymmetricAlgorithm = WinRTCrypto.SymmetricKeyAlgorithmProvider.OpenAlgorithm(PCLCrypto.SymmetricAlgorithm.AesCbcPkcs7);

        /// <summary>
        /// Initializes a new instance of the <see cref="CryptoSettings"/> class.
        /// </summary>
        /// <param name="securityLevel">The security level.</param>
        public CryptoSettings(SecurityLevel securityLevel = SecurityLevel.Maximum)
        {
            this.ApplySecurityLevel(securityLevel);
        }

        /// <summary>
        /// Gets or sets the name of the hash algorithm to use for symmetric signatures.
        /// </summary>
        public HashAlgorithm SymmetricHashAlgorithm { get; set; }

        /// <summary>
        /// Gets or sets the size of the key (in bits) used for asymmetric signatures.
        /// </summary>
        public int AsymmetricKeySize { get; set; }

        /// <summary>
        /// Gets or sets the size of the key (in bits) used for symmetric blob encryption.
        /// </summary>
        public int SymmetricKeySize { get; set; }

        /// <summary>
        /// Applies a security level to this object.
        /// </summary>
        /// <param name="securityLevel">The security level.</param>
        public void ApplySecurityLevel(SecurityLevel securityLevel)
        {
            switch (securityLevel)
            {
                case SecurityLevel.Minimum:
                    this.SymmetricKeySize = 128;
                    this.AsymmetricKeySize = 512;
                    this.SymmetricHashAlgorithm = HashAlgorithm.Sha1;
                    break;
                case SecurityLevel.Maximum:
                    this.SymmetricKeySize = 256;
                    this.AsymmetricKeySize = 4096;
                    this.SymmetricHashAlgorithm = HashAlgorithm.Sha256;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(securityLevel));
            }
        }
    }
}
