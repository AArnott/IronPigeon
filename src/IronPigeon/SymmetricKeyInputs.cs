// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.Runtime.Serialization;
    using PCLCrypto;

    /// <summary>
    /// Inputs for construction a symmetric key.
    /// </summary>
    [DataContract]
    public class SymmetricKeyInputs : CryptoKeyInputs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SymmetricKeyInputs"/> class.
        /// </summary>
        /// <inheritdoc cref="CryptoKeyInputs(string, ReadOnlyMemory{byte})"/>
        public SymmetricKeyInputs(string algorithmName, ReadOnlyMemory<byte> keyMaterial)
            : base(algorithmName, keyMaterial)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SymmetricKeyInputs"/> class.
        /// </summary>
        /// <param name="algorithm">The symmetric algorithm that the key is used with.</param>
        /// <param name="keyMaterial"><inheritdoc cref="CryptoKeyInputs(string, ReadOnlyMemory{byte})" path="/param[@name='keyMaterial']"/></param>
        public SymmetricKeyInputs(SymmetricAlgorithm algorithm, ReadOnlyMemory<byte> keyMaterial)
            : this(GetAlgorithmName(algorithm), keyMaterial)
        {
        }

        /// <summary>
        /// Gets the asymmetric algorithm identified by <see cref="CryptoKeyInputs.AlgorithmName"/>.
        /// </summary>
        public SymmetricAlgorithm Algorithm => ParseAlgorithmName(this.AlgorithmName);

        /// <inheritdoc/>
        public override ICryptographicKey CreateKey()
        {
            ISymmetricKeyAlgorithmProvider algorithm = WinRTCrypto.SymmetricKeyAlgorithmProvider.OpenAlgorithm(this.Algorithm);
            byte[] keyMaterial = this.KeyMaterial.AsOrCreateArray();
            return algorithm.CreateSymmetricKey(keyMaterial);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => base.Equals(obj as SymmetricKeyInputs);

        /// <inheritdoc/>
        public override int GetHashCode() => base.GetHashCode();

        /// <summary>
        /// Encodes a given asymmetric algorithm as a string for use in <see cref="CryptoKeyInputs.AlgorithmName"/>.
        /// </summary>
        /// <param name="algorithm">The algorithm to encode.</param>
        /// <returns>The string representation of the algorithm.</returns>
        private static string GetAlgorithmName(SymmetricAlgorithm algorithm) => algorithm.ToString();

        private static SymmetricAlgorithm ParseAlgorithmName(string name) => (SymmetricAlgorithm)Enum.Parse(typeof(SymmetricAlgorithm), name, ignoreCase: true);
    }
}
