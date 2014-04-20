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

	/// <summary>
	/// The Windows Phone 8 implementation of an IronPigeon channel.
	/// </summary>
	public class WinPhoneChannel : Channel {
		/// <summary>
		/// Registers a Windows 8 application to receive push notifications for incoming messages.
		/// </summary>
		/// <param name="pushNotificationChannelUri">The push notification channel's ChannelUri property.</param>
		/// <param name="pushContent">Content of the push.</param>
		/// <param name="toastLine1">The first line in the toast notification to send.</param>
		/// <param name="toastLine2">The second line in the toast notification to send.</param>
		/// <param name="tileTemplate">The tile template used by the client app.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>
		/// A task representing the async operation.
		/// </returns>
		public async Task RegisterPushNotificationChannelAsync(Uri pushNotificationChannelUri, string pushContent = null, string toastLine1 = null, string toastLine2 = null, string tileTemplate = null, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(pushNotificationChannelUri, "pushNotificationChannelUri");

			var request = new HttpRequestMessage(HttpMethod.Put, this.Endpoint.PublicEndpoint.MessageReceivingEndpoint);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.Endpoint.InboxOwnerCode);
			request.Content = new FormUrlEncodedContent(new Dictionary<string, string> {
				{ "wp8_channel_uri", pushNotificationChannelUri.AbsoluteUri },
				{ "wp8_channel_content", pushContent ?? string.Empty },
				{ "wp8_channel_toast_text1", toastLine1 ?? string.Empty },
				{ "wp8_channel_toast_text2", toastLine2 ?? string.Empty },
				{ "wp8_channel_tile_template", tileTemplate ?? string.Empty },
			});
			var response = await this.HttpClient.SendAsync(request, cancellationToken);
			response.EnsureSuccessStatusCode();
		}
	}
}
