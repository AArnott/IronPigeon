namespace IronPigeon.Dart {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Runtime.Serialization;
	using System.Text;
	using System.Threading.Tasks;

	using Validation;
#if NET40
	using ReadOnlyListOfEndpoint = System.Collections.ObjectModel.ReadOnlyCollection<Endpoint>;
#else
	using ReadOnlyListOfEndpoint = System.Collections.Generic.IReadOnlyList<Endpoint>;
#endif

	/// <summary>
	/// A "Dart", or secure email message.
	/// </summary>
	[DataContract]
	public class Message {
		/// <summary>
		/// The content-type that identifies darts as the payload to an IronPigeon message.
		/// </summary>
		public const string ContentType = "application/dart";

		/// <summary>
		/// Initializes a new instance of the <see cref="Message" /> class.
		/// </summary>
		public Message() {
			this.CreationDateUtc = DateTime.UtcNow;
			this.ExpirationUtc = DateTime.UtcNow + TimeSpan.FromDays(7);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Message" /> class.
		/// </summary>
		/// <param name="author">The author.</param>
		/// <param name="recipients">The recipients.</param>
		/// <param name="subject">The subject.</param>
		/// <param name="body">The body.</param>
		public Message(OwnEndpoint author, ReadOnlyListOfEndpoint recipients, string subject, string body)
			: this(author.PublicEndpoint, recipients, subject, body) {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Message" /> class.
		/// </summary>
		/// <param name="author">The author.</param>
		/// <param name="recipients">The recipients.</param>
		/// <param name="subject">The subject.</param>
		/// <param name="body">The body.</param>
		internal Message(Endpoint author, ReadOnlyListOfEndpoint recipients, string subject, string body)
			: this() {
			Requires.NotNull(author, "author");
			Requires.NotNull(recipients, "recipients");
			Requires.NotNullOrEmpty(subject, "subject");
			Requires.NotNull(body, "body");

			this.Author = author;
			this.Recipients = recipients.ToArray();
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
		/// Gets or sets the author of this message.
		/// </summary>
		[DataMember]
		public Endpoint Author { get; set; }

		/// <summary>
		/// Gets or sets the set of recipients this message claims to have received this message.
		/// </summary>
		[DataMember]
		public Endpoint[] Recipients { get; set; }

		/// <summary>
		/// Gets or sets the set of CC recipients the sender claims to be sending the message to.
		/// </summary>
		[DataMember]
		public Endpoint[] CarbonCopyRecipients { get; set; }

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
		public PayloadReference InReplyTo { get; set; }

		/// <summary>
		/// Gets or sets the body of this message.
		/// </summary>
		[DataMember]
		public string Body { get; set; }

		/// <summary>
		/// Gets or sets the attachments.
		/// </summary>
		/// <value>The attachments.</value>
		[DataMember]
		public PayloadReference[] Attachments { get; set; }

		/// <summary>
		/// Gets or sets the date after which the sender no longer wishes to recommend receipt of this message.
		/// </summary>
		[DataMember]
		public DateTime ExpirationUtc { get; set; }

		/// <summary>
		/// Gets or sets the originating payload.
		/// </summary>
		internal Payload OriginatingPayload { get; set; }
	}
}
