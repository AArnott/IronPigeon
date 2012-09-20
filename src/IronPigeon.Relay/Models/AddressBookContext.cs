namespace IronPigeon.Relay.Models {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Web;
	using Microsoft.WindowsAzure.StorageClient;
	using Validation;

	public class AddressBookContext : TableServiceContext {
		public AddressBookContext(CloudTableClient client, string tableName)
			: base(client.BaseUri.AbsoluteUri, client.Credentials) {
			Requires.NotNullOrEmpty(tableName, "tableName");
			this.TableName = tableName;
		}

		public string TableName { get; private set; }

		public async Task<AddressBookEntity> GetAsync(string provider, string userId) {
			Requires.NotNullOrEmpty(provider, "provider");
			Requires.NotNullOrEmpty(userId, "userId");

			var query = this.GetQuery(AddressBookEntity.ConstructRowKey(provider, userId));
			var result = await query.ExecuteAsync();
			return result.FirstOrDefault();
		}

		public void AddObject(AddressBookEntity entity) {
			this.AddObject(this.TableName, entity);
		}

		private CloudTableQuery<AddressBookEntity> GetQuery(string rowKey) {
			return (from inbox in this.CreateQuery<AddressBookEntity>(this.TableName)
					where inbox.RowKey == rowKey
					select inbox).AsTableServiceQuery();
		}
	}
}