namespace IronPigeon.Relay.Models {
	using System;
	using System.Collections.Generic;
	using System.Data.Services.Common;
	using System.Linq;
	using System.Web;

	/// <summary>
	/// A base class for Azure Table storage entities.
	/// </summary>
	[DataServiceKey("PartitionKey", "RowKey")]
	public abstract class TableStorageEntity {
		/// <summary>
		/// Initializes a new instance of the <see cref="TableStorageEntity" /> class.
		/// </summary>
		protected TableStorageEntity() {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TableStorageEntity" /> class.
		/// </summary>
		/// <param name="partitionKey">The partition key.</param>
		/// <param name="rowKey">The row key.</param>
		protected TableStorageEntity(string partitionKey, string rowKey) {
			this.PartitionKey = partitionKey;
			this.RowKey = rowKey;
		}

		/// <summary>
		/// Gets or sets the partition key.
		/// </summary>
		/// <value>
		/// The partition key.
		/// </value>
		public string PartitionKey { get; set; }

		/// <summary>
		/// Gets or sets the row key.
		/// </summary>
		/// <value>
		/// The row key.
		/// </value>
		public string RowKey { get; set; }

		/// <summary>
		/// Gets or sets the timestamp.
		/// </summary>
		/// <value>
		/// The timestamp.
		/// </value>
		public DateTime Timestamp { get; set; }
	}
}