namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

	/// <summary>
	/// The result of symmetric encryption using a random key, IV.
	/// </summary>
	public class SymmetricEncryptionResult {
		/// <summary>
		/// Initializes a new instance of the <see cref="SymmetricEncryptionResult"/> class.
		/// </summary>
		/// <param name="key">The randomly generated symmetric key used to encrypt the data.</param>
		/// <param name="iv">The initialization vector used to encrypt the data.</param>
		/// <param name="ciphertext">The encrypted data.</param>
		public SymmetricEncryptionResult(byte[] key, byte[] iv, byte[] ciphertext) {
			Requires.NotNullOrEmpty(key, "key");
			Requires.NotNullOrEmpty(iv, "iv");
			Requires.NotNullOrEmpty(ciphertext, "ciphertext");

			this.Key = key;
			this.IV = iv;
			this.Ciphertext = ciphertext;
		}

		/// <summary>
		/// Gets the symmetric key used to encrypt the data.
		/// </summary>
		public byte[] Key { get; private set; }

		/// <summary>
		/// Gets the initialization vector used to encrypt the data.
		/// </summary>
		public byte[] IV { get; private set; }

		/// <summary>
		/// Gets the encrypted data.
		/// </summary>
		public byte[] Ciphertext { get; private set; }
	}
}
