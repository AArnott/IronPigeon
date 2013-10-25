namespace IronPigeon.Relay.Models {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Web;
	using Microsoft.WindowsAzure.Storage.Table;
	using Microsoft.WindowsAzure.Storage.Table.DataServices;
	using Microsoft.WindowsAzure.StorageClient;
	using Validation;

	public class PushNotificationContext : TableServiceContext {
		public PushNotificationContext(CloudTableClient client, string tableName)
			: base(client) {
			Requires.NotNullOrEmpty(tableName, "tableName");
			this.TableName = tableName;
		}

		public string TableName { get; private set; }

		public virtual async Task<PushNotificationClientEntity> GetAsync(string clientPackageSecurityIdentifier) {
			Requires.NotNullOrEmpty(clientPackageSecurityIdentifier, "clientPackageSecurityIdentifier");
			Requires.Argument(clientPackageSecurityIdentifier == null || clientPackageSecurityIdentifier.StartsWith(PushNotificationClientEntity.SchemePrefix), "clientPackageSecurityIdentifier", "Prefix {0} not found", PushNotificationClientEntity.SchemePrefix);

			var query = this.GetQuery(clientPackageSecurityIdentifier.Substring(PushNotificationClientEntity.SchemePrefix.Length));
			var result = await query.ExecuteSegmentedAsync();
			return result.FirstOrDefault();
		}

		public void AddObject(PushNotificationClientEntity entity) {
			this.AddObject(this.TableName, entity);
		}

		private TableServiceQuery<PushNotificationClientEntity> GetQuery(string rowKey) {
			Requires.NotNullOrEmpty(rowKey, "rowKey");

			return (from inbox in this.CreateQuery<PushNotificationClientEntity>(this.TableName)
					where inbox.RowKey == rowKey
					select inbox).AsTableServiceQuery(this);
		}
	}
}