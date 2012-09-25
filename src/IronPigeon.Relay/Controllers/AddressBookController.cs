namespace IronPigeon.Relay.Controllers {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Threading.Tasks;
	using System.Web;
	using System.Web.Mvc;
	using IronPigeon.Relay.Models;
	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.StorageClient;
	using Newtonsoft.Json;
	using Validation;
#if !NET40
	using TaskEx = System.Threading.Tasks.Task;
#endif

	/// <summary>
	/// This controller serves URLs that may appear to the user, but represent the downloadable address book entry
	/// for IronPigeon communication.
	/// </summary>
#if !DEBUG
	[RequireHttps]
#endif
	public class AddressBookController : Controller {
		/// <summary>
		/// The name of the table in Azure table storage where address book entries are stored.
		/// </summary>
		internal const string DefaultTableName = "AddressBooks";

		internal const string EmailTableName = "AddressBookEmails";

		/// <summary>
		/// Initializes a new instance of the <see cref="AddressBookController" /> class.
		/// </summary>
		public AddressBookController()
			: this(DefaultTableName, EmailTableName, AzureStorageConfig.DefaultCloudConfigurationName) {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AddressBookController" /> class.
		/// </summary>
		/// <param name="tableName">Name of the table where address book entries are stored.</param>
		/// <param name="cloudConfigurationName">Name of the cloud configuration.</param>
		public AddressBookController(string tableName, string emailTableName, string cloudConfigurationName) {
			Requires.NotNullOrEmpty(cloudConfigurationName, "cloudConfigurationName");

			var storage = CloudStorageAccount.FromConfigurationSetting(cloudConfigurationName);
			var tableClient = storage.CreateCloudTableClient();
			this.ClientTable = new AddressBookContext(tableClient, tableName, emailTableName);
			this.HttpClient = new HttpClient();
		}

		public AddressBookContext ClientTable { get; set; }

		public HttpClient HttpClient { get; set; }

		/// <summary>
		/// Returns the address book entry, or an HTML page for browsers.
		/// GET: /AddressBook/?blob={uri}
		/// </summary>
		/// <param name="blob">The blob address to redirect a programmatic client to.</param>
		/// <returns>The HTTP response.</returns>
		public ActionResult Index(string blob) {
			Requires.NotNullOrEmpty(blob, "blob");

			var blobUri = new Uri(blob, UriKind.Absolute);
			if (!this.Request.AcceptTypes.Contains(AddressBookEntry.ContentType) && this.Request.AcceptTypes.Contains("text/html")) {
				// This looks like a browser rather than an IronPigeon client.
				// Return an HTML page that describes what IronPigeon is.
				return this.View();
			}

			return this.Redirect(blobUri.AbsoluteUri);
		}

		[HttpPut, ActionName("MicrosoftAccount"), OAuthAuthorize(Roles = "AddressBook")]
		public async Task<ActionResult> PutMicrosoftAccount(string id) {
			string addressBookBlobUri = this.Request.Form["addressBookBlobUri"];
			new Uri(addressBookBlobUri, UriKind.Absolute); // throws if invalid arg

			if (id != this.HttpContext.User.Identity.Name) {
				return new HttpUnauthorizedResult();
			}

			var entity = await this.ClientTable.GetAsync(AddressBookEntity.MicrosoftProvider, this.HttpContext.User.Identity.Name);
			if (entity == null) {
				entity = new AddressBookEntity();
				entity.Provider = AddressBookEntity.MicrosoftProvider;
				entity.UserId = this.HttpContext.User.Identity.Name;
				this.ClientTable.AddObject(entity);
			} else {
				this.ClientTable.UpdateObject(entity);
			}

			entity.AddressBookUrl = addressBookBlobUri;

			await this.ClientTable.SaveChangesAsync();
			return new EmptyResult();
		}

		[HttpGet, ActionName("MicrosoftAccount")]
		public async Task<ActionResult> GetMicrosoftAccount(string id) {
			var entity = await this.ClientTable.GetAsync(AddressBookEntity.MicrosoftProvider, id);
			if (entity == null) {
				return this.HttpNotFound();
			}

			return this.Redirect(entity.AddressBookUrl);
		}

		[HttpGet, ActionName("entry")]
		public async Task<ActionResult> GetAddressBookEntryByEmailHash(string email, string emailHash) {
			AddressBookEntity entry = null;
			if (!string.IsNullOrEmpty(email)) {
				entry = await this.ClientTable.GetAddressBookEntityByEmailAsync(email);
			} else if (!string.IsNullOrEmpty(emailHash)) {
				entry = await this.ClientTable.GetAddressBookEntityByEmailHashAsync(emailHash);
			} else {
				return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
			}

			if (entry == null) {
				return this.HttpNotFound();
			}

			return this.Redirect(entry.AddressBookUrl);
		}

		internal static async Task OneTimeInitializeAsync(CloudStorageAccount azureAccount) {
			var tableClient = azureAccount.CreateCloudTableClient();
			await TaskEx.WhenAll(
				tableClient.CreateTableIfNotExistAsync(DefaultTableName),
				tableClient.CreateTableIfNotExistAsync(EmailTableName));
		}
	}
}
