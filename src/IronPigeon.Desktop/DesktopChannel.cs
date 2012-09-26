namespace IronPigeon {
	using System;
	using System.Collections.Generic;
#if NET40
	using System.ComponentModel.Composition;
#else
	using System.Composition;
#endif
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	/// <summary>
	/// A channel for sending or receiving secure messages with additional desktop specific functionality.
	/// </summary>
	[Export(typeof(Channel)), Export]
#if !NET40
	[Shared]
#endif
	public class DesktopChannel : Channel {
	}
}
