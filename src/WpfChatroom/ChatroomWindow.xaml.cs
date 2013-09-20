namespace WpfChatroom {
	using System;
	using System.Collections.Generic;
	using System.Composition;
	using System.Linq;
	using System.Net.Http;
	using System.Text;
	using System.Threading.Tasks;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Data;
	using System.Windows.Documents;
	using System.Windows.Input;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using System.Windows.Shapes;

	using IronPigeon;
	using IronPigeon.Providers;

	/// <summary>
	/// Interaction logic for ChatroomWindow.xaml
	/// </summary>
	[Export]
	public partial class ChatroomWindow : Window {
		private Dictionary<string, Endpoint> members = new Dictionary<string, Endpoint>();

		/// <summary>
		/// Initializes a new instance of the <see cref="ChatroomWindow"/> class.
		/// </summary>
		public ChatroomWindow() {
			this.InitializeComponent();
		}

		/// <summary>
		/// Gets or sets the channel.
		/// </summary>
		[Import]
		public Channel Channel { get; set; }

		/// <summary>
		/// Gets or sets the crypto provider.
		/// </summary>
		[Import]
		public ICryptoProvider CryptoProvider { get; set; }

		internal async Task InvitingMemberAsync(InviteMember inviteWindow) {
			var addressBook = new DirectEntryAddressBook(this.CryptoProvider, new HttpClient());
			var endpoint = await addressBook.LookupAsync(inviteWindow.PublicEndpointUrlBox.Text);
			if (endpoint != null) {
				try {
					this.AddMember(inviteWindow.FriendlyNameBox.Text, endpoint);
				} catch (InvalidOperationException ex) {
					MessageBox.Show(ex.Message);
				}
			}

			inviteWindow.Close();
		}

		/// <summary>
		/// Raises the <see cref="E:System.Windows.FrameworkElement.Initialized" /> event. This method is invoked whenever <see cref="P:System.Windows.FrameworkElement.IsInitialized" /> is set to true internally.
		/// </summary>
		/// <param name="e">The <see cref="T:System.Windows.RoutedEventArgs" /> that contains the event data.</param>
		protected override async void OnInitialized(EventArgs e) {
			base.OnInitialized(e);

			await Task.Yield();
			this.AddMember("You", this.Channel.Endpoint.PublicEndpoint);
			await this.ReceiveMessageLoopAsync();
		}

		private void AddMember(string friendlyName, Endpoint endpoint) {
			if (this.members.Values.Contains(endpoint)) {
				throw new InvalidOperationException("That member is already in the chatroom.");
			}

			this.members.Add(friendlyName, endpoint);
			this.ChatroomMembersList.Items.Add(friendlyName);
		}

		private async Task ReceiveMessageLoopAsync() {
			TimeSpan delay = TimeSpan.Zero;
			while (this.IsLoaded) {
				try {
					await Task.Delay(delay);
					bool lastTimeFailed = delay > TimeSpan.Zero;
					delay = TimeSpan.Zero;
					var progress = new ProgressWithCompletion<Channel.PayloadReceipt>(m => this.ProcessReceivedMessagedAsync(m.Payload));
					await this.Channel.ReceiveAsync(longPoll: !lastTimeFailed, progress: progress);
					this.TopInfoBar.Visibility = Visibility.Collapsed;
				} catch (HttpRequestException) {
					// report the error eventually if it keeps happening.
					// sleep on it for a while.
					delay = TimeSpan.FromSeconds(5);
					this.TopInfoBar.Text = "Unable to receive messages. Will try again soon.";
					this.TopInfoBar.Visibility = Visibility.Visible;
				}
			}
		}

		private async Task ProcessReceivedMessagedAsync(Payload payload) {
			var message = Encoding.UTF8.GetString(payload.Content);
			this.History.Items.Add(message);
			await this.Channel.DeleteInboxItemAsync(payload);
		}

		private async void SendMessageButton_Click(object sender, RoutedEventArgs e) {
			this.AuthoredMessage.IsReadOnly = true;
			this.SendMessageButton.IsEnabled = false;
			try {
				if (this.AuthoredMessage.Text.Length > 0) {
					var payload = new Payload(Encoding.UTF8.GetBytes(this.AuthoredMessage.Text), "text/plain");
					await this.Channel.PostAsync(payload, this.members.Values.ToList(), DateTime.UtcNow + TimeSpan.FromDays(14));
				}

				this.BottomInfoBar.Visibility = Visibility.Collapsed;
				this.AuthoredMessage.Text = string.Empty;
			} catch (Exception ex) {
				this.BottomInfoBar.Text = "Unable to transmit message: " + ex.Message;
				this.BottomInfoBar.Visibility = Visibility.Visible;
			} finally {
				this.AuthoredMessage.IsReadOnly = false;
				this.SendMessageButton.IsEnabled = true;
			}
		}

		private void InviteButton_OnClick(object sender, RoutedEventArgs e) {
			var invite = new InviteMember(this);
			invite.Show();
		}
	}
}
