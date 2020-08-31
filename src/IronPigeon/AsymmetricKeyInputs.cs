// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.Diagnostics;
    using System.Runtime.Serialization;
    using Microsoft;
    using PCLCrypto;

    /// <summary>
    /// Inputs for construction an asymmetric key.
    /// </summary>
    [DataContract]
    public class AsymmetricKeyInputs : CryptoKeyInputs
    {
        /// <summary>
        /// Backing field for the <see cref="PublicKey"/> property.
        /// </summary>
        private AsymmetricKeyInputs? publicKeyInputs;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsymmetricKeyInputs"/> class.
        /// </summary>
        /// <param name="algorithmName"><inheritdoc cref="CryptoKeyInputs(string, ReadOnlyMemory{byte})" path="/param[@name='algorithmName']"/></param>
        /// <param name="keyMaterial"><inheritdoc cref="CryptoKeyInputs(string, ReadOnlyMemory{byte})" path="/param[@name='keyMaterial']"/></param>
        /// <param name="keyEncodingRfc">The RFC number that describes the encoding of the <paramref name="keyMaterial"/>.</param>
        /// <param name="hasPrivateKey">A value indicating whether the <see cref="CryptoKeyInputs.KeyMaterial"/> includes the private key.</param>
        public AsymmetricKeyInputs(string algorithmName, ReadOnlyMemory<byte> keyMaterial, int keyEncodingRfc, bool hasPrivateKey)
            : base(algorithmName, keyMaterial)
        {
            this.KeyEncodingRfc = keyEncodingRfc;
            this.HasPrivateKey = hasPrivateKey;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsymmetricKeyInputs"/> class.
        /// </summary>
        /// <param name="algorithm">The asymmetric algorithm that the key is used with.</param>
        /// <param name="cryptographicKey">The asymmetric key.</param>
        /// <param name="includePrivateKey"><c>true</c> to export the private key data; <c>false</c> to export only the public key data.</param>
        public AsymmetricKeyInputs(AsymmetricAlgorithm algorithm, ICryptographicKey cryptographicKey, bool includePrivateKey)
            : this(
                  GetAlgorithmName(algorithm),
                  includePrivateKey ? Requires.NotNull(cryptographicKey, nameof(cryptographicKey)).Export(CryptographicPrivateKeyBlobType.Pkcs8RawPrivateKeyInfo) : Requires.NotNull(cryptographicKey, nameof(cryptographicKey)).ExportPublicKey(CryptographicPublicKeyBlobType.X509SubjectPublicKeyInfo),
                  includePrivateKey ? /* Pkcs8 */ 5208 : /* X509 */ 5280,
                  hasPrivateKey: includePrivateKey)
        {
        }

        /// <summary>
        /// Gets the asymmetric algorithm identified by <see cref="CryptoKeyInputs.AlgorithmName"/>.
        /// </summary>
        public AsymmetricAlgorithm Algorithm => ParseAlgorithmName(this.AlgorithmName);

        /// <summary>
        /// Gets the RFC number that describes the encoding of the <see cref="CryptoKeyInputs.KeyMaterial" />.
        /// </summary>
        [DataMember]
        public int KeyEncodingRfc { get; }

        /// <summary>
        /// Gets a value indicating whether the <see cref="CryptoKeyInputs.KeyMaterial"/> includes the private key.
        /// </summary>
        [DataMember]
        public bool HasPrivateKey { get; }

        /// <summary>
        /// Gets this instance or a copy, such that any private key data is removed.
        /// </summary>
        /// <returns>A public key instance of these inputs.</returns>
        [IgnoreDataMember]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public AsymmetricKeyInputs PublicKey
        {
            get
            {
                if (this.publicKeyInputs is null)
                {
                    // Even if we think this instance has no private key, the potential damage from exposing it if our caller accidentally included the private key is too high.
                    // Export the public key data to make sure the private key is not present.
                    using ICryptographicKey key = this.CreateKey();
                    this.publicKeyInputs = new AsymmetricKeyInputs(this.Algorithm, key, includePrivateKey: false);

                    // Let this new instance know for sure that it is public-key only so we don't endless keep creating public keys.
                    this.publicKeyInputs.publicKeyInputs = this.publicKeyInputs;
                }

                return this.publicKeyInputs;
            }
        }

        /// <inheritdoc/>
        public override ICryptographicKey CreateKey()
        {
            IAsymmetricKeyAlgorithmProvider algorithm = WinRTCrypto.AsymmetricKeyAlgorithmProvider.OpenAlgorithm(this.Algorithm);

            byte[] keyMaterial = this.KeyMaterial.AsOrCreateArray();
            if (this.HasPrivateKey)
            {
                CryptographicPrivateKeyBlobType blobType = this.KeyEncodingRfc switch
                {
                    3447 => CryptographicPrivateKeyBlobType.Pkcs1RsaPrivateKey,
                    5208 => CryptographicPrivateKeyBlobType.Pkcs8RawPrivateKeyInfo,
                    _ => throw new NotSupportedException($"RFC {this.KeyEncodingRfc} not a supported private key encoding."),
                };
                return algorithm.ImportKeyPair(keyMaterial, blobType);
            }
            else
            {
                CryptographicPublicKeyBlobType blobType = this.KeyEncodingRfc switch
                {
                    3447 => CryptographicPublicKeyBlobType.Pkcs1RsaPublicKey,
                    3280 => CryptographicPublicKeyBlobType.X509SubjectPublicKeyInfo,
                    5280 => CryptographicPublicKeyBlobType.X509SubjectPublicKeyInfo,
                    _ => throw new NotSupportedException($"RFC {this.KeyEncodingRfc} not a supported public key encoding."),
                };
                return algorithm.ImportPublicKey(keyMaterial, blobType);
            }
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is AsymmetricKeyInputs other && base.Equals(other) && this.KeyEncodingRfc == other.KeyEncodingRfc && this.HasPrivateKey == other.HasPrivateKey;

        /// <inheritdoc/>
        public override int GetHashCode() => base.GetHashCode();

        /// <summary>
        /// Encodes a given asymmetric algorithm as a string for use in <see cref="CryptoKeyInputs.AlgorithmName"/>.
        /// </summary>
        /// <param name="algorithm">The algorithm to encode.</param>
        /// <returns>The string representation of the algorithm.</returns>
        private static string GetAlgorithmName(AsymmetricAlgorithm algorithm) => algorithm.ToString();

        private static AsymmetricAlgorithm ParseAlgorithmName(string name) => (AsymmetricAlgorithm)Enum.Parse(typeof(AsymmetricAlgorithm), name);
    }
}
