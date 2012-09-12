namespace IronPigeon.Dart {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	/// <summary>
	/// Helper methods useful to Dart.
	/// </summary>
	internal static class DartUtilities {
#if NET40
		/// <summary>
		/// Fills in for a method that only exists in .NET 4.5.
		/// </summary>
		/// <param name="type">The type to return.</param>
		/// <returns>The type passed in as a parameter.</returns>
		internal static Type GetTypeInfo(this Type type) {
			return type;
		}
#endif
	}
}
