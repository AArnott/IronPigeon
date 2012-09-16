namespace IronPigeon.Relay.Models {
	using System;
	using System.Collections.Generic;
	using System.ComponentModel.DataAnnotations;
#if !NET40
	using System.ComponentModel.DataAnnotations.Schema;
#endif
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Threading.Tasks;
	using System.Web.Http;
	using Newtonsoft.Json.Linq;
	using Validation;

	public class PushNotificationClientEntity : TableStorageEntity {
		internal const string SchemePrefix = "ms-app://";

		private const string DefaultPartition = "PushNotifications";

		public PushNotificationClientEntity() {
			this.PartitionKey = DefaultPartition;
		}

		public PushNotificationClientEntity(string packageSecurityIdentifier, string clientSecret)
			: this() {
			this.PackageSecurityIdentifier = packageSecurityIdentifier;
			this.ClientSecret = clientSecret;
		}

		[Required(AllowEmptyStrings = false), StringLength(255, MinimumLength = 1)]
		[Display(Name = "Package security identifier")]
#if !NET40
		[NotMapped]
#endif
		public string PackageSecurityIdentifier {
			get {
				return SchemePrefix + this.RowKey;
			}

			set {
				Requires.Argument(value == null || value.StartsWith(SchemePrefix), "value", "Prefix {0} not found", SchemePrefix);
				this.RowKey = value.Substring(SchemePrefix.Length);
			}
		}

		[Required(AllowEmptyStrings = false), StringLength(255, MinimumLength = 1)]
		[Display(Name = "Client secret")]
		public string ClientSecret { get; set; }

		public string AccessToken { get; set; }

		public async Task<string> AcquireWnsPushBearerTokenAsync(HttpClient httpClient) {
			Requires.NotNull(httpClient, "httpClient");

			var tokenEndpoint = new Uri("https://login.live.com/accesstoken.srf");
			const string Scope = "notify.windows.com";

			var formData = new Dictionary<string, string> {
					                                              { "grant_type", "client_credentials" },
					                                              { "client_id", this.PackageSecurityIdentifier },
					                                              { "client_secret", this.ClientSecret },
					                                              { "scope", Scope },
				                                              };
			var content = new FormUrlEncodedContent(formData);
			var response = await httpClient.PostAsync(tokenEndpoint, content);
			response.EnsureSuccessStatusCode();
			var json = await response.Content.ReadAsStringAsync();
			var responseObj = JObject.Parse(json);
			this.AccessToken = (string)responseObj["access_token"];
			return this.AccessToken;
		}
	}
}
