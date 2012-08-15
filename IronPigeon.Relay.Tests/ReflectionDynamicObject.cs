namespace IronPigeon.Relay.Tests {
	using System;
	using System.Collections.Generic;
	using System.Dynamic;
	using System.Linq;
	using System.Reflection;
	using System.Text;
	using System.Threading.Tasks;
	using Microsoft;

	internal class ReflectionDynamicObject : DynamicObject {
		private readonly object realObject;

		internal ReflectionDynamicObject(object realObject) {
			Requires.NotNull(realObject, "realObject");

			this.realObject = realObject;
		}

		public override bool TryGetMember(GetMemberBinder binder, out object result) {
			// Get the property value
			result = this.realObject.GetType().InvokeMember(
				binder.Name,
				BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
				null,
				this.realObject,
				null);

			// Always return true, since InvokeMember would have thrown if something went wrong
			return true;
		}
	}
}
