namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Runtime.Serialization;
	using System.Text;

	/// <summary>
	/// The response from a message relay service to the query for new incoming messages.
	/// </summary>
	[DataContract]
	public class IncomingList {
		/// <summary>
		/// Gets or sets the list of incoming items.
		/// </summary>
		[DataMember]
		public List<IncomingItem> Items { get; set; }

		/// <summary>
		/// Describes an individual incoming message.
		/// </summary>
		[DataContract]
		public class IncomingItem {
			/// <summary>
			/// Gets or sets the location from which the incoming <see cref="PayloadReference"/> may be downloaded.
			/// </summary>
			[DataMember]
			public Uri Location { get; set; }

			/// <summary>
			/// Gets or sets the date that this item was posted to this inbox.
			/// </summary>
			/// <value>
			/// A DateTime value in UTC.
			/// </value>
			[DataMember]
			public DateTime DatePostedUtc { get; set; }
		}
	}
}
