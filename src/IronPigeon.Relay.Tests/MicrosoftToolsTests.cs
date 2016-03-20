// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using IronPigeon.Relay.Code;

    using NUnit.Framework;

    [TestFixture]
    public class MicrosoftToolsTests
    {
        [Test]
        public void GetEmailHash()
        {
            Assert.That(
                MicrosoftTools.GetEmailHash("andrewarnott@gmail.com"),
                Is.EqualTo("c56c97c675466cdbecaa76cb43cf41756ec510d1faa58e382d10c66723c310f0"));
        }
    }
}
