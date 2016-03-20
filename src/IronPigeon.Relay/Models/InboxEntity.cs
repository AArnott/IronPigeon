// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data.Services.Common;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Web;

    using Microsoft.WindowsAzure.StorageClient;

    public class InboxEntity : TableStorageEntity
    {
        private const string DefaultPartition = "Inbox";

        private const int CodeLength = 16;

        public InboxEntity()
        {
            this.PartitionKey = DefaultPartition;
        }

        /// <summary>
        /// Gets or sets the inbox owner code.
        /// </summary>
        public string InboxOwnerCode { get; set; }

        /// <summary>
        /// Gets or sets the Windows 8 application's package security identifier.
        /// </summary>
        public string ClientPackageSecurityIdentifier { get; set; }

        /// <summary>
        /// Gets or sets the URI of the Windows Notification Service to push to when a message arrives.
        /// </summary>
        public string PushChannelUri { get; set; }

        /// <summary>
        /// Gets or sets the content of the POST when sending push notifications.
        /// </summary>
        public string PushChannelContent { get; set; }

        /// <summary>
        /// Gets or sets the URI of the WinPhone8 notification service to push to when a message arrives.
        /// </summary>
        public string WinPhone8PushChannelUri { get; set; }

        /// <summary>
        /// Gets or sets the content of the POST when sending push notifications to WinPhone8.
        /// </summary>
        public string WinPhone8PushChannelContent { get; set; }

        /// <summary>
        /// Gets or sets the first line of text to include in the toast notification when a message comes in.
        /// </summary>
        public string WinPhone8ToastText1 { get; set; }

        /// <summary>
        /// Gets or sets the second line of text to include in the toast notification when a message comes in.
        /// </summary>
        public string WinPhone8ToastText2 { get; set; }

        /// <summary>
        /// Gets or sets the name of the Windows Phone 8 tile template in use by the client.
        /// </summary>
        /// <value>A template name. Or <c>null</c> to indicate the default of "FlipTile".</value>
        public string WinPhone8TileTemplate { get; set; }

        /// <summary>
        /// Gets or sets the Google Cloud Messaging registration identifier.
        /// </summary>
        /// <value>
        /// The google cloud messaging registration identifier.
        /// </value>
        public string GoogleCloudMessagingRegistrationId { get; set; }

        /// <summary>
        /// Gets or sets the device token from an iOS device that should receive push notification.
        /// </summary>
        public string ApplePushNotificationGatewayDeviceToken { get; set; }

        /// <summary>
        /// Gets or sets the timestamp for when the inbox was last accessed by its owner.
        /// </summary>
        public DateTime? LastAuthenticatedInteractionUtc { get; set; }

        /// <summary>
        /// Gets or sets the timestamp for when the last push notification was sent to a Windows 8 client.
        /// </summary>
        public DateTime? LastWindows8PushNotificationUtc { get; set; }

        /// <summary>
        /// Gets or sets the timestamp for when the last push notification was sent to a Windows Phone 8 client.
        /// </summary>
        public DateTime? LastWinPhone8PushNotificationUtc { get; set; }

        /// <summary>
        /// Gets a value indicating whether push notification is enabled.
        /// </summary>
        internal bool IsPushNotificationEnabled
        {
            get { return this.PushChannelUri != null || this.WinPhone8PushChannelUri != null; }
        }

        public static InboxEntity Create()
        {
            var entity = new InboxEntity();

            var rng = RNGCryptoServiceProvider.Create();
            var inboxOwnerCode = new byte[CodeLength];
            rng.GetBytes(inboxOwnerCode);
            entity.InboxOwnerCode = Utilities.ToBase64WebSafe(inboxOwnerCode);
            entity.RowKey = Guid.NewGuid().ToString();

            return entity;
        }
    }
}