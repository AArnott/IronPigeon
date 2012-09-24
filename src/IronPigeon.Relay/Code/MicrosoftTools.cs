namespace IronPigeon.Relay.Code {
	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using System.Web;
	using Validation;

	public static class MicrosoftTools {
		public static string GetEmailHash(string email) {
			Requires.NotNullOrEmpty(email, "email");

			email = email.Trim();
			var clientId = ConfigurationManager.AppSettings["TrustedClientId"].Trim();

			var concat = email + clientId;
			concat = concat.ToLowerInvariant();
			var buffer = Encoding.UTF8.GetBytes(concat);
			using (var hashAlgorithm = HashAlgorithm.Create("sha256")) {
				var hashBuffer = hashAlgorithm.ComputeHash(buffer);
				string liveHash = ByteArrayToString(hashBuffer);
				return liveHash;
			}
		}

		private static string ByteArrayToString(byte[] ba) {
			StringBuilder hex = new StringBuilder(ba.Length * 2);
			foreach (byte b in ba) {
				hex.AppendFormat("{0:x2}", b);
			}

			return hex.ToString();
		}
	}
}