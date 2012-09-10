namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Runtime.Serialization;
	using System.Text;

	[DataContract]
	public class IncomingList {
		[DataMember]
		public List<IncomingItem> Items { get; set; }

		[DataContract]
		public class IncomingItem {
			[DataMember]
			public Uri Location { get; set; }

			[DataMember]
			public DateTime DatePostedUtc { get; set; }
		}
	}
}
