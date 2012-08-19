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

	using Microsoft.WindowsAzure;
	using Microsoft.WindowsAzure.StorageClient;

	class Program {
		static void Main(string[] args) {
			DoAsync().GetAwaiter().GetResult();
		}

		static async Task DoAsync() {
			CloudStorageAccount.SetConfigurationSettingPublisher(
				(name, func) => func(ConfigurationManager.ConnectionStrings[name].ConnectionString));
			var azureAccount = CloudStorageAccount.FromConfigurationSetting("StorageConnectionString");

			var blobStorage = new AzureBlobStorage(azureAccount, "consoleapptest");
			var cryptoServices = new DesktopCryptoProvider(SecurityLevel.Minimal);
			var ownEndpoint = OwnEndpoint.Create(cryptoServices);
			var channel = new Channel(blobStorage, cryptoServices, ownEndpoint);

			var tableStorage = azureAccount.CreateCloudTableClient();
			await tableStorage.CreateTableIfNotExistAsync("inbox");
			await blobStorage.CreateContainerIfNotExistAsync();

			await channel.CreateInboxAsync(new Uri(ConfigurationManager.ConnectionStrings["RelayService"].ConnectionString));

			string privateFilePath = Path.GetTempFileName();
			using (var stream = File.OpenWrite(privateFilePath)) {
				await channel.Endpoint.SaveAsync(stream);
			}

			var shareableAddress = await channel.PublishAddressBookEntryAsync();
			Console.WriteLine("Public receiving endpoint: {0}", shareableAddress.AbsoluteUri);
			Console.WriteLine("Private receiving endpoint: \"{0}\".", privateFilePath);

			Endpoint friend = channel.Endpoint.PublicEndpoint;

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

				var incoming = await channel.ReceiveAsync();
				foreach (var payload in incoming) {
					var message = Encoding.UTF8.GetString(payload.Content);
					Console.WriteLine("< {0}", message);
				}

				await Task.WhenAll(incoming.Select(payload => channel.DeleteInboxItem(payload)));
			}
		}
	}
}
