namespace IronPigeon.Relay.Tests {
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
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
		public void InboxCreateRoute() {
			var httpContextMock = new Mock<HttpContextBase>();
			httpContextMock.Setup(c => c.Request.AppRelativeCurrentExecutionFilePath)
				.Returns("~/inbox/somethumbprint/Create"); // POST

			RouteData routeData = this.routes.GetRouteData(httpContextMock.Object);
			Assert.NotNull(routeData);
			Assert.That(routeData.Values["controller"], Is.EqualTo("Inbox"));
			Assert.That(routeData.Values["action"], Is.EqualTo("Create"));
			Assert.That(routeData.Values["thumbprint"], Is.EqualTo("somethumbprint"));
		}

		[Test]
		public void InboxPushRoute() {
			var httpContextMock = new Mock<HttpContextBase>();
			httpContextMock.Setup(c => c.Request.AppRelativeCurrentExecutionFilePath)
				.Returns("~/inbox/somethumbprint/Push"); // PUT

			RouteData routeData = this.routes.GetRouteData(httpContextMock.Object);
			Assert.NotNull(routeData);
			Assert.That(routeData.Values["controller"], Is.EqualTo("Inbox"));
			Assert.That(routeData.Values["action"], Is.EqualTo("Push"));
			Assert.That(routeData.Values["thumbprint"], Is.EqualTo("somethumbprint"));
		}

		[Test]
		public void InboxIndexRoute() {
			var httpContextMock = new Mock<HttpContextBase>();
			httpContextMock.Setup(c => c.Request.AppRelativeCurrentExecutionFilePath)
				.Returns("~/inbox/somethumbprint"); // GET, POST, DELETE

			RouteData routeData = this.routes.GetRouteData(httpContextMock.Object);
			Assert.NotNull(routeData);
			Assert.That(routeData.Values["controller"], Is.EqualTo("Inbox"));
			Assert.That(routeData.Values["action"], Is.EqualTo("Index"));
			Assert.That(routeData.Values["thumbprint"], Is.EqualTo("somethumbprint"));
		}

		[Test]
		public void InboxItemRoute() {
			var httpContextMock = new Mock<HttpContextBase>();
			httpContextMock.Setup(c => c.Request.AppRelativeCurrentExecutionFilePath)
				.Returns("~/inbox/somethumbprint"); // GET, DELETE // ?item=someId

			RouteData routeData = this.routes.GetRouteData(httpContextMock.Object);
			Assert.NotNull(routeData);
			Assert.That(routeData.Values["controller"], Is.EqualTo("Inbox"));
			Assert.That(routeData.Values["action"], Is.EqualTo("Index"));
			Assert.That(routeData.Values["thumbprint"], Is.EqualTo("somethumbprint"));
			////Assert.That(routeData.Values["item"], Is.EqualTo("someId"));
		}
	}
}
