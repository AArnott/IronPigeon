namespace IronPigeon {
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using PCLCrypto;


	/// <summary>
	/// Implements the cryptographic algorithms that protect users and data required by the IronPigeon protocol.
	/// </summary>
	public interface ICryptoProvider {
		/// <summary>
		/// Gets or sets the name of the hash algorithm to use for symmetric signatures.
		/// </summary>
		string SymmetricHashAlgorithmName { get; set; }

		/// <summary>
		/// Gets or sets the name of the algorithm to use for asymmetric signatures.
		/// </summary>
		string AsymmetricHashAlgorithmName { get; set; }

		AsymmetricAlgorithm SigningAlgorithm { get;set; }

		/// <summary>
		/// Gets or sets the configuration to use for symmetric encryption.
		/// </summary>
		EncryptionConfiguration SymmetricEncryptionConfiguration { get; set; }

		/// <summary>
		/// Gets or sets the size of the key (in bits) used for symmetric blob encryption.
		/// </summary>
		int SymmetricEncryptionKeySize { get; set; }

		/// <summary>
		/// Gets the length (in bits) of the symmetric encryption cipher block.
		/// </summary>
		int SymmetricEncryptionBlockSize { get; }

		/// <summary>
		/// Gets or sets the size of the key (in bits) used for asymmetric signatures.
		/// </summary>
		int SignatureAsymmetricKeySize { get; set; }

		/// <summary>
		/// Gets or sets the size of the key (in bits) used for asymmetric encryption.
		/// </summary>
		int EncryptionAsymmetricKeySize { get; set; }

		AsymmetricAlgorithm EncryptionAlgorithm { get; set; }
	}
}
