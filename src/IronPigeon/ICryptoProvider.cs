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

		/// <summary>
		/// Gets or sets the key size (in bits) used for ECDiffieHellman for negotiating shared secrets.
		/// </summary>
		int ECDiffieHellmanKeySize { get; set; }

		/// <summary>
		/// Gets or sets the size of the Elliptic-curve DSA key.
		/// </summary>
		/// <value>
		/// The size of the EC DSA key.
		/// </value>
		int ECDsaKeySize { get; set; }

		AsymmetricAlgorithm EncryptionAlgorithm { get; set; }

		/// <summary>
		/// Derives a cryptographically strong key from the specified password.
		/// </summary>
		/// <param name="password">The user-supplied password.</param>
		/// <param name="salt">The salt.</param>
		/// <param name="iterations">The rounds of computation to use in deriving a stronger key. The larger this is, the longer attacks will take.</param>
		/// <param name="keySizeInBytes">The desired key size in bytes.</param>
		/// <returns>The generated key.</returns>
		byte[] DeriveKeyFromPassword(string password, byte[] salt, int iterations, int keySizeInBytes);

		/// <summary>
		/// Computes the authentication code for the contents of a stream given the specified symmetric key.
		/// </summary>
		/// <param name="data">The data to compute the HMAC for.</param>
		/// <param name="key">The key to use in hashing.</param>
		/// <param name="hashAlgorithmName">The hash algorithm to use.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The authentication code.</returns>
		Task<byte[]> ComputeAuthenticationCodeAsync(Stream data, byte[] key, string hashAlgorithmName, CancellationToken cancellationToken = default(CancellationToken));

		/// <summary>
		/// Symmetrically encrypts the specified buffer using a randomly generated key.
		/// </summary>
		/// <param name="data">The data to encrypt.</param>
		/// <param name="encryptionVariables">Optional encryption variables to use; or <c>null</c> to use randomly generated ones.</param>
		/// <returns>The result of the encryption.</returns>
		SymmetricEncryptionResult Encrypt(byte[] data, SymmetricEncryptionVariables encryptionVariables = null);

		/// <summary>
		/// Symmetrically encrypts a stream.
		/// </summary>
		/// <param name="plaintext">The stream of plaintext to encrypt.</param>
		/// <param name="ciphertext">The stream to receive the ciphertext.</param>
		/// <param name="encryptionVariables">An optional key and IV to use. May be <c>null</c> to use randomly generated values.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>A task that completes when encryption has completed, whose result is the key and IV to use to decrypt the ciphertext.</returns>
		Task<SymmetricEncryptionVariables> EncryptAsync(Stream plaintext, Stream ciphertext, SymmetricEncryptionVariables encryptionVariables = null, CancellationToken cancellationToken = default(CancellationToken));

		/// <summary>
		/// Symmetrically decrypts a stream.
		/// </summary>
		/// <param name="ciphertext">The stream of ciphertext to decrypt.</param>
		/// <param name="plaintext">The stream to receive the plaintext.</param>
		/// <param name="encryptionVariables">The key and IV to use.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>A task that represents the asynchronous operation.</returns>
		Task DecryptAsync(Stream ciphertext, Stream plaintext, SymmetricEncryptionVariables encryptionVariables, CancellationToken cancellationToken = default(CancellationToken));

		/// <summary>
		/// Symmetrically decrypts a buffer using the specified key.
		/// </summary>
		/// <param name="data">The encrypted data and the key and IV used to encrypt it.</param>
		/// <returns>The decrypted buffer.</returns>
		byte[] Decrypt(SymmetricEncryptionResult data);
	}
}
