namespace IronPigeon.Relay.Models {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Web;
	using Microsoft;
	using Microsoft.WindowsAzure.StorageClient;

	public class InboxContext : TableServiceContext {
		public InboxContext(CloudTableClient client, string tableName)
			: base(client.BaseUri.AbsoluteUri, client.Credentials) {
			Requires.NotNullOrEmpty(tableName, "tableName");
			this.TableName = tableName;
		}

		public string TableName { get; private set; }

		public CloudTableQuery<InboxEntity> Get(string rowKey) {
			return (from inbox in this.CreateQuery<InboxEntity>(this.TableName)
					where inbox.RowKey == rowKey
					select inbox).AsTableServiceQuery();
		}

		public void AddObject(InboxEntity entity) {
			this.AddObject(this.TableName, entity);
		}
	}
}