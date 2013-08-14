namespace WpfChatroom {
	using System;
	using System.Collections.Generic;
	using System.Composition;
	using System.Composition.Hosting;
	using System.Configuration;
	using System.Linq;
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
	using IronPigeon;
	using IronPigeon.Providers;
	using Microsoft.Win32;

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		/// <summary>
		/// Gets or sets the own endpoint services.
		/// </summary>
		[Import]
		public OwnEndpointServices OwnEndpointServices { get; set; }

		/// <summary>
		/// Gets or sets the message relay service.
		/// </summary>
		[Import]
		public RelayCloudBlobStorageProvider MessageRelayService { get; set; }

		/// <summary>
		/// Gets or sets the crypto provider.
		/// </summary>
		[Import]
		public ICryptoProvider CryptoProvider { get; set; }

		[Import]
		public ExportFactory<ChatroomWindow> ChatroomWindowFactory { get; set; }

		/// <summary>
		/// Gets or sets the channel.
		/// </summary>
		[Import]
		public Channel Channel { get; set; }

		public MainWindow() {
			InitializeComponent();

			var configuration =
				new ContainerConfiguration().WithAssembly(typeof(Channel).Assembly)
											.WithPart(typeof(DesktopCryptoProvider))
											.WithPart(typeof(DesktopChannel))
											.WithAssembly(Assembly.GetExecutingAssembly());
			var container = configuration.CreateContainer();
			container.SatisfyImports(this);

			this.MessageRelayService.BlobPostUrl = new Uri(ConfigurationManager.ConnectionStrings["RelayBlobService"].ConnectionString);
			this.MessageRelayService.InboxServiceUrl = new Uri(ConfigurationManager.ConnectionStrings["RelayInboxService"].ConnectionString);
			this.CryptoProvider.ApplySecurityLevel(SecurityLevel.Minimum);
		}

		private async void CreateNewEndpoint_OnClick(object sender, RoutedEventArgs e) {
			this.CreateNewEndpoint.IsEnabled = false;
			this.CreateNewEndpoint.Cursor = Cursors.AppStarting;
			try {
				var cts = new CancellationTokenSource();
				var endpointTask = this.OwnEndpointServices.CreateAsync(cts.Token);
				var dialog = new SaveFileDialog();
				bool? result = dialog.ShowDialog(this);
				if (result.HasValue && result.Value) {
					await this.SetEndpointAsync(await endpointTask, cts.Token);
					using (var stream = dialog.OpenFile()) {
						this.Channel.Endpoint.SaveAsync(stream, cts.Token);
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
						await this.SetEndpointAsync(await OwnEndpoint.OpenAsync(fileStream));
					}
				}
			} finally {
				this.OpenOwnEndpoint.Cursor = Cursors.Arrow;
				this.OpenOwnEndpoint.IsEnabled = true;
			}
		}

		private void OpenChatroom_OnClick(object sender, RoutedEventArgs e) {
			var chatroomWindow = this.ChatroomWindowFactory.CreateExport();
			chatroomWindow.Value.Show();
		}

		private async Task SetEndpointAsync(OwnEndpoint endpoint, CancellationToken cancellationToken = default(CancellationToken)) {
			Uri addressBookEntry = await this.OwnEndpointServices.PublishAddressBookEntryAsync(endpoint, cancellationToken);
			this.Channel.Endpoint = endpoint;
			this.PublicEndpointUrlTextBlock.Text = addressBookEntry.AbsoluteUri;
			this.OpenChatroom.IsEnabled = true;
		}

		private void PublicEndpointUrlTextBlock_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
			if (!string.IsNullOrEmpty(this.PublicEndpointUrlTextBlock.Text)) {
				Clipboard.SetText(this.PublicEndpointUrlTextBlock.Text);
			}
		}
	}
}
