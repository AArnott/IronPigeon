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
			await blobStorage.CreateContainerIfNotExistAsync();

			var tableStorage = azureAccount.CreateCloudTableClient();
			await tableStorage.CreateTableIfNotExistAsync("inbox");

			var cryptoServices = new DesktopCryptoProvider() {
				EncryptionAsymmetricKeySize = 512, // use small key sizes so tests run faster
				SignatureAsymmetricKeySize = 512,
				BlobSymmetricKeySize = 128,
			};

			var channel = new Channel() {
				CloudBlobStorage = blobStorage,
				CryptoServices = cryptoServices,
			};

			channel.Endpoint = OwnEndpoint.Create(channel.CryptoServices);
			await channel.RegisterRelayAsync(new Uri("http://localhost:39472/Inbox/"));

			string privateFilePath = Path.GetTempFileName();
			using (var writer = new BinaryWriter(File.OpenWrite(privateFilePath))) {
				writer.SerializeDataContract(channel.Endpoint);
			}

			Console.WriteLine("Saved full receiving endpoint data to: \"{0}\".", privateFilePath);

			var abe = channel.Endpoint.CreateAddressBookEntry(cryptoServices);
			var abeWriter = new StringWriter();
			await Utilities.SerializeDataContractAsBase64Async(abeWriter, abe);
			Console.WriteLine("Your address book entry is:\n{0}\n", abeWriter);
			string abeFileName = Path.GetTempFileName();
			File.WriteAllText(abeFileName, abeWriter.ToString());
			Console.WriteLine("This has been copied to \"{0}\".", abeFileName);
			Console.WriteLine(
				"If you upload this file to the web, append this fragment to the URL you share out: #{0}",
				cryptoServices.CreateWebSafeBase64Thumbprint(channel.Endpoint.PublicEndpoint.SigningKeyPublicMaterial));

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
