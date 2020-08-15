// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests
{
    using System.Collections.Generic;
    using System.Web;
    using Xunit;

    public class UtilitiesTests2
    {
        [Fact]
        public void UrlEncode()
        {
            var data = new Dictionary<string, string> { { "a", "b" }, { "a=b&c", "e=f&g" }, };
            string urlEncoded = data.UrlEncode();
            Assert.Equal("a=b&a%3Db%26c=e%3Df%26g", urlEncoded);

            System.Collections.Specialized.NameValueCollection? decoded = HttpUtility.ParseQueryString(urlEncoded);
            Assert.Equal(data.Count, decoded.Count);
            foreach (string key in decoded)
            {
                Assert.Equal(decoded[key], data[key]);
            }
        }
    }
}
