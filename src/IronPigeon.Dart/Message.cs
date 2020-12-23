// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Dart
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Mime;
    using System.Runtime.Serialization;
    using IronPigeon.Relay;
    using Microsoft;

    /// <summary>
    /// A "Dart", or secure email message.
    /// </summary>
    [DataContract]
    public class Message : IEquatable<Message>
    {
        /// <summary>
        /// The content-type that identifies darts as the payload to an IronPigeon message.
        /// </summary>
        public static readonly ContentType ContentType = new ContentType("application/dart");

        /// <summary>
        /// Initializes a new instance of the <see cref="Message" /> class
        /// to represent an outgoing message.
        /// </summary>
        /// <param name="author">The author.</param>
        /// <param name="authorName">The author's name.</param>
        /// <param name="recipients">The recipients.</param>
        /// <param name="subject">The subject.</param>
        /// <param name="body">The body.</param>
        public Message(OwnEndpoint author, string authorName, IReadOnlyList<Endpoint> recipients, string subject, string body)
            : this(Requires.NotNull(author, nameof(author)).PublicEndpoint, authorName, recipients, subject, body)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Message" /> class
        /// to represent an incoming message.
        /// </summary>
        /// <param name="author">The author.</param>
        /// <param name="authorName">The author's name.</param>
        /// <param name="recipients">The recipients.</param>
        /// <param name="subject">The subject.</param>
        /// <param name="body">The body.</param>
        [MessagePack.SerializationConstructor]
        public Message(Endpoint author, string authorName, IReadOnlyList<Endpoint> recipients, string subject, string body)
        {
            Requires.NotNull(author, nameof(author));
            Requires.NotNull(authorName, nameof(authorName));
            Requires.NotNull(recipients, nameof(recipients));
            Requires.NotNullOrEmpty(subject, nameof(subject));
            Requires.NotNull(body, nameof(body));

            this.CreationDateUtc = DateTime.UtcNow;
            this.ExpirationUtc = DateTime.UtcNow + TimeSpan.FromDays(7);
            this.Author = author;
            this.AuthorName = authorName;
            this.Recipients.AddRange(recipients);
            this.Subject = subject;
            this.Body = body;
        }

        /// <summary>
        /// Gets or sets the creation date of this message.
        /// </summary>
        /// <value>The creation date in UTC time.</value>
        [DataMember(IsRequired = true)]
        public DateTime CreationDateUtc { get; set; }

        /// <summary>
        /// Gets or sets the date after which the sender no longer wishes to recommend receipt of this message.
        /// </summary>
        [DataMember]
        public DateTime ExpirationUtc { get; set; }

        /// <summary>
        /// Gets the author of this message.
        /// </summary>
        [DataMember]
        public Endpoint Author { get; }

        /// <summary>
        /// Gets or sets the author's self-proclaimed name.
        /// </summary>
        [DataMember]
        public string AuthorName { get; set; }

        /// <summary>
        /// Gets the list of recipients this message claims to have received this message.
        /// </summary>
        [DataMember]
        public List<Endpoint> Recipients { get; } = new List<Endpoint>();

        /// <summary>
        /// Gets the list of CC recipients the sender claims to be sending the message to.
        /// </summary>
        [DataMember]
        public List<Endpoint> CarbonCopyRecipients { get; } = new List<Endpoint>();

        /// <summary>
        /// Gets or sets the subject of this message.
        /// </summary>
        [DataMember]
        public string Subject { get; set; }

        /// <summary>
        /// Gets or sets the message this message was sent in reply to.
        /// </summary>
        /// <value>A reference to another message, or <c>null</c>.</value>
        [DataMember]
        public PayloadReference? InReplyTo { get; set; }

        /// <summary>
        /// Gets or sets the body of this message.
        /// </summary>
        [DataMember]
        public string? Body { get; set; }

        /// <summary>
        /// Gets a collection of attachments.
        /// </summary>
        [DataMember]
        public List<PayloadReference> Attachments { get; } = new List<PayloadReference>();

        /// <summary>
        /// Gets or sets the inbox item that pointed to this message.
        /// </summary>
        internal InboxItem? OriginatingInboxItem { get; set; }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        public bool Equals(Message? other)
        {
            return other is object
                && this.Author.Equals(other.Author)
                && this.Subject == other.Subject
                && this.Body == other.Body
                && this.CreationDateUtc == other.CreationDateUtc;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => this.Equals(obj as Message);

        /// <inheritdoc/>
        public override int GetHashCode() => unchecked(this.Author.GetHashCode() + this.Subject.GetHashCode() + this.Body.GetHashCode());
    }
}
