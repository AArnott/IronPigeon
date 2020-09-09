// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using MessagePack;
    using Microsoft;

    /// <summary>
    /// The result of posting a message notification to a cloud inbox.
    /// </summary>
    public class NotificationPostedReceipt
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationPostedReceipt"/> class.
        /// </summary>
        /// <param name="recipient">The inbox that received the notification.</param>
        public NotificationPostedReceipt(Endpoint recipient)
        {
            Requires.NotNull(recipient, nameof(recipient));

            this.Recipient = recipient;
        }

        /// <summary>
        /// Gets the receiver of the notification.
        /// </summary>
        public Endpoint Recipient { get; private set; }
    }
}
