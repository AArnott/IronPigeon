namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	public class Channel {
		public Task PostAsync(Message message, IReadOnlyList<Contact> recipients) {
			Requires.NotNull(message, "message");
			Requires.NotNullOrEmpty(recipients, "recipients");

			throw new NotImplementedException();
		}

		public Task<IReadOnlyList<Message>> ReceiveAsync(IProgress<Message> progress = null) {
			throw new NotImplementedException();
		}
	}
}
