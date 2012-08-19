﻿namespace IronPigeon.Relay.Models {
	using System;
	using System.Collections.Generic;
	using System.Data.Services.Common;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Web;

	using Microsoft.WindowsAzure.StorageClient;

	public class InboxEntity : TableStorageEntity {
		private const string DefaultPartition = "Inbox";

		private const int CodeLength = 16;

		public InboxEntity() {
			this.PartitionKey = DefaultPartition;
		}

		public static InboxEntity Create() {
			var entity = new InboxEntity();

			var rng = RNGCryptoServiceProvider.Create();
			var inboxOwnerCode = new byte[CodeLength];
			rng.GetBytes(inboxOwnerCode);
			entity.InboxOwnerCode = Utilities.ToBase64WebSafe(inboxOwnerCode);
			entity.RowKey = Guid.NewGuid().ToString();

			return entity;
		}

		/// <summary>
		/// Gets or sets the inbox owner code.
		/// </summary>
		public string InboxOwnerCode { get; set; }
	}
}