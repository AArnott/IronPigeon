namespace ConsoleChat {
	using System;
	using System.Collections.Generic;
	using System.Composition;
	using System.Composition.Convention;
	using System.Composition.Hosting;
	using System.Configuration;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using System.Windows.Forms;
	using IronPigeon;
	using IronPigeon.Providers;
	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.Storage;
	using Microsoft.WindowsAzure.Storage.Blob;
	using Microsoft.WindowsAzure.StorageClient;
	using Validation;

	/// <summary>
	/// Simple console app that demonstrates the IronPigeon protocol in a live chat program.
	/// </summary>
	[Export]
	internal class Program {
		/// <summary>
		/// The name of the table in Azure Table storage to create.
		/// </summary>
		private const string AzureTableStorageName = "inbox";

		/// <summary>
		/// The name of the container in Azure blob storage to use for message payloads.
		/// </summary>
		private const string AzureBlobStorageContainerName = "consoleapptest";

		/// <summary>
		/// Gets or sets the channel.
		/// </summary>
		[Import]
		public Channel Channel { get; set; }

		/// <summary>
		/// Gets or sets the message relay service.
		/// </summary>
		[Import]
		public RelayCloudBlobStorageProvider MessageRelayService { get; set; }

		/// <summary>
		/// Gets or sets the crypto provider.
		/// </summary>
		[Import]
		public CryptoSettings CryptoProvider { get; set; }

		/// <summary>
		/// Gets or sets the own endpoint services.
		/// </summary>
		[Import]
		public OwnEndpointServices OwnEndpointServices { get; set; }

		/// <summary>
		/// Entrypoint to the console application
		/// </summary>
		/// <param name="args">The arguments passed into the console app.</param>
		[STAThread]
		private static void Main(string[] args) {
			var configuration = new ContainerConfiguration()
				.WithAssembly(typeof(Channel).Assembly)
				.WithPart(typeof(DesktopChannel))
				.WithPart(typeof(Program));
			var container = configuration.CreateContainer();
			
			var program = container.GetExport<Program>();
			program.DoAsync().GetAwaiter().GetResult();
		}

		/// <summary>
		/// A helper method that purges all of blob storage.
		/// </summary>
		/// <param name="azureAccount">The Azure account to clear out.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		private static async Task PurgeAllAsync(CloudStorageAccount azureAccount) {
			Requires.NotNull(azureAccount, "azureAccount");

			var blobClient = azureAccount.CreateCloudBlobClient();
			foreach (var container in blobClient.ListContainers()) {
				if (container.Name != "wad-control-container") {
					Console.WriteLine("\nContainer: {0}", container.Name);
					if (container.Name.StartsWith("unittests")) {
						container.Delete();
					} else {
						var blobs = await container.ListBlobsSegmentedAsync(
							container.Name,
							useFlatBlobListing: true,
							pageSize: 50,
							details: BlobListingDetails.Metadata,
							options: new BlobRequestOptions(),
							operationContext: null);
						foreach (var blob in blobs.Cast<ICloudBlob>()) {
							Console.WriteLine("\tBlob: {0} {1}", blob.Uri, blob.Metadata["DeleteAfter"]);
						}

						await Task.WhenAll(blobs.Cast<ICloudBlob>().Select(b => b.DeleteAsync()));
					}
				}
			}
		}

		/// <summary>
		/// Ensures that the Azure blob container and table are created in the Azure account.
		/// </summary>
		/// <param name="azureAccount">The Azure account in use.</param>
		/// <param name="blobStorage">The blob storage </param>
		/// <returns>A task representing the asynchronous operation.</returns>
		private static async Task InitializeLocalCloudAsync(CloudStorageAccount azureAccount, AzureBlobStorage blobStorage) {
			var tableStorage = azureAccount.CreateCloudTableClient();
			await Task.WhenAll(
				tableStorage.GetTableReference(AzureTableStorageName).CreateIfNotExistsAsync(),
				blobStorage.CreateContainerIfNotExistAsync());
		}

		/// <summary>
		/// The asynchronous entrypoint into the app.
		/// </summary>
		/// <returns>The asynchronous operation.</returns>
		private async Task DoAsync() {
			this.MessageRelayService.BlobPostUrl = new Uri(ConfigurationManager.ConnectionStrings["RelayBlobService"].ConnectionString);
			this.MessageRelayService.InboxServiceUrl = new Uri(ConfigurationManager.ConnectionStrings["RelayInboxService"].ConnectionString);
			this.CryptoProvider.ApplySecurityLevel(SecurityLevel.Minimum);

			this.Channel.Endpoint = await this.CreateOrOpenEndpointAsync();
			if (this.Channel.Endpoint == null) {
				return;
			}

			var shareableAddress = await this.OwnEndpointServices.PublishAddressBookEntryAsync(this.Channel.Endpoint);
			Console.WriteLine("Public receiving endpoint: {0}", shareableAddress.AbsoluteUri);

			Endpoint friend = await this.GetFriendEndpointAsync(this.Channel.Endpoint.PublicEndpoint);

			await this.ChatLoopAsync(friend);
		}

		/// <summary>
		/// Queries the user for the remote endpoint to send messages to.
		/// </summary>
		/// <param name="defaultEndpoint">The user's own endpoint, to use for loopback demos in the event the user has no friend to talk to.</param>
		/// <returns>A task whose result is the remote endpoint to use.</returns>
		private async Task<Endpoint> GetFriendEndpointAsync(Endpoint defaultEndpoint) {
			do {
				Console.Write("Enter your friend's public endpoint URL (leave blank for loopback): ");
				string url = Console.ReadLine();
				if (string.IsNullOrWhiteSpace(url)) {
					return defaultEndpoint;
				}

				var addressBook = new DirectEntryAddressBook(this.CryptoProvider, new System.Net.Http.HttpClient());
				var endpoint = await addressBook.LookupAsync(url);
				if (endpoint != null) {
					return endpoint;
				} else {
					Console.WriteLine("Unable to find endpoint.");
					continue;
				}
			} while (true);
		}

		/// <summary>
		/// Creates a new local endpoint to identify the user, or opens a previously created one.
		/// </summary>
		/// <returns>A task whose result is the local user's own endpoint.</returns>
		private async Task<OwnEndpoint> CreateOrOpenEndpointAsync() {
			OwnEndpoint result;
			switch (MessageBox.Show("Do you have an existing endpoint you want to open?", "Endpoint selection", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question)) {
				case DialogResult.Yes:
					var openFile = new OpenFileDialog();
					if (openFile.ShowDialog() == DialogResult.Cancel) {
						result = null;
						break;
					}

					using (var fileStream = openFile.OpenFile()) {
						result = await OwnEndpoint.OpenAsync(fileStream);
					}

					break;
				case DialogResult.No:
					result = await this.OwnEndpointServices.CreateAsync();
					string privateFilePath = Path.GetTempFileName();
					using (var stream = File.OpenWrite(privateFilePath)) {
						await result.SaveAsync(stream);
					}

					Console.WriteLine("Private receiving endpoint: \"{0}\"", privateFilePath);
					break;
				default:
					result = null;
					break;
			}

			return result;
		}

		/// <summary>
		/// Executes the send/receive loop until the user exits the chat session with the "#quit" command.
		/// </summary>
		/// <param name="friend">The remote endpoint to send messages to.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		private async Task ChatLoopAsync(Endpoint friend) {
			while (true) {
				Console.Write("> ");
				var line = Console.ReadLine();
				if (line == "#quit") {
					return;
				}

				if (line.Length > 0) {
					var payload = new Payload(Encoding.UTF8.GetBytes(line), "text/plain");
					await this.Channel.PostAsync(payload, new[] { friend }, DateTime.UtcNow + TimeSpan.FromMinutes(5));
				}

				Console.WriteLine("Awaiting friend's reply...");
				var incoming = await this.Channel.ReceiveAsync(longPoll: true);
				foreach (var payloadReceipt in incoming) {
					var message = Encoding.UTF8.GetString(payloadReceipt.Payload.Content);
					Console.WriteLine("< {0}", message);
				}

				await Task.WhenAll(incoming.Select(receipt => this.Channel.DeleteInboxItemAsync(receipt.Payload)));
			}
		}
	}
}
