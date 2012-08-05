namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Runtime.Serialization;
	using System.Text;
	using Microsoft;
	
	[DataContract]
	public class Message {
		public Message(byte[] content, string contentType) {
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
		/// serialized in the <see cref="Content property."/>
		/// </summary>
		[DataMember]
		public string ContentType { get; private set; }
	}
}
