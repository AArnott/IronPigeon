namespace IronPigeon.Relay.Tests {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using System.Web;
	using System.Web.Routing;

	using Moq;

	using NUnit.Framework;

	internal class TestHelper {
		internal static void AssertRoute(RouteCollection routes, string url, object expectations) {
			var httpContextMock = new Mock<HttpContextBase>();
			httpContextMock.Setup(c => c.Request.AppRelativeCurrentExecutionFilePath)
				.Returns(url);

			RouteData routeData = routes.GetRouteData(httpContextMock.Object);
			Assert.IsNotNull(routeData, "Should have found the route");

			foreach (var kvp in new RouteValueDictionary(expectations)) {
				Assert.True(
					string.Equals(kvp.Value.ToString(),
						routeData.Values[kvp.Key].ToString(),
						StringComparison.OrdinalIgnoreCase),
					string.Format("Expected '{0}', not '{1}' for '{2}'.",
						kvp.Value, routeData.Values[kvp.Key], kvp.Key));
			}
		}
	}
}
