// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.Runtime.Serialization;
    using Microsoft;
    using PCLCrypto;

    /// <summary>
    /// Contains the instructions and data required to perform cryptographic operations.
    /// </summary>
    [DataContract]
    public abstract class CryptoKeyInputs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CryptoKeyInputs"/> class.
        /// </summary>
        /// <param name="algorithmName">The cryptographic algorithm to initialize the key with. This value is treated with case insensitivity.</param>
        /// <param name="keyMaterial">The key material.</param>
        public CryptoKeyInputs(string algorithmName, ReadOnlyMemory<byte> keyMaterial)
        {
            Requires.NotNullOrEmpty(algorithmName, nameof(algorithmName));

            this.KeyMaterial = keyMaterial;
            this.AlgorithmName = algorithmName;
        }

        /// <summary>
        /// Gets the name of the algorithm to use.
        /// </summary>
        /// <remarks>
        /// This value should be treated with case insensitivity.
        /// </remarks>
        [DataMember]
        public string AlgorithmName { get; }

        /// <summary>
        /// Gets the key material to use.
        /// </summary>
        /// <remarks>
        /// When this is a symmetric key, the key material may be encrypted using an asymmetric key.
        /// </remarks>
        [DataMember]
        public ReadOnlyMemory<byte> KeyMaterial { get; }

        /// <summary>
        /// Creates a cryptographic key given the inputs on this instance.
        /// </summary>
        /// <returns>A cryptographic key.</returns>
        public abstract ICryptographicKey CreateKey();

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is CryptoKeyInputs other
                && string.Equals(this.AlgorithmName, other.AlgorithmName, StringComparison.OrdinalIgnoreCase)
                && Utilities.AreEquivalent(this.KeyMaterial.Span, other.KeyMaterial.Span);
        }

        /// <inheritdoc/>
        public override int GetHashCode() => this.AlgorithmName.GetHashCode() + Utilities.GetHashCode(this.KeyMaterial.Span);
    }
}
