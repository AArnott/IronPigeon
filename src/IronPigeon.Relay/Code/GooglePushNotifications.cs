namespace IronPigeon.Relay {
	using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using IronPigeon.Relay.Models;
using Validation;

	public class GooglePushNotifications {
		public GooglePushNotifications() {
		}

		public GooglePushNotifications(HttpClient httpClient, string registrationId) {
			this.HttpClient = httpClient;
			this.RegistrationId = registrationId;
		}

		public HttpClient HttpClient { get; set; }

		public string RegistrationId { get; set; }

		public string GoogleApiKey { get; set; }

		public async Task<bool> PushGoogleRawNotificationAsync(CancellationToken cancellationToken) {
			Requires.ValidState(!string.IsNullOrEmpty(this.RegistrationId), "RegistrationId must be set.");
			Requires.ValidState(!string.IsNullOrEmpty(this.GoogleApiKey), "GoogleApiKey must be set.");

			var value = new GooglePushObject();

			var pushNotifyRequest = new HttpRequestMessage(HttpMethod.Post, "https://android.googleapis.com/gcm/send");
			pushNotifyRequest.Headers.Authorization = new AuthenticationHeaderValue("key=" + this.GoogleApiKey);
			pushNotifyRequest.Content = new ObjectContent<GooglePushObject>(value, new JsonMediaTypeFormatter());
			////pushNotifyRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
			return await this.PushGoogleUpdate(pushNotifyRequest, cancellationToken);
		}

		private async Task<bool> PushGoogleUpdate(HttpRequestMessage request, CancellationToken cancellationToken) {
			Requires.NotNull(request, "request");
			Requires.ValidState(this.HttpClient != null, "HttpClient must be initialized.");

			var response = await this.HttpClient.SendAsync(request, cancellationToken);
			if (response.StatusCode == HttpStatusCode.NotFound) {
				return false;
			}

			response.EnsureSuccessStatusCode();
			return true;
		}

		/// <summary>
		/// An JSON-serializable object used to create a push notification to the 
		/// Google Cloud Messaging service.
		/// </summary>
		[DataContract]
		public class GooglePushObject {
			/// <summary>
			/// Gets or sets a string array with the list of devices (registration IDs) receiving the message.
			/// It must contain at least 1 and at most 1000 registration IDs. To send a multicast
			/// message, you must use JSON. For sending a single message to a single device, you
			/// could use a JSON object with just 1 registration id, or plain text (see below).
			/// A request must include a recipient—this can be either a registration ID, an array
			/// of registration IDs, or a notification_key. Required.
			/// </summary>
			[DataMember(Name = "registration_ids")]
			public string[] RegistrationIds { get; set; }

			/// <summary>
			/// Gets or sets a string that maps a single user to multiple registration
			/// IDs associated with that user. This allows a 3rd-party server to send
			/// a single message to multiple app instances (typically on multiple devices)
			/// owned by a single user. A 3rd-party server can use notification_key as
			/// the target for a message instead of an individual registration ID (or
			/// array of registration IDs). The maximum number of members allowed for
			/// a notification_key is 10. For more discussion of this topic, see User
			/// Notifications. 
			/// </summary>
			[DataMember(Name = "notification_key")]
			public string NotificationKey { get; set; }

			/// <summary>
			/// Gets or sets an arbitrary string (such as "Updates Available") that is
			/// used to collapse a group of like messages when the device is offline,
			/// so that only the last message gets sent to the client. This is intended
			/// to avoid sending too many messages to the phone when it comes back
			/// online. Note that since there is no guarantee of the order in which
			/// messages get sent, the "last" message may not actually be the last
			/// message sent by the application server. Collapse keys are also called
			/// send-to-sync messages. 
			/// </summary>
			[DataMember(Name = "collapse_key")]
			public string CollapseKey { get; set; }

			/// <summary>
			/// Gets or sets a JSON object whose fields represents the key-value pairs
			/// of the message's payload data. If present, the payload data it will be
			/// included in the Intent as application data, with the key being the
			/// extra's name. For instance, "data":{"score":"3x1"} would result in an
			/// intent extra named score whose value is the string 3x1. There is no limit
			/// on the number of key/value pairs, though there is a limit on the total
			/// size of the message (4kb). The values could be any JSON object, but we
			/// recommend using strings, since the values will be converted to strings
			/// in the GCM server anyway. If you want to include objects or other
			/// non-string data types (such as integers or booleans), you have to do the
			/// conversion to string yourself. Also note that the key cannot be a
			/// reserved word (from or any word starting with google.). To complicate
			/// things slightly, there are some reserved words (such as collapse_key) that
			/// are technically allowed in payload data. However, if the request also
			/// contains the word, the value in the request will overwrite the value in
			/// the payload data. Hence using words that are defined as field names in
			/// this table is not recommended, even in cases where they are technically
			/// allowed. Optional.
			/// </summary>
			[DataMember(Name = "data")]
			public Dictionary<string, string> Data { get; set; }

			/// <summary>
			/// Gets or sets a value that indicates that the message should not be sent
			/// immediately if the device is idle. The server will wait for the device
			/// to become active, and then only the last message for each collapse_key
			/// value will be sent. The default value is false.
			/// </summary>
			[DataMember(Name = "delay_while_idle")]
			public bool? DelayWhileIdle { get; set; }

			/// <summary>
			/// Gets or sets how long (in seconds) the message should be kept on GCM storage
			/// if the device is offline. Optional (default time-to-live is 4 weeks).
			/// </summary>
			[DataMember(Name = "time_to_live")]
			public long? TimeToLive { get; set; }

			/// <summary>
			/// Gets or sets a string containing the package name of your application.
			/// When set, messages will only be sent to registration IDs that match the package name.
			/// </summary>
			[DataMember(Name = "restricted_package_name")]
			public string RestrictedPackageName { get; set; }

			/// <summary>
			/// Gets or sets a value that allows developers to test their request without actually sending a message.
			/// The default value is false.
			/// </summary>
			[DataMember(Name = "dry_run")]
			public bool? DryRun { get; set; }
		}
	}
}