// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace WpfChatroom
{
    using System;
    using System.Collections.Generic;
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
    using IronPigeon.Dart;
    using IronPigeon.Providers;
    using Microsoft;
    using Microsoft.VisualStudio.Threading;

    /// <summary>
    /// Interaction logic for ChatroomWindow.xaml.
    /// </summary>
    public partial class ChatroomWindow : Window
    {
        private readonly MainWindow mainWindow;

        private readonly Dictionary<string, Endpoint> members = new Dictionary<string, Endpoint>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ChatroomWindow" /> class.
        /// </summary>
        /// <param name="mainWindow">The main window.</param>
        internal ChatroomWindow(MainWindow mainWindow)
        {
            Requires.Argument(mainWindow.PostalService is object, nameof(mainWindow), "{0} property must be set.", nameof(MainWindow.PostalService));

            this.InitializeComponent();
            this.mainWindow = mainWindow;
            this.PostalService = mainWindow.PostalService;
        }

        /// <inheritdoc cref="MainWindow.JoinableTaskContext" />
        public JoinableTaskContext JoinableTaskContext => this.mainWindow.JoinableTaskContext;

        /// <summary>
        /// Gets the channel.
        /// </summary>
        public PostalService PostalService { get; }

        /// <summary>
        /// Adds an endpoint to the conversation.
        /// </summary>
        /// <param name="friendlyName">The name to display for messages from this endpoint.</param>
        /// <param name="endpoint">The endpoint to add.</param>
        internal void AddMember(string friendlyName, Endpoint endpoint)
        {
            if (this.members.Values.Contains(endpoint))
            {
                throw new InvalidOperationException("That member is already in the chatroom.");
            }

            this.members.Add(friendlyName, endpoint);
            this.ChatroomMembersList.Items.Add(friendlyName);
        }

        /// <summary>
        /// Invites someone to the conversation.
        /// </summary>
        /// <param name="inviteWindow">The window that was used to invite the endpoint.</param>
        /// <returns>A task that completes when the operation is done.</returns>
        internal async Task InvitingMemberAsync(InviteMember inviteWindow)
        {
            var addressBook = new DirectEntryAddressBook(this.PostalService.Channel.HttpClient);
            Endpoint? endpoint = await addressBook.LookupAsync(inviteWindow.PublicEndpointUrlBox.Text);
            if (endpoint != null)
            {
                try
                {
                    this.AddMember(inviteWindow.FriendlyNameBox.Text, endpoint);
                }
                catch (InvalidOperationException ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }

            inviteWindow.Close();
        }

        /// <summary>
        /// Raises the <see cref="System.Windows.FrameworkElement.Initialized" /> event. This method is invoked whenever <see cref="System.Windows.FrameworkElement.IsInitialized" /> is set to true internally.
        /// </summary>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> that contains the event data.</param>
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            this.JoinableTaskContext.Factory.RunAsync(async delegate
            {
                await Task.Yield();
                this.AddMember("You", this.PostalService.Channel.Endpoint.PublicEndpoint);
                await this.ReceiveMessageLoopAsync();
            });
        }

        private async Task ReceiveMessageLoopAsync()
        {
            TimeSpan delay = TimeSpan.Zero;
            while (this.IsLoaded)
            {
                try
                {
                    await Task.Delay(delay);
                    bool lastTimeFailed = delay > TimeSpan.Zero;
                    delay = TimeSpan.Zero;

                    await foreach (Message message in this.PostalService.ReceiveAsync(longPoll: !lastTimeFailed))
                    {
                        this.History.Items.Add(message.Body);
                        await this.PostalService.DeleteAsync(message);
                    }

                    this.TopInfoBar.Visibility = Visibility.Collapsed;
                }
                catch (HttpRequestException)
                {
                    // report the error eventually if it keeps happening.
                    // sleep on it for a while.
                    delay = TimeSpan.FromSeconds(5);
                    this.TopInfoBar.Text = "Unable to receive messages. Will try again soon.";
                    this.TopInfoBar.Visibility = Visibility.Visible;
                }
            }
        }

        private void SendMessageButton_Click(object sender, RoutedEventArgs e)
        {
            this.JoinableTaskContext.Factory.RunAsync(async delegate
            {
                this.AuthoredMessage.IsReadOnly = true;
                this.SendMessageButton.IsEnabled = false;
                try
                {
                    if (this.AuthoredMessage.Text.Length > 0)
                    {
                        var message = new Message(this.PostalService.Channel.Endpoint, "Author", this.members.Values.ToList(), "message", this.AuthoredMessage.Text)
                        {
                            ExpirationUtc = DateTime.UtcNow + TimeSpan.FromDays(14),
                            AuthorName = "WpfChatroom user",
                        };
                        await this.PostalService.PostAsync(message);
                    }

                    this.BottomInfoBar.Visibility = Visibility.Collapsed;
                    this.AuthoredMessage.Text = string.Empty;
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    this.BottomInfoBar.Text = "Unable to transmit message: " + ex.Message;
                    this.BottomInfoBar.Visibility = Visibility.Visible;
                }
                finally
                {
                    this.AuthoredMessage.IsReadOnly = false;
                    this.SendMessageButton.IsEnabled = true;
                }
            });
        }

        private void InviteButton_OnClick(object sender, RoutedEventArgs e)
        {
            var invite = new InviteMember(this);
            invite.Show();
        }
    }
}
