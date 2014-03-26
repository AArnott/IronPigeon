namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using PCLCrypto;
	using Validation;

	/// <summary>
	/// Security level options.
	/// </summary>
	public enum SecurityLevel {
		/// <summary>
		/// Minimum security level. Useful for unit testing.
		/// </summary>
		Minimum,

		/// <summary>
		/// It can't get much higher than this while retaining sanity.
		/// </summary>
		Maximum,
	}
}
