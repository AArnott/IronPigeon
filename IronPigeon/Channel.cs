namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	public class Channel {
		public Task PostAsync(Message message, IReadOnlyList<Contact> recipients, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(message, "message");
			Requires.NotNullOrEmpty(recipients, "recipients");

			throw new NotImplementedException();
		}

		public Task<IReadOnlyList<Message>> ReceiveAsync(IProgress<Message> progress = null, CancellationToken cancellationToken = default(CancellationToken)) {
			throw new NotImplementedException();
		}
	}
}
