namespace IronPigeon.Dart{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Runtime.Serialization;
	using System.Text;
	using System.Threading.Tasks;

	using Microsoft;
#if NET40
	using ReadOnlyListOfEndpoint = System.Collections.ObjectModel.ReadOnlyCollection<Endpoint>;
#else
	using ReadOnlyListOfEndpoint = System.Collections.Generic.IReadOnlyList<Endpoint>;
#endif

	[DataContract]
	public class Message {
		public Message() {
			this.CreationDateUtc = DateTime.UtcNow;
		}

		public Message(OwnEndpoint author, ReadOnlyListOfEndpoint recipients, string subject, string body)
			: this(author.PublicEndpoint, recipients, subject, body) {
		}

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
		/// Gets the author of this message.
		/// </summary>
		[DataMember]
		public Endpoint Author { get; set; }

		/// <summary>
		/// Gets the set of recipients this message claims to have received this message.
		/// </summary>
		[DataMember]
		public Endpoint[] Recipients { get; set; }

		/// <summary>
		/// Gets or sets the set of CC recipients the sender claims to be sending the message to.
		/// </summary>
		[DataMember]
		public Endpoint[] CarbonCopyRecipients { get; set; }

		/// <summary>
		/// Gets the subject of this message.
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
		/// Gets the body of this message.
		/// </summary>
		[DataMember]
		public string Body { get; set; }

		/// <summary>
		/// Gets or sets the attachments.
		/// </summary>
		/// <value>The attachments.</value>
		[DataMember]
		public PayloadReference[] Attachments { get; set; }
	}
}
