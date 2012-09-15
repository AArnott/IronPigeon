namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Validation;
	using Windows.Networking.PushNotifications;

	/// <summary>
	/// A channel for sending or receiving secure messages with additional WinRT specific functionality.
	/// </summary>
	public class WinRTChannel : Channel {
		/// <summary>
		/// Initializes a new instance of the <see cref="WinRTChannel" /> class.
		/// </summary>
		public WinRTChannel() {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WinRTChannel" /> class.
		/// </summary>
		/// <param name="blobStorageProvider">The blob storage provider.</param>
		/// <param name="cryptoProvider">The crypto provider.</param>
		/// <param name="endpoint">The receiving endpoint.</param>
		public WinRTChannel(ICloudBlobStorageProvider blobStorageProvider, ICryptoProvider cryptoProvider, OwnEndpoint endpoint)
			: base(blobStorageProvider, cryptoProvider, endpoint) {
		}

		/// <summary>
		/// Gets or sets the package security identifier of the app.
		/// </summary>
		/// <value>
		/// The package security identifier.
		/// </value>
		public string PackageSecurityIdentifier { get; set; }

		/// <summary>
		/// Registers a Windows 8 application to receive push notifications for incoming messages.
		/// </summary>
		/// <param name="pushNotificationChannel">The push notification channel.</param>
		/// <param name="pushContent">Content of the push.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>A task representing the async operation.</returns>
		public async Task RegisterPushNotificationChannelAsync(PushNotificationChannel pushNotificationChannel, string pushContent, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(pushNotificationChannel, "pushNotificationChannel");
			Requires.ValidState(!string.IsNullOrEmpty(this.PackageSecurityIdentifier), "PackageSecurityIdentifier must be initialized first.");

			var request = new HttpRequestMessage(HttpMethod.Put, this.Endpoint.PublicEndpoint.MessageReceivingEndpoint);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.Endpoint.InboxOwnerCode);
			request.Content = new FormUrlEncodedContent(new Dictionary<string, string> {
				{ "package_security_identifier", this.PackageSecurityIdentifier },
				{ "channel_uri", pushNotificationChannel.Uri },
				{ "channel_content", pushContent ?? string.Empty },
				{ "expiration", pushNotificationChannel.ExpirationTime.ToString(CultureInfo.InvariantCulture) },
			});
			var response = await this.HttpClient.SendAsync(request, cancellationToken);
			response.EnsureSuccessStatusCode();
		}
	}
}
