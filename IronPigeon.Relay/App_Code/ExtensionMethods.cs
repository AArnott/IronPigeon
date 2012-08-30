namespace IronPigeon.Relay {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Web;
#if !NET40
	using TaskEx = System.Threading.Tasks.Task;
#endif

	internal static class ExtensionMethods {
		/// <summary>
		/// Gets the client disconnected token.
		/// </summary>
		/// <param name="response">The response.</param>
		internal static CancellationToken GetClientDisconnectedToken(this HttpResponseBase response) {
#if NET40
			var cts = new CancellationTokenSource();
			TaskEx.Run(async delegate {
				while (response.IsClientConnected) {
					await TaskEx.Delay(5000);
				}

				cts.Cancel();
			});

			return cts.Token;
#else
			return response.ClientDisconnectedToken;
#endif
		}
	}
}