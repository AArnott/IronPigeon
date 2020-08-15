// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft;

    /// <summary>
    /// Captures the key and IV used for a symmetric encryption.
    /// </summary>
    public class SymmetricEncryptionVariables
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SymmetricEncryptionVariables"/> class.
        /// </summary>
        /// <param name="key">The randomly generated symmetric key used to encrypt the data.</param>
        /// <param name="iv">The initialization vector used to encrypt the data.</param>
        public SymmetricEncryptionVariables(byte[] key, byte[] iv)
        {
            Requires.NotNullOrEmpty(key, nameof(key));
            Requires.NotNullOrEmpty(iv, nameof(iv));

            this.Key = key;
            this.IV = iv;
        }

        /// <summary>
        /// Gets the symmetric key used to encrypt the data.
        /// </summary>
        public byte[] Key { get; private set; }

        /// <summary>
        /// Gets the initialization vector used to encrypt the data.
        /// </summary>
        public byte[] IV { get; private set; }
    }
}
