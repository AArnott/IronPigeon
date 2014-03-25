//-----------------------------------------------------------------------
// <copyright file="PclCryptoProvider.cs" company="Andrew Arnott">
//     Copyright (c) Andrew Arnott. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Composition;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using PCLCrypto;
	using Validation;

	/// <summary>
	/// An <see cref="ICryptoProvider"/> instance based on PclCrypto.
	/// </summary>
	[Export(typeof(ICryptoProvider))]
	[Shared]
	public class PclCryptoProvider : CryptoProviderBase {
		/// <summary>
		/// The symmetric encryption algorithm provider to use.
		/// </summary>
		private static readonly ISymmetricKeyAlgorithmProvider SymmetricAlgorithm = WinRTCrypto.SymmetricKeyAlgorithmProvider.OpenAlgorithm(PCLCrypto.SymmetricAlgorithm.AesCbcPkcs7);

		/// <summary>
		/// Initializes a new instance of the <see cref="PclCryptoProvider"/> class.
		/// </summary>
		public PclCryptoProvider()
			: this(SecurityLevel.Maximum) {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="PclCryptoProvider"/> class.
		/// </summary>
		/// <param name="securityLevel">The security level to apply to this instance.  The default is <see cref="SecurityLevel.Maximum"/>.</param>
		public PclCryptoProvider(SecurityLevel securityLevel) {
			Requires.NotNull(securityLevel, "securityLevel");
			securityLevel.Apply(this);
		}

		/// <summary>
		/// Gets the length (in bits) of the symmetric encryption cipher block.
		/// </summary>
		public override int SymmetricEncryptionBlockSize {
			get { return (int)SymmetricAlgorithm.BlockLength * 8; }
		}
	}
}
