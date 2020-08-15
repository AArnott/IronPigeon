// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using Microsoft;

    /// <summary>
    /// The result of symmetric encryption using a random key, IV.
    /// </summary>
    public class SymmetricEncryptionResult : SymmetricEncryptionVariables
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SymmetricEncryptionResult"/> class.
        /// </summary>
        /// <param name="key">The randomly generated symmetric key used to encrypt the data.</param>
        /// <param name="iv">The initialization vector used to encrypt the data.</param>
        /// <param name="ciphertext">The encrypted data.</param>
        public SymmetricEncryptionResult(byte[] key, byte[] iv, byte[] ciphertext)
            : base(key, iv)
        {
            Requires.NotNullOrEmpty(ciphertext, nameof(ciphertext));

            this.Ciphertext = ciphertext;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SymmetricEncryptionResult"/> class.
        /// </summary>
        /// <param name="encryptionVariables">The key and IV used to encrypt the ciphertext.</param>
        /// <param name="ciphertext">The encrypted data.</param>
        public SymmetricEncryptionResult(SymmetricEncryptionVariables encryptionVariables, byte[] ciphertext)
            : this(Requires.NotNull(encryptionVariables, nameof(encryptionVariables)).Key, encryptionVariables.IV, ciphertext)
        {
        }

        /// <summary>
        /// Gets the encrypted data.
        /// </summary>
        public byte[] Ciphertext { get; private set; }
    }
}
