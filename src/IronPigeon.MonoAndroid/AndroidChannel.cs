namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Composition;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Validation;

	/// <summary>
	/// An Android implementation of <see cref="Channel"/>.
	/// </summary>
	[Export(typeof(Channel))]
	[Shared]
	public class AndroidChannel : Channel {
		/// <summary>
		/// Registers a Windows 8 application to receive push notifications for incoming messages.
		/// </summary>
		/// <param name="googlePlayRegistrationId">The Google Cloud Messaging registration identifier.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>
		/// A task representing the async operation.
		/// </returns>
		public async Task RegisterPushNotificationChannelAsync(string googlePlayRegistrationId, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNullOrEmpty(googlePlayRegistrationId, "googlePlayRegistrationId");

			var request = new HttpRequestMessage(HttpMethod.Put, this.Endpoint.PublicEndpoint.MessageReceivingEndpoint);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.Endpoint.InboxOwnerCode);
			request.Content = new FormUrlEncodedContent(new Dictionary<string, string> {
				{ "gcm_registration_id", googlePlayRegistrationId },
			});
			var response = await this.HttpClient.SendAsync(request, cancellationToken);
			response.EnsureSuccessStatusCode();
		}
	}
}
