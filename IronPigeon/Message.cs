namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;

	public class Message {
		public Message(Stream content, DateTime expiresUtc, string contentType) {
			Requires.NotNull(content, "content");
			Requires.True(expiresUtc.Kind == DateTimeKind.Utc, "expiresUtc", Strings.UTCTimeRequired);
			Requires.NotNullOrEmpty(contentType, "contentType");
		}

		public Stream Content { get; private set; }

		public DateTime ExpiresUtc { get; private set; }

		public string ContentType { get; private set; }
	}
}
