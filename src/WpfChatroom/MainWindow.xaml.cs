// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace WpfChatroom
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Documents;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Navigation;
    using System.Windows.Shapes;
    using IronPigeon;
    using IronPigeon.Dart;
    using IronPigeon.Providers;
    using MessagePack;
    using Microsoft;
    using Microsoft.VisualStudio.Threading;
    using Microsoft.Win32;

    /// <summary>
    /// Interaction logic for MainWindow.xaml.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            this.InitializeComponent();

            this.JoinableTaskContext = new JoinableTaskContext();
            this.HttpClient = new HttpClient();
            this.MessageRelayService = new RelayCloudBlobStorageProvider(this.HttpClient)
            {
                BlobPostUrl = new Uri(ConfigurationManager.ConnectionStrings["RelayBlobService"].ConnectionString),
                InboxFactoryUrl = new Uri(ConfigurationManager.ConnectionStrings["RelayInboxService"].ConnectionString),
            };
        }

        /// <summary>
        /// Gets the <see cref="Microsoft.VisualStudio.Threading.JoinableTaskContext"/> for this application.
        /// </summary>
        public JoinableTaskContext JoinableTaskContext { get; }

        /// <summary>
        /// Gets the HTTP client to use.
        /// </summary>
        public HttpClient HttpClient { get; }

        /// <summary>
        /// Gets the message relay service.
        /// </summary>
        public RelayCloudBlobStorageProvider MessageRelayService { get; }

        /// <summary>
        /// Gets the crypto settings to use.
        /// </summary>
        public CryptoSettings CryptoSettings { get; } = CryptoSettings.Recommended;

        /// <summary>
        /// Gets the channel.
        /// </summary>
        public Channel? Channel => this.PostalService?.Channel;

        /// <summary>
        /// Gets or sets the postal service.
        /// </summary>
        public PostalService? PostalService { get; set; }

        private void CreateNewEndpoint_OnClick(object sender, RoutedEventArgs e)
        {
            this.JoinableTaskContext.Factory.RunAsync(async delegate
            {
                this.CreateNewEndpoint.IsEnabled = false;
                this.CreateNewEndpoint.Cursor = Cursors.AppStarting;
                try
                {
                    Task<OwnEndpoint> endpointTask = OwnEndpoint.CreateAsync(this.CryptoSettings, this.MessageRelayService);
                    var dialog = new SaveFileDialog();
                    bool? result = dialog.ShowDialog(this);
                    if (result.HasValue && result.Value)
                    {
                        OwnEndpoint endpoint = await endpointTask;
                        Uri addressBookEntry = await endpoint.PublishAddressBookEntryAsync(this.MessageRelayService);
                        var fileFormat = new EndpointAndAddressBookUri(addressBookEntry, endpoint);
                        using Stream? stream = dialog.OpenFile();
                        await MessagePackSerializer.SerializeAsync(stream, fileFormat, MessagePackSerializerOptions.Standard);

                        await this.SetEndpointAsync(await endpointTask, addressBookEntry);
                    }
                }
                finally
                {
                    this.CreateNewEndpoint.Cursor = Cursors.Arrow;
                    this.CreateNewEndpoint.IsEnabled = true;
                }
            });
        }

        private void OpenOwnEndpoint_OnClick(object sender, RoutedEventArgs e)
        {
            this.JoinableTaskContext.Factory.RunAsync(async delegate
            {
                this.OpenOwnEndpoint.IsEnabled = false;
                this.OpenOwnEndpoint.Cursor = Cursors.AppStarting;
                try
                {
                    var dialog = new OpenFileDialog();
                    bool? result = dialog.ShowDialog(this);
                    if (result.HasValue && result.Value)
                    {
                        using Stream? fileStream = dialog.OpenFile();
                        EndpointAndAddressBookUri fileFormat = await MessagePackSerializer.DeserializeAsync<EndpointAndAddressBookUri>(fileStream, MessagePackSerializerOptions.Standard);
                        await this.SetEndpointAsync(fileFormat.Endpoint, fileFormat.AddressBookUri);
                    }
                }
                finally
                {
                    this.OpenOwnEndpoint.Cursor = Cursors.Arrow;
                    this.OpenOwnEndpoint.IsEnabled = true;
                }
            });
        }

        private void OpenChatroom_OnClick(object sender, RoutedEventArgs e)
        {
            Verify.Operation(this.PostalService is object, "Endpoint not initialized yet.");
            var chatroomWindow = new ChatroomWindow(this);
            chatroomWindow.Show();
        }

        private void ChatWithAuthor_OnClick(object sender, RoutedEventArgs e)
        {
            this.JoinableTaskContext.Factory.RunAsync(async delegate
            {
                Verify.Operation(this.PostalService is object, "Endpoint not initialized yet.");
                var chatroomWindow = new ChatroomWindow(this);
                chatroomWindow.Show();

                var addressBook = new DirectEntryAddressBook(this.Channel.HttpClient);
                Endpoint? endpoint = await addressBook.LookupAsync("http://tinyurl.com/omhxu6l#-Rrs7LRrCE3bV8x58j1l4JUzAT3P2obKia73k3IFG9k");
                Assumes.NotNull(endpoint);
                chatroomWindow.AddMember("App author", endpoint);
            });
        }

        private Task SetEndpointAsync(OwnEndpoint endpoint, Uri addressBookEntry)
        {
            this.PostalService = new PostalService(new Channel(this.HttpClient, endpoint, this.MessageRelayService, this.CryptoSettings));
            this.PublicEndpointUrlTextBlock.Text = addressBookEntry.AbsoluteUri;
            this.OpenChatroom.IsEnabled = true;
            this.ChatWithAuthor.IsEnabled = true;
            return Task.CompletedTask;
        }

        private void PublicEndpointUrlTextBlock_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!string.IsNullOrEmpty(this.PublicEndpointUrlTextBlock.Text))
            {
                Clipboard.SetText(this.PublicEndpointUrlTextBlock.Text);
            }
        }

        [DataContract]
        private class EndpointAndAddressBookUri
        {
            public EndpointAndAddressBookUri(Uri addressBookUri, OwnEndpoint endpoint)
            {
                this.AddressBookUri = addressBookUri;
                this.Endpoint = endpoint;
            }

            [DataMember]
            public Uri AddressBookUri { get; }

            [DataMember]
            public OwnEndpoint Endpoint { get; }
        }
    }
}
