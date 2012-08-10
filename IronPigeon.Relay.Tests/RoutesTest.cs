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

	[TestFixture]
	public class RoutesTest {
		private RouteCollection routes;

		[SetUp]
		public void SetUp() {
			this.routes = new RouteCollection();
			RouteConfig.RegisterRoutes(this.routes);
		}

		[Test]
		public void InboxRoutes() {
			var httpContextMock = new Mock<HttpContextBase>();
			httpContextMock.Setup(c => c.Request.AppRelativeCurrentExecutionFilePath)
				.Returns("~/inbox/somethumbprint");

			RouteData routeData = this.routes.GetRouteData(httpContextMock.Object);
			Assert.NotNull(routeData);
			Assert.That(routeData.Values["controller"], Is.EqualTo("Inbox"));
			Assert.That(routeData.Values["action"], Is.EqualTo("Index"));
			Assert.That(routeData.Values["thumbprint"], Is.EqualTo("somethumbprint"));
		}

		[Test]
		public void NotificationRoutes() {
			var httpContextMock = new Mock<HttpContextBase>();
			httpContextMock.Setup(c => c.Request.AppRelativeCurrentExecutionFilePath)
				.Returns("~/inbox/somethumbprint/someId");

			RouteData routeData = this.routes.GetRouteData(httpContextMock.Object);
			Assert.NotNull(routeData);
			Assert.That(routeData.Values["controller"], Is.EqualTo("Inbox"));
			Assert.That(routeData.Values["action"], Is.EqualTo("Notification"));
			Assert.That(routeData.Values["thumbprint"], Is.EqualTo("somethumbprint"));
			Assert.That(routeData.Values["notificationId"], Is.EqualTo("someId"));
		}
	}
}
