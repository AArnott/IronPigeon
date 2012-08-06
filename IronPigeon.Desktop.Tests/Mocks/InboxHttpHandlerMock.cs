namespace IronPigeon.Tests.Mocks {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net.Http;
	using System.Runtime.Serialization.Json;
	using System.Text;
	using System.Threading.Tasks;

	internal class InboxHttpHandlerMock {
		internal const string InboxBaseUri = "http://localhost/inbox/";

		private readonly Dictionary<Endpoint, List<Tuple<DateTime, byte[]>>> recipients;

		internal InboxHttpHandlerMock(IReadOnlyList<Endpoint> recipients) {
			this.recipients = recipients.ToDictionary(r => r, r => new List<Tuple<DateTime, byte[]>>());
		}

		internal Dictionary<Endpoint, List<Tuple<DateTime, byte[]>>> Inboxes {
			get { return this.recipients; }
		}

		internal void Register(HttpMessageHandlerMock httpMock) {
			httpMock.RegisterHandler(this.HttpHandler);
		}

		private async Task<HttpResponseMessage> HttpHandler(HttpRequestMessage request) {
			if (request.Method == HttpMethod.Post) {
				var recipient = this.recipients.Keys.FirstOrDefault(r => r.MessageReceivingEndpoint.AbsolutePath == request.RequestUri.AbsolutePath);
				if (recipient != null) {
					var inbox = this.recipients[recipient];
					var buffer = await request.Content.ReadAsByteArrayAsync();
					inbox.Add(Tuple.Create(DateTime.UtcNow, buffer));
					return new HttpResponseMessage();
				}
			} else if (request.Method == HttpMethod.Get) {
				var recipient = this.recipients.Keys.FirstOrDefault(r => r.MessageReceivingEndpoint == request.RequestUri);
				if (recipient != null) {
					var inbox = recipients[recipient];
					var list = new IncomingList();
					string locationBase = recipient.MessageReceivingEndpoint.AbsoluteUri + "/";
					list.Items = new List<IncomingList.IncomingItem>();
					for (int i = 0; i < inbox.Count; i++) {
						list.Items.Add(new IncomingList.IncomingItem { DatePostedUtc = inbox[i].Item1, Location = new Uri(locationBase + i) });
					}

					var serializer = new DataContractJsonSerializer(typeof(IncomingList));
					var contentStream = new MemoryStream();
					serializer.WriteObject(contentStream, list);
					contentStream.Position = 0;
					return new HttpResponseMessage { Content = new StreamContent(contentStream) };
				}

				recipient = recipients.Keys.FirstOrDefault(r => request.RequestUri.AbsolutePath.StartsWith(r.MessageReceivingEndpoint.AbsolutePath + "/"));
				if (recipient != null) {
					var messageIndex = int.Parse(request.RequestUri.Segments[request.RequestUri.Segments.Length - 1]);
					var message = this.recipients[recipient][messageIndex];
					byte[] messageBuffer = message.Item2;
					return new HttpResponseMessage { Content = new StreamContent(new MemoryStream(messageBuffer)) };
				}
			}

			return null;
		}
	}
}
