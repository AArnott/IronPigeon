namespace IronPigeon.Tests.Mocks {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net.Http;
	using System.Text;
	using System.Threading.Tasks;
	using Validation;

	internal class HttpMessageHandlerMock : HttpMessageHandler {
		private readonly List<Func<HttpRequestMessage, Task<HttpResponseMessage>>> handlers = new List<Func<HttpRequestMessage, Task<HttpResponseMessage>>>();

		internal HttpMessageHandlerMock() {
		}

		internal void ClearHandlers() {
			this.handlers.Clear();
		}

		internal void RegisterHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) {
			Requires.NotNull(handler, "handler");
			this.handlers.Add(handler);
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken) {
			foreach (var handler in this.handlers) {
				var result = await handler(request);
				if (result != null) {
					return result;
				}
			}

			throw new InvalidOperationException("No handler registered for request " + request.RequestUri);
		}
	}
}
