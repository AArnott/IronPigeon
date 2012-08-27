namespace ConsoleChat {
	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using System.Windows.Forms;
	using IronPigeon;
	using IronPigeon.Providers;
	using Microsoft;
	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.StorageClient;

	class Program {
		private const string AzureTableStorageName = "inbox";

		private const string AzureBlobStorageContainerName = "consoleapptest";

		[STAThread]
		static void Main(string[] args) {
			DoAsync().GetAwaiter().GetResult();
		}

		static async Task DoAsync() {
			CloudStorageAccount.SetConfigurationSettingPublisher(
				(name, func) => func(ConfigurationManager.ConnectionStrings[name].ConnectionString));
			var azureAccount = CloudStorageAccount.FromConfigurationSetting("StorageConnectionString");

			var blobStorage = new AzureBlobStorage(azureAccount, AzureBlobStorageContainerName);
			var cryptoServices = new DesktopCryptoProvider(SecurityLevel.Minimal);
			var ownEndpoint = await CreateOrOpenEndpointAsync(cryptoServices);
			if (ownEndpoint == null) {
				return;
			}

			await InitializeLocalCloudAsync(azureAccount, blobStorage);
			var channel = new Channel(blobStorage, cryptoServices, ownEndpoint);
			await channel.CreateInboxAsync(new Uri(ConfigurationManager.ConnectionStrings["RelayService"].ConnectionString));
			var shareableAddress = await channel.PublishAddressBookEntryAsync();
			Console.WriteLine("Public receiving endpoint: {0}", shareableAddress.AbsoluteUri);

			Endpoint friend = await GetFriendEndpointAsync(cryptoServices, channel.Endpoint.PublicEndpoint);

			await ChatLoopAsync(channel, friend);
		}

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

		private static async Task<OwnEndpoint> CreateOrOpenEndpointAsync(DesktopCryptoProvider cryptoServices) {
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

				await Task.WhenAll(incoming.Select(payload => channel.DeleteInboxItem(payload)));
			}
		}

		private static async Task InitializeLocalCloudAsync(CloudStorageAccount azureAccount, AzureBlobStorage blobStorage) {
			var tableStorage = azureAccount.CreateCloudTableClient();
			await Task.WhenAll(
				tableStorage.CreateTableIfNotExistAsync(AzureTableStorageName),
				blobStorage.CreateContainerIfNotExistAsync());
		}
	}
}
