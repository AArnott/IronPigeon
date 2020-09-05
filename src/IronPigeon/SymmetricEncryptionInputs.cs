// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.Runtime.Serialization;
    using PCLCrypto;

    /// <summary>
    /// Instructions for performing symmetric encryption/decryption.
    /// </summary>
    [DataContract]
    public class SymmetricEncryptionInputs : SymmetricKeyInputs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SymmetricEncryptionInputs"/> class.
        /// </summary>
        /// <param name="algorithmName"><inheritdoc cref="SymmetricKeyInputs(SymmetricAlgorithm, ReadOnlyMemory{byte})" path="/param[@name='algorithmName']"/></param>
        /// <param name="keyMaterial"><inheritdoc cref="SymmetricKeyInputs(SymmetricAlgorithm, ReadOnlyMemory{byte})" path="/param[@name='keyMaterial']"/></param>
        /// <param name="iv">The initialization vector to use, when applicable given the <see cref="SymmetricKeyInputs.Algorithm"/>.</param>
        public SymmetricEncryptionInputs(string algorithmName, ReadOnlyMemory<byte> keyMaterial, ReadOnlyMemory<byte> iv)
            : base(algorithmName, keyMaterial)
        {
            this.IV = iv;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SymmetricEncryptionInputs"/> class.
        /// </summary>
        /// <param name="algorithm"><inheritdoc cref="SymmetricKeyInputs(SymmetricAlgorithm, ReadOnlyMemory{byte})" path="/param[@name='algorithm']"/></param>
        /// <param name="keyMaterial"><inheritdoc cref="SymmetricKeyInputs(SymmetricAlgorithm, ReadOnlyMemory{byte})" path="/param[@name='keyMaterial']"/></param>
        /// <param name="iv">The initialization vector to use, when applicable given the <see cref="SymmetricKeyInputs.Algorithm"/>.</param>
        public SymmetricEncryptionInputs(SymmetricAlgorithm algorithm, ReadOnlyMemory<byte> keyMaterial, ReadOnlyMemory<byte> iv)
            : base(algorithm, keyMaterial)
        {
            this.IV = iv;
        }

        /// <summary>
        /// Gets the initialization vector to use, when applicable given the <see cref="SymmetricKeyInputs.Algorithm"/>.
        /// </summary>
        [DataMember]
        public ReadOnlyMemory<byte> IV { get; }

        /// <summary>
        /// Copies this instance with a new key.
        /// </summary>
        /// <param name="keyMaterial">The key material.</param>
        /// <returns>A new instance.</returns>
        public SymmetricEncryptionInputs WithKeyMaterial(ReadOnlyMemory<byte> keyMaterial) => new SymmetricEncryptionInputs(this.AlgorithmName, keyMaterial, this.IV);
    }
}
