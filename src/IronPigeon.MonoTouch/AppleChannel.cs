namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Composition;
	using System.Linq;
	using System.Text;

	/// <summary>
	/// An Apple iOS implementation of <see cref="Channel"/>.
	/// </summary>
	[Export(typeof(Channel))]
	[Shared]
	public class AppleChannel : Channel {
	}
}
