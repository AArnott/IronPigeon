namespace IronPigeon {
	using Validation;

	/// <summary>
	/// Describes the configuration for a crypto engine.
	/// </summary>
	public struct EncryptionConfiguration {
		/// <summary>
		/// Initializes a new instance of the <see cref="EncryptionConfiguration"/> struct.
		/// </summary>
		/// <param name="algorithmName">Name of the encryption algorithm (e.g. Rijndael).</param>
		/// <param name="blockMode">The block mode (e.g. CBC, ECB).</param>
		/// <param name="padding">The padding to use (e.g. PKCS7).</param>
		public EncryptionConfiguration(string algorithmName, string blockMode, string padding)
			: this() {
			Requires.NotNullOrEmpty(algorithmName, "algorithmName");
			Requires.NotNullOrEmpty(blockMode, "blockMode");

			this.AlgorithmName = algorithmName;
			this.BlockMode = blockMode;
			this.Padding = padding;
		}

		/// <summary>
		/// Gets the name of the encryption algorithm (e.g. Rijndael).
		/// </summary>
		public string AlgorithmName { get; private set; }

		/// <summary>
		/// Gets the block mode (e.g. CBC, ECB).
		/// </summary>
		public string BlockMode { get; private set; }

		/// <summary>
		/// Gets the padding method (e.g. PKCS7).
		/// </summary>
		public string Padding { get; private set; }
	}
}
