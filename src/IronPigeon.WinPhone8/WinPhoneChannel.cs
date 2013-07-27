namespace IronPigeon.WinPhone8 {
	using System;
	using System.Collections.Generic;
	using System.Composition;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	[Export(typeof(Channel))]
	[Shared]
	public class WinPhoneChannel : Channel {
	}
}
