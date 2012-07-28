namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	public interface ILogger {
		void WriteLine(string message, byte[] buffer);
	}
}
