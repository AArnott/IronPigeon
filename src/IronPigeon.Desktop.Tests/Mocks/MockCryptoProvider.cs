namespace IronPigeon.Tests.Mocks {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using NUnit.Framework;
	using PCLCrypto;

	internal class MockCryptoProvider : ICryptoProvider {
		internal const int KeyLengthInBytes = 5;

		#region ICryptoProvider Members

		public string SymmetricHashAlgorithmName {
			get { return "mock"; }
			set { throw new NotSupportedException(); }
		}

		public string AsymmetricHashAlgorithmName {
			get { return "mock"; }
			set { throw new NotSupportedException(); }
		}

		EncryptionConfiguration ICryptoProvider.SymmetricEncryptionConfiguration {
			get { return new EncryptionConfiguration("mock", "mock", "mock"); }
			set { throw new NotSupportedException(); }
		}

		public int SignatureAsymmetricKeySize {
			get { return KeyLengthInBytes; }
			set { throw new NotSupportedException(); }
		}

		public int EncryptionAsymmetricKeySize {
			get { return KeyLengthInBytes; }
			set { throw new NotSupportedException(); }
		}

		public int SymmetricEncryptionKeySize {
			get { return KeyLengthInBytes; }
			set { throw new NotSupportedException(); }
		}

		/// <summary>
		/// Gets the length of the symmetric encryption cipher block.
		/// </summary>
		public int SymmetricEncryptionBlockSize {
			get { return 5; }
		}

		public PCLCrypto.AsymmetricAlgorithm SigningAlgorithm { get; set; }

		public PCLCrypto.AsymmetricAlgorithm EncryptionAlgorithm { get; set; }

		#endregion
	}
}
