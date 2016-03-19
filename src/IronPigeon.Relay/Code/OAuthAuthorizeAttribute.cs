// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Web;
    using System.Web.Mvc;
    using DotNetOpenAuth.Messaging;
    using DotNetOpenAuth.OAuth2;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class OAuthAuthorizeAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            var rsa = new RSACryptoServiceProvider();
            rsa.ImportCspBlob(Convert.FromBase64String(ConfigurationManager.AppSettings["PrivateAsymmetricKey"]));
            var analyzer = new StandardAccessTokenAnalyzer(rsa, rsa);
            var resourceServer = new ResourceServer(analyzer);
            try
            {
                httpContext.User = resourceServer.GetPrincipal(
                    httpContext.Request, this.Roles.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            }
            catch (ProtocolException)
            {
                httpContext.User = null;
            }

            return httpContext.User != null;
        }
    }
}