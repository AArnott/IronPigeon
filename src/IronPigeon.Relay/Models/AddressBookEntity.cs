namespace IronPigeon.Relay.Models {
	using System;
	using System.Collections.Generic;
	using System.ComponentModel.DataAnnotations.Schema;
	using System.Linq;
	using System.Web;
	using Validation;

	/// <summary>
	/// The Azure table storage entity tracking address book entries.
	/// </summary>
	public class AddressBookEntity : TableStorageEntity {
		/// <summary>
		/// The value for <see cref="Provider"/> used for Microsoft accounts.
		/// </summary>
		public const string MicrosoftProvider = "Microsoft";

		/// <summary>
		/// The default partition that address books are filed under.
		/// </summary>
		private const string DefaultPartition = "AddressBook";

		/// <summary>
		/// Initializes a new instance of the <see cref="AddressBookEntity" /> class.
		/// </summary>
		public AddressBookEntity() {
			this.PartitionKey = DefaultPartition;
		}

		/// <summary>
		/// Gets or sets the user's identity provider.
		/// </summary>
		[NotMapped]
		public string Provider {
			get {
				string provider, userId;
				SplitRowKey(this.RowKey, out provider, out userId);
				return provider;
			}

			set {
				this.RowKey = ConstructRowKey(value, this.UserId);
			}
		}

		/// <summary>
		/// Gets or sets a user ID that is unique within the <see cref="Provider"/>.
		/// </summary>
		[NotMapped]
		public string UserId {
			get {
				string provider, userId;
				SplitRowKey(this.RowKey, out provider, out userId);
				return userId;
			}

			set {
				this.RowKey = ConstructRowKey(this.Provider, value);
			}
		}

		public string FirstName { get; set; }

		public string LastName { get; set; }

		public string AddressBookUrl { get; set; }

		internal static string ConstructRowKey(string provider, string userId) {
			return Uri.EscapeDataString(provider ?? string.Empty) + "&" + Uri.EscapeDataString(userId ?? string.Empty);
		}

		internal static void SplitRowKey(string rowKey, out string provider, out string userId) {
			if (string.IsNullOrEmpty(rowKey)) {
				provider = null;
				userId = null;
				return;
			}

			Requires.Argument(rowKey.IndexOf('&') >= 0, "rowKey", "Invalid format.  Missing & character.");
			var parts = rowKey.Split('&');
			provider = Uri.UnescapeDataString(parts[0]);
			userId = Uri.UnescapeDataString(parts[1]);
		}
	}
}