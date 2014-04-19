namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Composition;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	/// <summary>
	/// A channel for sending or receiving secure messages with additional desktop specific functionality.
	/// </summary>
	[Export(typeof(Channel))]
	[Shared]
	public class DesktopChannel : Channel {
	}
}
