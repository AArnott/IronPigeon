namespace IronPigeon.Relay.Controllers {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Web;
	using System.Web.Mvc;
	using DotNetOpenAuth.OAuth2;
	using DotNetOpenAuth.Messaging;
	using Validation;
	using System.Configuration;
	using System.Net;
	using System.Net.Http;
	using System.Threading.Tasks;
	using Newtonsoft.Json;
	using System.IO;

#if !DEBUG
	[RequireHttps]
#endif
	public class OAuthController : Controller {
		private static readonly AuthorizationServerDescription LiveConnectService = new AuthorizationServerDescription {
			AuthorizationEndpoint = new Uri("https://login.live.com/oauth20_authorize.srf"),
			TokenEndpoint = new Uri("https://login.live.com/oauth20_token.srf"),
		};

		private static readonly WebServerClient LiveConnectClient = new WebServerClient(
			LiveConnectService,
			ConfigurationManager.AppSettings["TrustedClientId"],
			ClientCredentialApplicator.PostParameter(ConfigurationManager.AppSettings["TrustedClientSecret"]));

		private AuthorizationServer authorizationServer;

		public OAuthController()
			: this(new AuthorizationServer(new AuthorizationServerHost())) {

		}

		public OAuthController(AuthorizationServer authorizationServer) {
			Requires.NotNull(authorizationServer, "authorizationServer");
			this.authorizationServer = authorizationServer;
			this.HttpClient = new HttpClient();
		}

		public HttpClient HttpClient { get; set; }

		public ActionResult Token() {
			return this.authorizationServer.HandleTokenRequest(this.Request).AsActionResult();
		}

		public ActionResult Authorize() {
			var incomingAuthzRequest = this.authorizationServer.ReadAuthorizationRequest(this.Request);
			if (incomingAuthzRequest == null) {
				return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Missing authorization request");
			}

			var returnTo = new UriBuilder(this.Url.AbsoluteAction("AuthorizeWithMicrosoftAccount"));
			returnTo.AppendQueryArgument("nestedAuth", this.Request.Url.Query);
			var scopes = new[] { "wl.signin" };
			return LiveConnectClient.PrepareRequestUserAuthorization(scopes, returnTo.Uri).AsActionResult();
		}

		public async Task<ActionResult> AuthorizeWithMicrosoftAccount(string nestedAuth) {
			var authorizationState = LiveConnectClient.ProcessUserAuthorization(this.Request);
			var accessToken = authorizationState.AccessToken;
			if (String.IsNullOrEmpty(accessToken)) {
				throw new ArgumentNullException("accessToken");
			}

			// Rebuild the original authorization request from the client.
			var reconstitutedRequestUri = new UriBuilder(this.Request.Url);
			reconstitutedRequestUri.Query = nestedAuth.Substring(1);
			var reconstitutedRequestInfo = HttpRequestInfo.Create("GET", reconstitutedRequestUri.Uri);
			var incomingAuthzRequest = this.authorizationServer.ReadAuthorizationRequest(reconstitutedRequestInfo);
			if (incomingAuthzRequest == null) {
				return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Missing authorization request");
			}

			// Since this authz request has gone through at least one untrusted leg, make sure it's following our rules.
			// We're auto-approving this, but only want to do so if it's the client we trust.
			if (incomingAuthzRequest.ClientIdentifier != ConfigurationManager.AppSettings["TrustedClientPackageId"]) {
				return new HttpUnauthorizedResult("Not a trusted client.");
			}

			var uri = new Uri("https://apis.live.net/v5.0/me?access_token=" + Uri.EscapeDataString(accessToken));
			var result = await this.HttpClient.GetAsync(uri);
			result.EnsureSuccessStatusCode();
			string jsonUserInfo = await result.Content.ReadAsStringAsync();
			var serializer = new JsonSerializer();
			var jsonReader = new JsonTextReader(new StringReader(jsonUserInfo));
			var microsoftAccountInfo = serializer.Deserialize<MicrosoftAccountInfo>(jsonReader);

			var response = this.authorizationServer.PrepareApproveAuthorizationRequest(incomingAuthzRequest, microsoftAccountInfo.Id, new[] { "AddressBook" });
			return this.authorizationServer.Channel.PrepareResponse(response).AsActionResult();
		}

		private class MicrosoftAccountInfo {
			public string Id { get; set; }
		}
	}
}
