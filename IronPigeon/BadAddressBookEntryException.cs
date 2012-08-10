namespace IronPigeon {
	using System;

	/// <summary>
	/// An exception thrown when an error occurs while reading an address book entry.
	/// </summary>
	public class BadAddressBookEntryException : Exception {
		/// <summary>
		/// Initializes a new instance of the <see cref="BadAddressBookEntryException" /> class.
		/// </summary>
		public BadAddressBookEntryException() {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="BadAddressBookEntryException" /> class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		public BadAddressBookEntryException(string message)
			: base(message) {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="BadAddressBookEntryException" /> class.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="inner">The inner exception.</param>
		public BadAddressBookEntryException(string message, Exception inner)
			: base(message, inner) {
		}
	}
}
