namespace WpfChatroom {
	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.IO;
	using System.Linq;
	using System.Net.Http;
	using System.Reflection;
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
	using Autofac;
	using IronPigeon;
	using IronPigeon.Dart;
	using IronPigeon.Providers;
	using Microsoft.Win32;

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		private IContainer container;

		/// <summary>
		/// Initializes a new instance of the <see cref="MainWindow"/> class.
		/// </summary>
		public MainWindow() {
			this.InitializeComponent();

			var containerBuilder = new ContainerBuilder();
			containerBuilder.RegisterModule(new IronPigeonPlatformModule());
			containerBuilder.RegisterModule(new IronPigeonDartModule());
			containerBuilder.RegisterType<ChatroomWindow>()
				.AsSelf()
				.PropertiesAutowired();
			containerBuilder.RegisterInstance(this)
				.AsSelf()
				.PropertiesAutowired();
			this.container = containerBuilder.Build();
			this.container.Resolve<MainWindow>(); // satisfy imports

			this.MessageRelayService.BlobPostUrl = new Uri(ConfigurationManager.ConnectionStrings["RelayBlobService"].ConnectionString);
			this.MessageRelayService.InboxServiceUrl = new Uri(ConfigurationManager.ConnectionStrings["RelayInboxService"].ConnectionString);
		}

		/// <summary>
		/// Gets or sets the own endpoint services.
		/// </summary>
		public OwnEndpointServices OwnEndpointServices { get; set; }

		/// <summary>
		/// Gets or sets the message relay service.
		/// </summary>
		public RelayCloudBlobStorageProvider MessageRelayService { get; set; }

		/// <summary>
		/// Gets or sets the crypto provider.
		/// </summary>
		public ICryptoProvider CryptoProvider { get; set; }

		/// <summary>
		/// Gets or sets the channel.
		/// </summary>
		public Channel Channel { get; set; }

		private async void CreateNewEndpoint_OnClick(object sender, RoutedEventArgs e) {
			this.CreateNewEndpoint.IsEnabled = false;
			this.CreateNewEndpoint.Cursor = Cursors.AppStarting;
			try {
				var cts = new CancellationTokenSource();
				var endpointTask = this.OwnEndpointServices.CreateAsync(cts.Token);
				var dialog = new SaveFileDialog();
				bool? result = dialog.ShowDialog(this);
				if (result.HasValue && result.Value) {
					Uri addressBookEntry = await this.OwnEndpointServices.PublishAddressBookEntryAsync(await endpointTask, cts.Token);
					await this.SetEndpointAsync(await endpointTask, addressBookEntry, cts.Token);
					using (var stream = dialog.OpenFile()) {
						var writer = new BinaryWriter(stream, Encoding.UTF8);
						writer.SerializeDataContract(addressBookEntry);
						writer.Flush();
						await this.Channel.Endpoint.SaveAsync(stream, cts.Token);
					}
				} else {
					cts.Cancel();
				}
			} finally {
				this.CreateNewEndpoint.Cursor = Cursors.Arrow;
				this.CreateNewEndpoint.IsEnabled = true;
			}
		}

		private async void OpenOwnEndpoint_OnClick(object sender, RoutedEventArgs e) {
			this.OpenOwnEndpoint.IsEnabled = false;
			this.OpenOwnEndpoint.Cursor = Cursors.AppStarting;
			try {
				var dialog = new OpenFileDialog();
				bool? result = dialog.ShowDialog(this);
				if (result.HasValue && result.Value) {
					using (var fileStream = dialog.OpenFile()) {
						var reader = new BinaryReader(fileStream, Encoding.UTF8);
						var addressBookEntry = reader.DeserializeDataContract<Uri>();
						await this.SetEndpointAsync(await OwnEndpoint.OpenAsync(fileStream), addressBookEntry);
					}
				}
			} finally {
				this.OpenOwnEndpoint.Cursor = Cursors.Arrow;
				this.OpenOwnEndpoint.IsEnabled = true;
			}
		}

		private void OpenChatroom_OnClick(object sender, RoutedEventArgs e) {
			var chatroomWindow = this.container.Resolve<ChatroomWindow>();
			chatroomWindow.Show();
		}

		private async void ChatWithAuthor_OnClick(object sender, RoutedEventArgs e) {
			var chatroomWindow = this.container.Resolve<ChatroomWindow>();
			chatroomWindow.Show();

			var addressBook = new DirectEntryAddressBook(this.CryptoProvider, new HttpClient());
			var endpoint = await addressBook.LookupAsync("http://tinyurl.com/omhxu6l#-Rrs7LRrCE3bV8x58j1l4JUzAT3P2obKia73k3IFG9k");
			chatroomWindow.AddMember("App author", endpoint);
		}

		private Task SetEndpointAsync(OwnEndpoint endpoint, Uri addressBookEntry, CancellationToken cancellationToken = default(CancellationToken)) {
			this.Channel.Endpoint = endpoint;
			this.PublicEndpointUrlTextBlock.Text = addressBookEntry.AbsoluteUri;
			this.OpenChatroom.IsEnabled = true;
			this.ChatWithAuthor.IsEnabled = true;
			return Task.FromResult<object>(null);
		}

		private void PublicEndpointUrlTextBlock_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
			if (!string.IsNullOrEmpty(this.PublicEndpointUrlTextBlock.Text)) {
				Clipboard.SetText(this.PublicEndpointUrlTextBlock.Text);
			}
		}
	}
}
