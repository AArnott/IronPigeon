namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Runtime.Serialization;
	using System.Text;

	[DataContract]
	public class InboxList {
		[DataMember]
		public List<InboxItem> Items { get; set; }

		[DataContract]
		public class InboxItem {
			[DataMember]
			public Uri Location { get; set; }

			[DataMember]
			public DateTime DatePostedUtc { get; set; }
		}
	}
}
