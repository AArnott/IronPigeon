namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	public class BadAddressBookEntryException : Exception {
		public BadAddressBookEntryException() { }
		public BadAddressBookEntryException(string message) : base(message) { }
		public BadAddressBookEntryException(string message, Exception inner) : base(message, inner) { }
	}
}
