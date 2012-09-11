namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	/// <summary>
	/// An interface that receives log messages.
	/// </summary>
	public interface ILogger {
		/// <summary>
		/// Receives a message and a buffer.
		/// </summary>
		/// <param name="message">The message.</param>
		/// <param name="buffer">The buffer.</param>
		void WriteLine(string message, byte[] buffer);
	}
}
