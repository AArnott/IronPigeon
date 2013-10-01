namespace IronPigeon.Relay {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Threading.Tasks;
	using System.Web;
	using System.Xml.Linq;

	using IronPigeon.Relay.Models;

	using Validation;

	public class WinPhonePushNotifications {
		private const string WinPhoneNS = "WPNotification";

		public WinPhonePushNotifications() {
		}

		public WinPhonePushNotifications(HttpClient httpClient, Uri channelUri) {
			this.HttpClient = httpClient;
			this.ChannelUri = channelUri;
		}

		public HttpClient HttpClient { get; set; }

		public Uri ChannelUri { get; set; }

		public async Task<bool> PushWinPhoneTileAsync(string backgroundImage = null, string title = null, int? count = null) {
			Requires.ValidState(this.ChannelUri != null, "ChannelUri must be set.");

			var tile = TileUpdate(backgroundImage, title, count);
			var pushNotifyRequest = new HttpRequestMessage(HttpMethod.Post, this.ChannelUri);
			pushNotifyRequest.Headers.Add("X-WindowsPhone-Target", "token");
			pushNotifyRequest.Headers.Add("X-NotificationClass", "1");
			pushNotifyRequest.Content = new StringContent(tile.ToString());
			pushNotifyRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
			return await this.PushWinPhoneUpdate(pushNotifyRequest);
		}

		public async Task<bool> PushWinPhoneToastAsync(string text1, string text2) {
			Requires.ValidState(this.ChannelUri != null, "ChannelUri must be set.");

			var toast = Toast(text1, text2);
			var pushNotifyRequest = new HttpRequestMessage(HttpMethod.Post, this.ChannelUri);
			pushNotifyRequest.Headers.Add("X-WindowsPhone-Target", "toast");
			pushNotifyRequest.Headers.Add("X-NotificationClass", "2");
			pushNotifyRequest.Content = new StringContent(toast.ToString());
			pushNotifyRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
			return await this.PushWinPhoneUpdate(pushNotifyRequest);
		}

		public async Task<bool> PushWinPhoneRawNotificationAsync(string content) {
			Requires.ValidState(this.ChannelUri != null, "ChannelUri must be set.");

			var pushNotifyRequest = new HttpRequestMessage(HttpMethod.Post, this.ChannelUri);
			pushNotifyRequest.Headers.Add("X-NotificationClass", "3");
			pushNotifyRequest.Content = new StringContent(content);
			pushNotifyRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
			return await this.PushWinPhoneUpdate(pushNotifyRequest);
		}

		private static XElement TileUpdate(string backgroundImage = null, string title = null, int? count = null) {
			var tile = new XElement(XName.Get("Tile", WinPhoneNS));
			if (backgroundImage != null) {
				tile.Add(new XElement(XName.Get("BackgroundImage", WinPhoneNS), backgroundImage));
			}

			if (title != null) {
				tile.Add(new XElement(XName.Get("Title", WinPhoneNS), title));
			}

			if (count.HasValue) {
				tile.Add(new XElement(XName.Get("Count", WinPhoneNS), count.Value));
			}

			return new XElement(XName.Get("Notification", WinPhoneNS), tile);
		}

		private static XElement Toast(string text1, string text2) {
			return new XElement(
				XName.Get("Notification", WinPhoneNS),
				new XElement(
					XName.Get("Toast", WinPhoneNS),
					new XElement(XName.Get("Text1", WinPhoneNS), text1),
					new XElement(XName.Get("Text2", WinPhoneNS), text2)));
		}

		private async Task<bool> PushWinPhoneUpdate(HttpRequestMessage request) {
			Requires.NotNull(request, "request");
			Requires.ValidState(this.HttpClient != null, "HttpClient must be initialized.");

			var response = await this.HttpClient.SendAsync(request);
			if (response.StatusCode == HttpStatusCode.NotFound) {
				return false;
			}

			response.EnsureSuccessStatusCode();
			return true;
		}
	}
}