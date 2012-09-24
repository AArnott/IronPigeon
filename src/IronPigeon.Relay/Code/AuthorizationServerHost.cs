namespace IronPigeon.Relay {
	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Web;
	using DotNetOpenAuth.Messaging.Bindings;
	using DotNetOpenAuth.OAuth2;
	using DotNetOpenAuth.OAuth2.ChannelElements;
	using DotNetOpenAuth.OAuth2.Messages;

	public class AuthorizationServerHost : IAuthorizationServerHost {
		public ICryptoKeyStore CryptoKeyStore {
			get { throw new NotImplementedException(); }
		}

		public INonceStore NonceStore {
			get { throw new NotImplementedException(); }
		}

		public AccessTokenResult CreateAccessToken(IAccessTokenRequest accessTokenRequestMessage) {
			var rsa = new RSACryptoServiceProvider();
			rsa.ImportCspBlob(Convert.FromBase64String(ConfigurationManager.AppSettings["PrivateAsymmetricKey"]));

			var accessToken = new AuthorizationServerAccessToken() {
				AccessTokenSigningKey = rsa,
				ResourceServerEncryptionKey = rsa,
			};
			var result = new AccessTokenResult(accessToken);
			result.AllowRefreshToken = false;
			return result;
		}

		public IClientDescription GetClient(string clientIdentifier) {
			if (clientIdentifier == ConfigurationManager.AppSettings["TrustedClientPackageId"]) {
				return new ClientDescription(
					ConfigurationManager.AppSettings["TrustedClientSecret"], // the client secret technically isn't necessary since we only use implicit grants.
					new Uri(ConfigurationManager.AppSettings["TrustedClientPackageId"]),
					ClientType.Public);
			} else {
				throw new ArgumentException();
			}
		}

		public bool IsAuthorizationValid(IAuthorizationDescription authorization) {
			throw new NotImplementedException();
		}

		public bool TryAuthorizeClientCredentialsGrant(IAccessTokenRequest accessRequest) {
			throw new NotImplementedException();
		}

		public bool TryAuthorizeResourceOwnerCredentialGrant(string userName, string password, IAccessTokenRequest accessRequest, out string canonicalUserName) {
			throw new NotImplementedException();
		}
	}
}