// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class UtilitiesTests2
    {
        [TestMethod]
        public void UrlEncode()
        {
            var data = new Dictionary<string, string> { { "a", "b" }, { "a=b&c", "e=f&g" }, };
            string urlEncoded = data.UrlEncode();
            Assert.AreEqual("a=b&a%3Db%26c=e%3Df%26g", urlEncoded);

            var decoded = HttpUtility.ParseQueryString(urlEncoded);
            Assert.AreEqual(data.Count, decoded.Count);
            foreach (string key in decoded)
            {
                Assert.AreEqual(decoded[key], data[key]);
            }
        }
    }
}
