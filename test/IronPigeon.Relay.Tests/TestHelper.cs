// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;
    using System.Web.Routing;
    using Moq;
    using Xunit;

    internal class TestHelper
    {
        internal static void AssertRoute(RouteCollection routes, string url, object expectations)
        {
            var httpContextMock = new Mock<HttpContextBase>();
            httpContextMock.Setup(c => c.Request.AppRelativeCurrentExecutionFilePath)
                .Returns(url);

            RouteData routeData = routes.GetRouteData(httpContextMock.Object);
            Assert.NotNull(routeData); // "Should have found the route"

            foreach (var kvp in new RouteValueDictionary(expectations))
            {
                Assert.True(
                    string.Equals(kvp.Value.ToString(), routeData.Values[kvp.Key].ToString(), StringComparison.OrdinalIgnoreCase),
                    string.Format("Expected '{0}', not '{1}' for '{2}'.", kvp.Value, routeData.Values[kvp.Key], kvp.Key));
            }
        }
    }
}
