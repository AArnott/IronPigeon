namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	public class InvalidMessageException : Exception {
		public InvalidMessageException() : this(Strings.InvalidMessage) { }
		public InvalidMessageException(string message) : base(message) { }
		public InvalidMessageException(string message, Exception inner) : base(message, inner) { }
	}
}
