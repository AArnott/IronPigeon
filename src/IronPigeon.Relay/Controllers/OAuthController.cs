namespace IronPigeon.Relay.Controllers {
	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Runtime.Serialization;
	using System.Threading.Tasks;
	using System.Web;
	using System.Web.Mvc;
	using DotNetOpenAuth.Messaging;
	using DotNetOpenAuth.OAuth2;
	using IronPigeon.Relay.Code;
	using IronPigeon.Relay.Models;
	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.Storage;
	using Microsoft.WindowsAzure.StorageClient;
	using Newtonsoft.Json;
	using Validation;

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
			: this(new AuthorizationServer(new AuthorizationServerHost()), AddressBookController.DefaultTableName, AddressBookController.EmailTableName, AzureStorageConfig.DefaultCloudConfigurationName) {
		}

		public OAuthController(AuthorizationServer authorizationServer, string primaryTableName, string emailTableName, string cloudConfigurationName) {
			Requires.NotNull(authorizationServer, "authorizationServer");
			Requires.NotNullOrEmpty(cloudConfigurationName, "cloudConfigurationName");

			this.authorizationServer = authorizationServer;
			this.HttpClient = new HttpClient();

			var storage = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings[cloudConfigurationName].ConnectionString);
			var tableClient = storage.CreateCloudTableClient();
			this.ClientTable = new AddressBookContext(tableClient, primaryTableName, emailTableName);
		}

		public AddressBookContext ClientTable { get; set; }

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
			var scopes = new[] { "wl.signin", "wl.basic", "wl.emails" }; // this is a subset of what the client app asks for, ensuring automatic approval.
			return LiveConnectClient.PrepareRequestUserAuthorization(scopes, returnTo.Uri).AsActionResult();
		}

		public async Task<ActionResult> AuthorizeWithMicrosoftAccount(string nestedAuth) {
			var authorizationState = LiveConnectClient.ProcessUserAuthorization(this.Request);
			var accessToken = authorizationState.AccessToken;
			if (string.IsNullOrEmpty(accessToken)) {
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

			await this.SaveAccountInfoAsync(microsoftAccountInfo);

			var response = this.authorizationServer.PrepareApproveAuthorizationRequest(incomingAuthzRequest, microsoftAccountInfo.Id, new[] { "AddressBook" });
			return this.authorizationServer.Channel.PrepareResponse(response).AsActionResult();
		}

		private async Task SaveAccountInfoAsync(MicrosoftAccountInfo microsoftAccountInfo) {
			Requires.NotNull(microsoftAccountInfo, "microsoftAccountInfo");
			Requires.That(microsoftAccountInfo.Emails != null && microsoftAccountInfo.Emails.Count > 0, "microsoftAccountInfo", "No emails were provided by Live Connect.");

			var entity = await this.ClientTable.GetAsync(AddressBookEntity.MicrosoftProvider, microsoftAccountInfo.Id);
			if (entity == null) {
				entity = new AddressBookEntity();
				entity.Provider = AddressBookEntity.MicrosoftProvider;
				entity.UserId = microsoftAccountInfo.Id;
				this.ClientTable.AddObject(entity);
			} else {
				this.ClientTable.UpdateObject(entity);
			}

			entity.FirstName = microsoftAccountInfo.FirstName;
			entity.LastName = microsoftAccountInfo.LastName;

			var previouslyRecordedEmails = await this.ClientTable.GetEmailAddressesAsync(entity);

			var previouslyRecordedEmailAddresses = new HashSet<string>(previouslyRecordedEmails.Select(e => e.Email));
			previouslyRecordedEmailAddresses.ExceptWith(microsoftAccountInfo.Emails.Values);

			var freshEmailAddresses = new HashSet<string>(microsoftAccountInfo.Emails.Values.Where(v => v != null));
			freshEmailAddresses.ExceptWith(previouslyRecordedEmails.Select(e => e.Email));

			foreach (var previouslyRecordedEmailAddress in previouslyRecordedEmailAddresses) {
				this.ClientTable.DeleteObject(
					previouslyRecordedEmails.FirstOrDefault(e => e.Email == previouslyRecordedEmailAddress));
			}

			foreach (var freshEmailAddress in freshEmailAddresses) {
				var newEmailEntity = new AddressBookEmailEntity {
					Email = freshEmailAddress,
					MicrosoftEmailHash = MicrosoftTools.GetEmailHash(freshEmailAddress),
					AddressBookEntityRowKey = entity.RowKey,
				};
				this.ClientTable.AddObject(newEmailEntity);
			}

			await this.ClientTable.SaveChangesWithRetriesAsync();
		}

		[DataContract]
		public class MicrosoftAccountInfo {
			[DataMember]
			public string Id { get; set; }

			[DataMember(Name = "first_name")]
			public string FirstName { get; set; }

			[DataMember(Name = "last_name")]
			public string LastName { get; set; }

			[DataMember(Name = "emails")]
			public IDictionary<string, string> Emails { get; set; }
		}
	}
}
