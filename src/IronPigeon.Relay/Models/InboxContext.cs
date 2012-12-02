namespace IronPigeon.Relay.Models {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Web;
	using Microsoft.WindowsAzure.Storage.Table;
	using Microsoft.WindowsAzure.Storage.Table.DataServices;
	using Microsoft.WindowsAzure.StorageClient;
	using Validation;

	public class InboxContext : TableServiceContext {
		public InboxContext(CloudTableClient client, string tableName)
			: base(client) {
			Requires.NotNullOrEmpty(tableName, "tableName");
			this.TableName = tableName;
		}

		public string TableName { get; private set; }

		public TableServiceQuery<InboxEntity> Get(string rowKey) {
			return (from inbox in this.CreateQuery<InboxEntity>(this.TableName)
					where inbox.RowKey == rowKey
					select inbox).AsTableServiceQuery(this);
		}

		public void AddObject(InboxEntity entity) {
			this.AddObject(this.TableName, entity);
		}
	}
}