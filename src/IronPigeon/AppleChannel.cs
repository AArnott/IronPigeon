namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Validation;

	/// <summary>
	/// An Apple iOS implementation of <see cref="Channel"/>.
	/// </summary>
	public class AppleChannel : Channel {
		/// <summary>
		/// Registers an iOS application to receive push notifications for incoming messages.
		/// </summary>
		/// <param name="deviceToken">The Apple-assigned device token to use from the cloud to reach this device.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>
		/// A task representing the async operation.
		/// </returns>
		public async Task RegisterPushNotificationChannelAsync(string deviceToken, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNullOrEmpty(deviceToken, "deviceToken");

			var request = new HttpRequestMessage(HttpMethod.Put, this.Endpoint.PublicEndpoint.MessageReceivingEndpoint);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.Endpoint.InboxOwnerCode);
			request.Content = new FormUrlEncodedContent(new Dictionary<string, string> {
				{ "ios_device_token", deviceToken },
			});
			var response = await this.HttpClient.SendAsync(request, cancellationToken);
			response.EnsureSuccessStatusCode();
		}
	}
}
