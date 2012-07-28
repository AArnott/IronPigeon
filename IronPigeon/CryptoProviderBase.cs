namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	public abstract class CryptoProviderBase : ICryptoProvider {
		private string hashAlgorithmName = Recommended.HashAlgorithmName;
		private int encryptionAsymmetricKeySize = Recommended.EncryptionAsymmetricKeySize;
		private int signatureAsymmetricKeySize = Recommended.SignatureAsymmetricKeySize;
		private int blobSymmetricKeySize = Recommended.BlobSymmetricKeySize;

		public string HashAlgorithmName {
			get { return this.hashAlgorithmName; }
			set { this.hashAlgorithmName = value; }
		}

		public int SignatureAsymmetricKeySize {
			get { return this.signatureAsymmetricKeySize; }
			set { this.signatureAsymmetricKeySize = value; }
		}

		public int EncryptionAsymmetricKeySize {
			get { return this.encryptionAsymmetricKeySize; }
			set { this.encryptionAsymmetricKeySize = value; }
		}

		public int BlobSymmetricKeySize {
			get { return this.blobSymmetricKeySize; }
			set { this.blobSymmetricKeySize = value; }
		}

		public abstract byte[] Sign(byte[] data, byte[] signingPrivateKey);
		public abstract bool VerifySignature(byte[] signingPublicKey, byte[] data, byte[] signature);
		public abstract SymmetricEncryptionResult Encrypt(byte[] data);
		public abstract byte[] Decrypt(SymmetricEncryptionResult data);
		public abstract byte[] Encrypt(byte[] encryptionPublicKey, byte[] data);
		public abstract byte[] Decrypt(byte[] decryptionPrivateKey, byte[] data);
		public abstract byte[] Hash(byte[] data);
		public abstract void GenerateSigningKeyPair(out byte[] keyPair, out byte[] publicKey);
		public abstract void GenerateEncryptionKeyPair(out byte[] keyPair, out byte[] publicKey);
	}
}
