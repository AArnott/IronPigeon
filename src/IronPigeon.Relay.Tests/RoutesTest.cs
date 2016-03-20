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

    using NUnit.Framework;

    [TestFixture]
    public class RoutesTest
    {
        private RouteCollection routes;

        [SetUp]
        public void SetUp()
        {
            this.routes = new RouteCollection();
            RouteConfig.RegisterRoutes(this.routes);
        }

        [Test]
        public void InboxCreateRoute()
        {
            var httpContextMock = new Mock<HttpContextBase>();
            httpContextMock.Setup(c => c.Request.AppRelativeCurrentExecutionFilePath)
                .Returns("~/Inbox/create"); // POST

            RouteData routeData = this.routes.GetRouteData(httpContextMock.Object);
            Assert.NotNull(routeData);
            Assert.That(routeData.Values["controller"], Is.EqualTo("Inbox"));
            Assert.That(routeData.Values["action"], Is.EqualTo("create"));
        }

        [Test]
        public void InboxPushRoute()
        {
            var httpContextMock = new Mock<HttpContextBase>();
            httpContextMock.Setup(c => c.Request.AppRelativeCurrentExecutionFilePath)
                .Returns("~/Inbox/Push/somethumbprint"); // PUT

            RouteData routeData = this.routes.GetRouteData(httpContextMock.Object);
            Assert.NotNull(routeData);
            Assert.That(routeData.Values["controller"], Is.EqualTo("Inbox"));
            Assert.That(routeData.Values["action"], Is.EqualTo("Push"));
            Assert.That(routeData.Values["id"], Is.EqualTo("somethumbprint"));
        }

        [Test]
        public void InboxSlotRoute()
        {
            var httpContextMock = new Mock<HttpContextBase>();
            httpContextMock.Setup(c => c.Request.AppRelativeCurrentExecutionFilePath)
                .Returns("~/Inbox/slot/somethumbprint"); // GET, POST, DELETE

            RouteData routeData = this.routes.GetRouteData(httpContextMock.Object);
            Assert.NotNull(routeData);
            Assert.That(routeData.Values["controller"], Is.EqualTo("Inbox"));
            Assert.That(routeData.Values["action"], Is.EqualTo("slot"));
            Assert.That(routeData.Values["id"], Is.EqualTo("somethumbprint"));
        }
    }
}
