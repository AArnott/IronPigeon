// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace ConsoleChat
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using System.Windows.Threading;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using IronPigeon;
    using IronPigeon.Providers;
    using IronPigeon.Relay;
    using MessagePack;
    using Microsoft;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.VisualStudio.Threading;

    /// <summary>
    /// Simple console app that demonstrates the IronPigeon protocol in a live chat program.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// The name of the table in Azure Table storage to create.
        /// </summary>
        private const string AzureTableStorageName = "inbox";

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        /// <param name="channel">The channel to use for communication.</param>
        internal Program(Channel channel)
        {
            this.Channel = channel;
            this.RelayServer = new RelayServer(channel.HttpClient, channel.Endpoint);
        }

        /// <summary>
        /// Gets the channel.
        /// </summary>
        public Channel Channel { get; }

        /// <summary>
        /// Gets the relay server.
        /// </summary>
        public RelayServer RelayServer { get; }

        /// <summary>
        /// Entrypoint to the console application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                Console.WriteLine("Canceling...");
                cts.Cancel();
                e.Cancel = true;
            };

            var frame = new DispatcherFrame();
            Task startTask = StartAsync(cts.Token);
            startTask.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default).Forget();
            Dispatcher.PushFrame(frame);
        }

        private static async Task StartAsync(CancellationToken cancellationToken)
        {
            using var httpClient = new HttpClient();
            using var httpClientChannel = new HttpClient();

            var relayService = new RelayCloudBlobStorageProvider(httpClient)
            {
                BlobPostUrl = new Uri(ConfigurationManager.ConnectionStrings["RelayBlobService"].ConnectionString),
                InboxFactoryUrl = new Uri(ConfigurationManager.ConnectionStrings["RelayInboxService"].ConnectionString),
            };

            CryptoSettings cryptoSettings = CryptoSettings.Recommended;
            EndpointAndAddressBookUri? saved = await CreateOrOpenEndpointAsync(cryptoSettings, relayService, relayService, cancellationToken);
            if (saved is null)
            {
                return;
            }

            var channel = new Channel(httpClientChannel, saved.Endpoint, relayService, cryptoSettings);

            Console.WriteLine("Public receiving endpoint: {0}", saved.AddressBookUri.AbsoluteUri);

            Endpoint friend = await GetFriendEndpointAsync(httpClient, saved.Endpoint.PublicEndpoint);

            var program = new Program(channel);

            await program.ChatLoopAsync(friend, cancellationToken);
        }

        /// <summary>
        /// A helper method that purges all of blob storage.
        /// </summary>
        /// <param name="blobService">The blob account to purge.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task PurgeAllAsync(BlobServiceClient blobService, CancellationToken cancellationToken)
        {
            Requires.NotNull(blobService, nameof(blobService));

            await foreach (BlobContainerItem containerItem in blobService.GetBlobContainersAsync(cancellationToken: cancellationToken))
            {
                if (containerItem.Name != "wad-control-container")
                {
                    Console.WriteLine("\nContainer: {0}", containerItem.Name);
                    if (containerItem.Name.StartsWith("unittests", StringComparison.Ordinal))
                    {
                        await blobService.DeleteBlobContainerAsync(containerItem.Name, cancellationToken: cancellationToken);
                    }
                    else
                    {
                        BlobContainerClient container = blobService.GetBlobContainerClient(containerItem.Name);
                        await foreach (BlobItem? blob in container.GetBlobsAsync(traits: BlobTraits.Metadata, cancellationToken: cancellationToken))
                        {
                            Console.WriteLine("\tBlob: {0} {1}", blob.Name, blob.Metadata["DeleteAfter"]);
                            await container.DeleteBlobAsync(blob.Name, cancellationToken: cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Ensures that the Azure blob container and table are created in the Azure account.
        /// </summary>
        /// <param name="azureAccount">The Azure account in use.</param>
        /// <param name="blobStorage">The blob storage.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task InitializeLocalCloudAsync(CloudStorageAccount azureAccount, AzureBlobStorage blobStorage, CancellationToken cancellationToken)
        {
            CloudTableClient? tableStorage = azureAccount.CreateCloudTableClient();
            await Task.WhenAll(
                tableStorage.GetTableReference(AzureTableStorageName).CreateIfNotExistsAsync(cancellationToken),
                blobStorage.CreateContainerIfNotExistAsync(cancellationToken));
        }

        /// <summary>
        /// Queries the user for the remote endpoint to send messages to.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use.</param>
        /// <param name="defaultEndpoint">The user's own endpoint, to use for loopback demos in the event the user has no friend to talk to.</param>
        /// <returns>A task whose result is the remote endpoint to use.</returns>
        private static async Task<Endpoint> GetFriendEndpointAsync(HttpClient httpClient, Endpoint defaultEndpoint)
        {
            do
            {
                Console.Write("Enter your friend's public endpoint URL (leave blank for loopback): ");
                string url = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(url))
                {
                    return defaultEndpoint;
                }

                var addressBook = new DirectEntryAddressBook(httpClient);
                Endpoint? endpoint = await addressBook.LookupAsync(url);
                if (endpoint != null)
                {
                    return endpoint;
                }
                else
                {
                    Console.WriteLine("Unable to find endpoint.");
                    continue;
                }
            }
            while (true);
        }

        /// <summary>
        /// Creates a new local endpoint to identify the user, or opens a previously created one.
        /// </summary>
        /// <returns>A task whose result is the local user's own endpoint.</returns>
        private static async Task<EndpointAndAddressBookUri?> CreateOrOpenEndpointAsync(CryptoSettings cryptoSettings, IEndpointInboxFactory inboxFactory, ICloudBlobStorageProvider blobProvider, CancellationToken cancellationToken)
        {
            switch (MessageBox.Show("Do you have an existing endpoint you want to open?", "Endpoint selection", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question))
            {
                case DialogResult.Yes:
                    {
                        using var openFile = new OpenFileDialog();
                        if (openFile.ShowDialog() == DialogResult.Cancel)
                        {
                            return null;
                        }

                        using Stream? fileStream = openFile.OpenFile();
                        return await MessagePackSerializer.DeserializeAsync<EndpointAndAddressBookUri>(fileStream, Utilities.MessagePackSerializerOptions, cancellationToken);
                    }

                case DialogResult.No:
                    {
                        Console.WriteLine("Creating new endpont. This could take a minute...");
                        OwnEndpoint? result = await OwnEndpoint.CreateAsync(cryptoSettings, inboxFactory, cancellationToken);

                        Uri shareableAddress = await result.PublishAddressBookEntryAsync(blobProvider, cancellationToken);
                        var saved = new EndpointAndAddressBookUri(shareableAddress, result);

                        string privateFilePath = Path.GetTempFileName();
                        using FileStream? stream = File.OpenWrite(privateFilePath);
                        await MessagePackSerializer.SerializeAsync(stream, saved, Utilities.MessagePackSerializerOptions, cancellationToken);
                        Console.WriteLine("Private receiving endpoint: \"{0}\"", privateFilePath);
                        return saved;
                    }

                default:
                    return null;
            }
        }

        /// <summary>
        /// Executes the send/receive loop until the user exits the chat session with the "#quit" command.
        /// </summary>
        /// <param name="friend">The remote endpoint to send messages to.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ChatLoopAsync(Endpoint friend, CancellationToken cancellationToken)
        {
            Console.WriteLine("Use \"#quit\" to exit.");

            while (true)
            {
                Console.Write("> ");
                var line = Console.ReadLine();
                if (line == "#quit")
                {
                    return;
                }

                if (line.Length > 0)
                {
                    using var payload = new MemoryStream(Encoding.UTF8.GetBytes(line));
                    await this.Channel.PostAsync(payload, new System.Net.Mime.ContentType("text/plain"), new[] { friend }, DateTime.UtcNow + TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
                }

                Console.WriteLine("Awaiting friend's reply...");
                await foreach (InboxItem inboxItem in this.Channel.ReceiveInboxItemsAsync(longPoll: true, cancellationToken))
                {
                    using var payload = new MemoryStream();
                    await inboxItem.PayloadReference.DownloadPayloadAsync(this.Channel.HttpClient, payload, cancellationToken: cancellationToken);
                    string message = Encoding.UTF8.GetString(payload.ToArray());
                    Console.WriteLine("< {0}", message);

                    if (inboxItem.RelayServerItem is object)
                    {
                        await this.RelayServer.DeleteInboxItemAsync(inboxItem.RelayServerItem, cancellationToken);
                    }
                }
            }
        }
    }
}
