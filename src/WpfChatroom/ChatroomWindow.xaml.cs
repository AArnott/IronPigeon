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
		/// Gets or sets the channel.
		/// </summary>
		[Import]
		public Channel Channel { get; set; }

		/// <summary>
		/// Gets or sets the crypto provider.
		/// </summary>
		[Import]
		public ICryptoProvider CryptoProvider { get; set; }

		public ChatroomWindow() {
			InitializeComponent();
		}

		internal async Task InvitingMemberAsync(InviteMember inviteWindow) {
			var addressBook = new DirectEntryAddressBook(this.CryptoProvider, new HttpClient());
			var endpoint = await addressBook.LookupAsync(inviteWindow.PublicEndpointUrlBox.Text);
			if (endpoint != null) {
				this.AddMember(inviteWindow.FriendlyNameBox.Text, endpoint);
			}

			inviteWindow.Close();
		}

		private void AddMember(string friendlyName, Endpoint endpoint) {
			this.members.Add(friendlyName, endpoint);
			this.ChatroomMembersList.Items.Add(friendlyName);
		}

		protected override async void OnInitialized(EventArgs e) {
			base.OnInitialized(e);

			await Task.Yield();
			this.AddMember("You", this.Channel.Endpoint.PublicEndpoint);
			await this.ReceiveMessageLoopAsync();
		}

		private async Task ReceiveMessageLoopAsync() {
			TimeSpan delay = TimeSpan.Zero;
			while (this.IsLoaded) {
				try {
					await Task.Delay(delay);
					bool lastTimeFailed = delay > TimeSpan.Zero;
					delay = TimeSpan.Zero;
					var progress = new ProgressWithCompletion<Payload>(m => this.ProcessReceivedMessagedAsync(m));
					await this.Channel.ReceiveAsync(longPoll: !lastTimeFailed);
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

		private Task ProcessReceivedMessagedAsync(Payload payload) {
			var message = Encoding.UTF8.GetString(payload.Content);
			this.History.Items.Add(message);
			this.Channel.DeleteInboxItemAsync(payload);
			return Task.FromResult<object>(null);
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
