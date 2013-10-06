namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using Validation;

	/// <summary>
	/// The result of symmetric encryption using a random key, IV.
	/// </summary>
	public class SymmetricEncryptionResult : SymmetricEncryptionVariables {
		/// <summary>
		/// Initializes a new instance of the <see cref="SymmetricEncryptionResult"/> class.
		/// </summary>
		/// <param name="key">The randomly generated symmetric key used to encrypt the data.</param>
		/// <param name="iv">The initialization vector used to encrypt the data.</param>
		/// <param name="ciphertext">The encrypted data.</param>
		public SymmetricEncryptionResult(byte[] key, byte[] iv, byte[] ciphertext)
			: base(key, iv) {
			Requires.NotNullOrEmpty(ciphertext, "ciphertext");

			this.Ciphertext = ciphertext;
		}

		/// <summary>
		/// Gets the encrypted data.
		/// </summary>
		public byte[] Ciphertext { get; private set; }
	}
}
