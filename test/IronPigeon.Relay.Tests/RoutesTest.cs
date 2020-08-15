// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;
    using System.Web.Routing;
    using Moq;
    using Xunit;

    public class RoutesTest
    {
        private RouteCollection routes;

        public RoutesTest()
        {
            this.routes = new RouteCollection();
            RouteConfig.RegisterRoutes(this.routes);
        }

        [Fact]
        public void InboxCreateRoute()
        {
            var httpContextMock = new Mock<HttpContextBase>();
            httpContextMock.Setup(c => c.Request.AppRelativeCurrentExecutionFilePath)
                .Returns("~/Inbox/create"); // POST

            RouteData routeData = this.routes.GetRouteData(httpContextMock.Object);
            Assert.NotNull(routeData);
            Assert.Equal("Inbox", routeData.Values["controller"]);
            Assert.Equal("create", routeData.Values["action"]);
        }

        [Fact]
        public void InboxPushRoute()
        {
            var httpContextMock = new Mock<HttpContextBase>();
            httpContextMock.Setup(c => c.Request.AppRelativeCurrentExecutionFilePath)
                .Returns("~/Inbox/Push/somethumbprint"); // PUT

            RouteData routeData = this.routes.GetRouteData(httpContextMock.Object);
            Assert.NotNull(routeData);
            Assert.Equal("Inbox", routeData.Values["controller"]);
            Assert.Equal("Push", routeData.Values["action"]);
            Assert.Equal("somethumbprint", routeData.Values["id"]);
        }

        [Fact]
        public void InboxSlotRoute()
        {
            var httpContextMock = new Mock<HttpContextBase>();
            httpContextMock.Setup(c => c.Request.AppRelativeCurrentExecutionFilePath)
                .Returns("~/Inbox/slot/somethumbprint"); // GET, POST, DELETE

            RouteData routeData = this.routes.GetRouteData(httpContextMock.Object);
            Assert.NotNull(routeData);
            Assert.Equal("Inbox", routeData.Values["controller"]);
            Assert.Equal("slot", routeData.Values["action"]);
            Assert.Equal("somethumbprint", routeData.Values["id"]);
        }
    }
}
