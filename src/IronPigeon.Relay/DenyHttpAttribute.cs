// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay
{
    using System;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>
    /// A filter to deny requests that come in over HTTP.
    /// </summary>
    /// <remarks>
    /// The <see cref="RequireHttpsAttribute"/> <em>redirects</em> HTTP requests to HTTPS.
    /// For actions that carry sensitive data however, we should simply deny such requests so the API clients learn immediately to use HTTPS.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class DenyHttpAttribute : Attribute, IAuthorizationFilter, IOrderedFilter
    {
        /// <inheritdoc />
        /// <value>Default is <c>int.MinValue + 50</c> to run this <see cref="IAuthorizationFilter"/> early.</value>
        public int Order { get; set; } = int.MinValue + 50;

        /// <summary>
        /// Called early in the filter pipeline to confirm request is authorized. Confirms requests are received over
        /// HTTPS. Takes no action for HTTPS requests. Otherwise if it was a GET request, sets
        /// <see cref="AuthorizationFilterContext.Result"/> to a result which will redirect the client to the HTTPS
        /// version of the request URI. Otherwise, sets <see cref="AuthorizationFilterContext.Result"/> to a result
        /// which will set the status code to <c>403</c> (Forbidden).
        /// </summary>
        /// <inheritdoc />
        public virtual void OnAuthorization(AuthorizationFilterContext filterContext)
        {
            if (filterContext is null)
            {
                throw new ArgumentNullException(nameof(filterContext));
            }

            if (!filterContext.HttpContext.Request.IsHttps)
            {
                filterContext.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            }
        }
    }
}
