namespace IronPigeon.Relay.Controllers {
	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Threading.Tasks;
	using System.Web.Mvc;
	using IronPigeon.Relay.Models;
	using Validation;
	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.StorageClient;
	using Newtonsoft.Json.Linq;

#if !DEBUG
	[RequireHttps]
#endif
	public class WindowsPushNotificationClientController : Controller {
		/// <summary>
		/// The key to the Azure account configuration information.
		/// </summary>
		internal const string DefaultCloudConfigurationName = "StorageConnectionString";

		internal const string DefaultTableName = "WindowsPushNotificationClients";

		public WindowsPushNotificationClientController()
			: this(DefaultTableName, DefaultCloudConfigurationName) {
		}

		public WindowsPushNotificationClientController(string tableName, string cloudConfigurationName) {
			Requires.NotNullOrEmpty(cloudConfigurationName, "cloudConfigurationName");

			var storage = CloudStorageAccount.FromConfigurationSetting(cloudConfigurationName);
			this.TableClient = storage.CreateCloudTableClient();
			this.ClientTable = new PushNotificationContext(this.TableClient, tableName);
			this.HttpClient = new HttpClient();
		}

		public HttpClient HttpClient { get; set; }

		public PushNotificationContext ClientTable { get; set; }

		public CloudTableClient TableClient { get; set; }

		[ActionName("Index"), HttpGet]
		public ActionResult Form() {
			return this.View();
		}

		[ActionName("Index"), HttpPost]
		public async Task<ActionResult> PutClient(PushNotificationClientEntity clientEntity) {
			Requires.NotNull(clientEntity, "clientEntity");

			if (this.TryValidateModel(clientEntity)) {
				clientEntity.ClientSecret = clientEntity.ClientSecret.Trim();
				clientEntity.PackageSecurityIdentifier = clientEntity.PackageSecurityIdentifier.Trim();

				// Check that the credentials are accurate before storing them.  This prevents DoS
				// attacks where folks could specify the wrong client secret for an existing registration
				// to thwart obtaining access tokens in the future.
				await clientEntity.AcquireWnsPushBearerTokenAsync(this.HttpClient);
				if (clientEntity.AccessToken != null) {
					var existingClient = await this.ClientTable.GetAsync(clientEntity.PackageSecurityIdentifier);
					if (existingClient != null) {
						existingClient.ClientSecret = clientEntity.ClientSecret;
						existingClient.AccessToken = clientEntity.AccessToken;
						this.ClientTable.UpdateObject(existingClient);
					} else {
						// Recreate with only the whitelisted properties copied over, to mitigate against 
						// artificially created posts that include more properties than we intend to allow
						// the client to submit.
						var newClient = new PushNotificationClientEntity(
							clientEntity.PackageSecurityIdentifier, clientEntity.ClientSecret);
						newClient.AccessToken = clientEntity.AccessToken;
						this.ClientTable.AddObject(newClient);
					}

					await this.ClientTable.SaveChangesAsync();
					this.ViewData["Successful"] = true;
				}
			}

			return this.View();
		}

		/// <summary>
		/// Stores a client SID and secret so that push notifications can be sent to that app.
		/// </summary>
		/// <param name="id">The Package Security Identifier (SID) of the client app.</param>
		/// <param name="clientSecret">The client secret of the app.</param>
		/// <returns>The asynchronous operation.</returns>
		public async Task Put(string id, string clientSecret) {
			var client = new PushNotificationClientEntity(id, clientSecret);
			this.ClientTable.AddObject(client);
			await this.ClientTable.SaveChangesAsync();
		}

		/// <summary>
		/// Removes a client SID and secret so that push notifications can be sent to that app.
		/// </summary>
		/// <param name="id">The Package Security Identifier (SID) of the client app.</param>
		/// <param name="clientSecret">The client secret of the app.</param>
		/// <returns>The asynchronous operation.</returns>
		public async Task Delete(string id, string clientSecret) {
			var client = await this.ClientTable.GetAsync(id);
			if (client == null) {
				throw new ArgumentException();
			}

			if (client.ClientSecret != clientSecret) {
				throw new ArgumentException();
			}

			this.ClientTable.DeleteObject(client);
			await this.ClientTable.SaveChangesAsync();
		}

		internal static async Task OneTimeInitializeAsync(CloudStorageAccount azureAccount) {
			var tableClient = azureAccount.CreateCloudTableClient();
			await tableClient.CreateTableIfNotExistAsync(DefaultTableName);
		}
	}
}