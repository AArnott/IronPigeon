// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace WpfChatroom
{
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

    using Microsoft;
    using Microsoft.VisualStudio.Threading;

    /// <summary>
    /// Interaction logic for InviteMember.xaml.
    /// </summary>
    public partial class InviteMember : Window
    {
        private readonly ChatroomWindow chatroom;

        /// <summary>
        /// Initializes a new instance of the <see cref="InviteMember"/> class.
        /// </summary>
        /// <param name="chatroom">The chatroom.</param>
        public InviteMember(ChatroomWindow chatroom)
        {
            Requires.NotNull(chatroom, nameof(chatroom));

            this.chatroom = chatroom;
            this.InitializeComponent();
        }

        /// <inheritdoc cref="ChatroomWindow.JoinableTaskContext" />
        public JoinableTaskContext JoinableTaskContext => this.chatroom.JoinableTaskContext;

        private void InviteButton_OnClick(object sender, RoutedEventArgs e)
        {
            this.JoinableTaskContext.Factory.RunAsync(async delegate
            {
                await this.chatroom.InvitingMemberAsync(this);
            });
        }
    }
}
