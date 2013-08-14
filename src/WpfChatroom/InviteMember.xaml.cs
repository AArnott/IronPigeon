namespace WpfChatroom {
	using System;
	using System.Collections.Generic;
	using System.Linq;
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

	using Validation;

	/// <summary>
	/// Interaction logic for InviteMember.xaml
	/// </summary>
	public partial class InviteMember : Window {
		private readonly ChatroomWindow chatroom;

		public InviteMember(ChatroomWindow chatroom) {
			Requires.NotNull(chatroom, "chatroom");

			this.chatroom = chatroom;
			InitializeComponent();
		}

		private async void InviteButton_OnClick(object sender, RoutedEventArgs e) {
			await this.chatroom.InvitingMemberAsync(this);
		}
	}
}
