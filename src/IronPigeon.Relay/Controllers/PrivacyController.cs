// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Web;
    using System.Web.Mvc;

    public class PrivacyController : Controller
    {
        /// <summary>
        /// GET: /Privacy/
        /// </summary>
        /// <returns>The web result.</returns>
        public ActionResult Index()
        {
            return this.View();
        }
    }
}
