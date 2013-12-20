namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Composition;
	using System.Text;

	/// <summary>
	/// An Android implementation of <see cref="Channel"/>.
	/// </summary>
	[Export(typeof(Channel))]
	[Shared]
	public class AndroidChannel : Channel {
	}
}
