namespace IronPigeon.Relay.Models {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Web;
	using Validation;
	using Microsoft.WindowsAzure.StorageClient;

	public class PushNotificationContext : TableServiceContext {
		public PushNotificationContext(CloudTableClient client, string tableName)
			: base(client.BaseUri.AbsoluteUri, client.Credentials) {
			Requires.NotNullOrEmpty(tableName, "tableName");
			this.TableName = tableName;
		}

		public string TableName { get; private set; }

		public async Task<PushNotificationClientEntity> GetAsync(string clientPackageSecurityIdentifier) {
			Requires.NotNullOrEmpty(clientPackageSecurityIdentifier, "clientPackageSecurityIdentifier");
			Requires.Argument(clientPackageSecurityIdentifier == null || clientPackageSecurityIdentifier.StartsWith(PushNotificationClientEntity.SchemePrefix), "clientPackageSecurityIdentifier", "Prefix {0} not found", PushNotificationClientEntity.SchemePrefix);

			var query = this.GetQuery(clientPackageSecurityIdentifier.Substring(PushNotificationClientEntity.SchemePrefix.Length));
			var result = await query.ExecuteAsync();
			return result.FirstOrDefault();
		}

		public void AddObject(PushNotificationClientEntity entity) {
			this.AddObject(this.TableName, entity);
		}

		private CloudTableQuery<PushNotificationClientEntity> GetQuery(string rowKey) {
			Requires.NotNullOrEmpty(rowKey, "rowKey");

			return (from inbox in this.CreateQuery<PushNotificationClientEntity>(this.TableName)
					where inbox.RowKey == rowKey
					select inbox).AsTableServiceQuery();
		}
	}
}