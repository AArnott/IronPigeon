namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Linq;
	using System.Text;

	/// <summary>
	/// Argument validation checks that throw some kind of ArgumentException when they fail (unless otherwise noted).
	/// </summary>
	internal static class Requires {
		/// <summary>
		/// Validates that a given parameter is not null.
		/// </summary>
		/// <typeparam name="T">The type of the parameter</typeparam>
		/// <param name="value">The value.</param>
		/// <param name="parameterName">Name of the parameter.</param>
		[DebuggerStepThrough]
		public static void NotNull<T>(T value, string parameterName) where T : class {
			if (value == null) {
				throw new ArgumentNullException(parameterName);
			}
		}

		/// <summary>
		/// Validates that a parameter is not null or empty.
		/// </summary>
		/// <param name="value">The value.</param>
		/// <param name="parameterName">Name of the parameter.</param>
		[DebuggerStepThrough]
		public static void NotNullOrEmpty(string value, string parameterName) {
			NotNull(value, parameterName);
			True(value.Length > 0, parameterName, Strings.EmptyStringNotAllowed);
		}

		/// <summary>
		/// Validates that an array is not null or empty.
		/// </summary>
		/// <typeparam name="T">The type of the elements in the sequence.</typeparam>
		/// <param name="value">The value.</param>
		/// <param name="parameterName">Name of the parameter.</param>
		[DebuggerStepThrough]
		public static void NotNullOrEmpty<T>(IEnumerable<T> value, string parameterName) {
			NotNull(value, parameterName);
			True(value.Any(), parameterName, Strings.InvalidArgument);
		}

		/// <summary>
		/// Validates some expression describing the acceptable condition for an argument evaluates to true.
		/// </summary>
		/// <param name="condition">The expression that must evaluate to true to avoid an <see cref="ArgumentException"/>.</param>
		/// <param name="parameterName">Name of the parameter.</param>
		/// <param name="unformattedMessage">The unformatted message.</param>
		/// <param name="args">Formatting arguments.</param>
		[DebuggerStepThrough]
		public static void True(bool condition, string parameterName, string unformattedMessage, params object[] args) {
			if (!condition) {
				throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, unformattedMessage, args), parameterName);
			}
		}
	}
}
