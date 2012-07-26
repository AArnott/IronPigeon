namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	public class Channel {
		public Task PostAsync(Stream content, DateTime expiresUtc, string contentType, Contact[] recipients) {
			Requires.NotNull(content, "content");
			Requires.True(expiresUtc.Kind == DateTimeKind.Utc, "expiresUtc", Strings.UTCTimeRequired);
			Requires.NotNullOrEmpty(contentType, "contentType");
			Requires.NotNullOrEmpty(recipients, "recipients");

			throw new NotImplementedException();
		}
	}
}
