namespace IronPigeon.Tests {
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using Validation;

	/// <summary>
	/// Unit test validation helpers.
	/// </summary>
	public static class AssertEx {
		/// <summary>
		/// Verifies that a delegate throws the specified exception.
		/// </summary>
		/// <typeparam name="TException">The exception that should be thrown.</typeparam>
		/// <param name="action">The delegate whose execution should result in a thrown exception.</param>
		public static void Throws<TException>(Action action)
			where TException : Exception {
			Requires.NotNull(action, "action");

			try {
				action();
				throw new Exception(string.Format(CultureInfo.CurrentCulture, "Unexpected {0} not thrown.", typeof(TException).FullName));
			} catch (TException) {
			}
		}
	}
}
