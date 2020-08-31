// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using Microsoft;
    using PCLCrypto;

    /// <summary>
    /// Configuration for common crypto operations.
    /// </summary>
    public class CryptoSettings
    {
        /// <summary>
        /// Minimum security level. Useful for unit testing.
        /// </summary>
        public static readonly CryptoSettings Testing = new CryptoSettings(symmetricKeySize: 128, asymmetricKeySize: 512);

        /// <summary>
        /// It can't get much higher than this while retaining sanity.
        /// </summary>
        public static readonly CryptoSettings Recommended = new CryptoSettings(symmetricKeySize: 256, asymmetricKeySize: 4096);

        /// <summary>
        /// Initializes a new instance of the <see cref="CryptoSettings"/> class.
        /// </summary>
        /// <param name="copyFrom">An instance to copy values from.</param>
        public CryptoSettings(CryptoSettings copyFrom)
        {
            Requires.NotNull(copyFrom, nameof(copyFrom));

            this.AsymmetricKeySize = copyFrom.AsymmetricKeySize;
            this.SymmetricKeySize = copyFrom.SymmetricKeySize;
            this.HashAlgorithm = copyFrom.HashAlgorithm;
            this.SigningAlgorithm = copyFrom.SigningAlgorithm;
            this.AsymmetricEncryptionAlgorithm = copyFrom.AsymmetricEncryptionAlgorithm;
            this.SymmetricEncryptionAlgorithm = copyFrom.SymmetricEncryptionAlgorithm;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CryptoSettings"/> class.
        /// </summary>
        /// <param name="symmetricKeySize">The size of the key (in bits) used for symmetric blob encryption.</param>
        /// <param name="asymmetricKeySize">The size of the key (in bits) used for asymmetric signatures.</param>
        private CryptoSettings(int symmetricKeySize, int asymmetricKeySize)
        {
            this.SymmetricKeySize = symmetricKeySize;
            this.AsymmetricKeySize = asymmetricKeySize;
        }

        /// <summary>
        /// Gets the size of the key (in bits) used for asymmetric signatures.
        /// </summary>
        public int AsymmetricKeySize { get; private set; }

        /// <summary>
        /// Gets the size of the key (in bits) used for symmetric blob encryption.
        /// </summary>
        public int SymmetricKeySize { get; private set; }

        /// <summary>
        /// Gets the name of the hash algorithm to use for symmetric signatures.
        /// </summary>
        public HashAlgorithm HashAlgorithm { get; private set; } = HashAlgorithm.Sha256;

        /// <summary>
        /// Gets the signing algorithm to use.
        /// </summary>
        public AsymmetricAlgorithm SigningAlgorithm { get; private set; } = AsymmetricAlgorithm.RsaSignPkcs1Sha256;

        /// <summary>
        /// Gets the encryption algorithm to use.
        /// </summary>
        public AsymmetricAlgorithm AsymmetricEncryptionAlgorithm { get; private set; } = AsymmetricAlgorithm.RsaOaepSha1;

        /// <summary>
        /// Gets the symmetric encryption algorithm provider to use.
        /// </summary>
        public SymmetricAlgorithm SymmetricEncryptionAlgorithm { get; private set; } = SymmetricAlgorithm.AesCbcPkcs7;

        /// <summary>
        /// Returns a copy of this object with <see cref="AsymmetricKeySize"/> set to the indicated value.
        /// </summary>
        /// <param name="value">The new value for the property.</param>
        /// <returns>The new instance.</returns>
        public CryptoSettings WithAsymmetricKeySize(int value) => new CryptoSettings(this) { AsymmetricKeySize = value };
    }
}
