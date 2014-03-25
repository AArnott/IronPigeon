namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using PCLCrypto;
	using Validation;

	/// <summary>
	/// A common base class for implementations of the <see cref="ICryptoProvider" /> interface.
	/// </summary>
	public abstract class CryptoProviderBase : ICryptoProvider {
		/// <summary>
		/// Backing field for the <see cref="SymmetricHashAlgorithmName"/> property.
		/// </summary>
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private string symmetricHashAlgorithmName = SecurityLevel.Maximum.SymmetricHashAlgorithmName;

		/// <summary>
		/// Backing field for the <see cref="AsymmetricHashAlgorithmName"/> property.
		/// </summary>
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private string asymmetricHashAlgorithmName = SecurityLevel.Maximum.AsymmetricHashAlgorithmName;

		/// <summary>
		/// Backing field for the <see cref="SymmetricEncryptionConfiguration"/> property.
		/// </summary>
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private EncryptionConfiguration symmetricEncryptionConfiguration = SecurityLevel.Maximum.SymmetricEncryptionConfiguration;

		/// <summary>
		/// Backing field for the <see cref="EncryptionAsymmetricKeySize"/> property.
		/// </summary>
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private int encryptionAsymmetricKeySize = SecurityLevel.Maximum.EncryptionAsymmetricKeySize;

		/// <summary>
		/// Backing field for the <see cref="SignatureAsymmetricKeySize"/> property.
		/// </summary>
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private int signatureAsymmetricKeySize = SecurityLevel.Maximum.SignatureAsymmetricKeySize;

		/// <summary>
		/// Backing field for the <see cref="SymmetricEncryptionKeySize"/> property.
		/// </summary>
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private int blobSymmetricKeySize = SecurityLevel.Maximum.BlobSymmetricKeySize;

		protected CryptoProviderBase() {
			this.SigningAlgorithm = AsymmetricAlgorithm.RsaSignPkcs1Sha256;
			this.EncryptionAlgorithm = AsymmetricAlgorithm.RsaOaepSha1;
        }

		/// <summary>
		/// Gets or sets the name of the hash algorithm to use for symmetric signatures.
		/// </summary>
		public string SymmetricHashAlgorithmName {
			get { return this.symmetricHashAlgorithmName; }
			set { this.symmetricHashAlgorithmName = value; }
		}

		/// <summary>
		/// Gets or sets the name of the algorithm to use for asymmetric signatures.
		/// </summary>
		public string AsymmetricHashAlgorithmName {
			get { return this.asymmetricHashAlgorithmName; }
			set { this.asymmetricHashAlgorithmName = value; }
		}

		public AsymmetricAlgorithm SigningAlgorithm { get;set; }

		/// <summary>
		/// Gets or sets the configuration to use for symmetric encryption.
		/// </summary>
		public EncryptionConfiguration SymmetricEncryptionConfiguration {
			get { return this.symmetricEncryptionConfiguration; }
			set { this.symmetricEncryptionConfiguration = value; }
		}

		/// <summary>
		/// Gets or sets the size of the key (in bits) used for asymmetric signatures.
		/// </summary>
		public int SignatureAsymmetricKeySize {
			get { return this.signatureAsymmetricKeySize; }
			set { this.signatureAsymmetricKeySize = value; }
		}

		/// <summary>
		/// Gets or sets the size of the key (in bits) used for asymmetric encryption.
		/// </summary>
		public int EncryptionAsymmetricKeySize {
			get { return this.encryptionAsymmetricKeySize; }
			set { this.encryptionAsymmetricKeySize = value; }
		}

		/// <summary>
		/// Gets or sets the size of the key (in bits) used for symmetric blob encryption.
		/// </summary>
		public int SymmetricEncryptionKeySize {
			get { return this.blobSymmetricKeySize; }
			set { this.blobSymmetricKeySize = value; }
		}

		public AsymmetricAlgorithm EncryptionAlgorithm { get; set; }

		/// <summary>
		/// Gets the length (in bits) of the symmetric encryption cipher block.
		/// </summary>
		public abstract int SymmetricEncryptionBlockSize { get; }
	}
}
