namespace ConsoleChat {
	using System;
	using System.Collections.Generic;
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
	using Validation;
	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.StorageClient;

	/// <summary>
	/// Simple console app that demonstrates the IronPigeon protocol in a live chat program.
	/// </summary>
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
		/// Entrypoint to the console application
		/// </summary>
		/// <param name="args">The arguments passed into the console app.</param>
		[STAThread]
		private static void Main(string[] args) {
			DoAsync().GetAwaiter().GetResult();
		}

		/// <summary>
		/// The asynchronous entrypoint into the app.
		/// </summary>
		/// <returns>The asynchronous operation.</returns>
		private static async Task DoAsync() {
			var configuration = new ContainerConfiguration().WithParts(
				typeof(Channel),
				typeof(DesktopCryptoProvider),
				typeof(RelayCloudBlobStorageProvider),
				typeof(GoogleUrlShortener),
				typeof(TwitterAddressBook),
				typeof(DirectEntryAddressBook));
			var container = configuration.CreateContainer();

			var relayService = container.GetExport<RelayCloudBlobStorageProvider>();
			relayService.PostUrl = new Uri(ConfigurationManager.ConnectionStrings["RelayInboxService"].ConnectionString);

			var cryptoServices = container.GetExport<ICryptoProvider>();
			cryptoServices.ApplySecurityLevel(SecurityLevel.Minimum);

			var ownEndpoint = await CreateOrOpenEndpointAsync(cryptoServices);
			if (ownEndpoint == null) {
				return;
			}

			var channel = container.GetExport<Channel>();
			channel.Endpoint = ownEndpoint;

			await channel.CreateInboxAsync(relayService.PostUrl);
			var shareableAddress = await channel.PublishAddressBookEntryAsync();
			Console.WriteLine("Public receiving endpoint: {0}", shareableAddress.AbsoluteUri);

			Endpoint friend = await GetFriendEndpointAsync(cryptoServices, channel.Endpoint.PublicEndpoint);

			await ChatLoopAsync(channel, friend);
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
						var options = new BlobRequestOptions
						{ UseFlatBlobListing = true, BlobListingDetails = BlobListingDetails.Metadata, };
						var blobs = container.ListBlobs(options).OfType<CloudBlob>().ToList();
						foreach (var blob in blobs) {
							Console.WriteLine("\tBlob: {0} {1}", blob.Uri, blob.Metadata["DeleteAfter"]);
						}

						await Task.WhenAll(blobs.Select(b => b.DeleteAsync()));
					}
				}
			}
		}

		/// <summary>
		/// Queries the user for the remote endpoint to send messages to.
		/// </summary>
		/// <param name="cryptoProvider">The crypto provider in use.</param>
		/// <param name="defaultEndpoint">The user's own endpoint, to use for loopback demos in the event the user has no friend to talk to.</param>
		/// <returns>A task whose result is the remote endpoint to use.</returns>
		private static async Task<Endpoint> GetFriendEndpointAsync(ICryptoProvider cryptoProvider, Endpoint defaultEndpoint) {
			Requires.NotNull(cryptoProvider, "cryptoProvider");

			do {
				Console.Write("Enter your friend's public endpoint URL (leave blank for loopback): ");
				string url = Console.ReadLine();
				if (string.IsNullOrWhiteSpace(url)) {
					return defaultEndpoint;
				}

				var addressBook = new DirectEntryAddressBook(cryptoProvider);
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
		/// <param name="cryptoServices">The crypto provider in use.</param>
		/// <returns>A task whose result is the local user's own endpoint.</returns>
		private static async Task<OwnEndpoint> CreateOrOpenEndpointAsync(ICryptoProvider cryptoServices) {
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
					result = OwnEndpoint.Create(cryptoServices);
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
		/// <param name="channel">The channel to use for sending/receiving messages.</param>
		/// <param name="friend">The remote endpoint to send messages to.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		private static async Task ChatLoopAsync(Channel channel, Endpoint friend) {
			while (true) {
				Console.Write("> ");
				var line = Console.ReadLine();
				if (line == "#quit") {
					return;
				}

				if (line.Length > 0) {
					var payload = new Payload(Encoding.UTF8.GetBytes(line), "text/plain");
					await channel.PostAsync(payload, new[] { friend }, DateTime.UtcNow + TimeSpan.FromMinutes(5));
				}

				Console.WriteLine("Awaiting friend's reply...");
				var incoming = await channel.ReceiveAsync(longPoll: true);
				foreach (var payload in incoming) {
					var message = Encoding.UTF8.GetString(payload.Content);
					Console.WriteLine("< {0}", message);
				}

				await Task.WhenAll(incoming.Select(payload => channel.DeleteInboxItemAsync(payload)));
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
				tableStorage.CreateTableIfNotExistAsync(AzureTableStorageName),
				blobStorage.CreateContainerIfNotExistAsync());
		}
	}
}
