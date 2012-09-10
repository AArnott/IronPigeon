namespace IronPigeon.Dart {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	internal static class DartUtilities {
#if NET40
		internal static Type GetTypeInfo(this Type type) {
			return type;
		}
#endif
	}
}
