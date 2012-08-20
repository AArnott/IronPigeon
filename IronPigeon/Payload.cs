namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Runtime.Serialization;
	using System.Text;
	using Microsoft;

	/// <summary>
	/// The payload in a securely transmitted message, before encryptions and signatures are applied
	/// or after they are removed.
	/// </summary>
	[DataContract]
	public class Payload {
		/// <summary>
		/// Initializes a new instance of the <see cref="Payload" /> class.
		/// </summary>
		/// <param name="content">The content.</param>
		/// <param name="contentType">Type of the content.</param>
		public Payload(byte[] content, string contentType) {
			Requires.NotNull(content, "content");
			Requires.NotNullOrEmpty(contentType, "contentType");

			this.Content = content;
			this.ContentType = contentType;
		}

		/// <summary>
		/// Gets the blob that constitutes the payload.
		/// </summary>
		[DataMember]
		public byte[] Content { get; private set; }

		/// <summary>
		/// Gets the content-type that describes the type of data that is
		/// serialized in the <see cref="Content"/> property.
		/// </summary>
		[DataMember]
		public string ContentType { get; private set; }

		/// <summary>
		/// Gets or sets the location of the payload reference that led to the discovery of this payload.
		/// </summary>
		internal Uri PayloadReferenceUri { get; set; }
	}
}
