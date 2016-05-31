// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay.Tests.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using IronPigeon.Relay.Controllers;
    using Newtonsoft.Json;
    using Xunit;

    public class OAuthControllerTests
    {
        [Fact]
        public void MicrosoftAccountMeInfoDeserialization()
        {
            const string JsonInfo = @"
{
   ""id"": ""063d0c265f96e43d"", 
   ""name"": ""Andrew Arnott"", 
   ""first_name"": ""Andrew"", 
   ""last_name"": ""Arnott"", 
   ""link"": ""http://profile.live.com/cid-063d0c265f96e43d/"", 
   ""gender"": ""male"", 
   ""emails"": {
      ""preferred"": ""andrewarnott@gmail.com"", 
      ""account"": ""andrewarnott@live.com"", 
      ""personal"": null, 
      ""business"": null
   }, 
   ""locale"": ""en_US"", 
   ""updated_time"": ""2012-09-23T22:56:27+0000""
}";

            var serializer = new JsonSerializer();
            var jsonReader = new JsonTextReader(new StringReader(JsonInfo));
            var microsoftAccountInfo = serializer.Deserialize<OAuthController.MicrosoftAccountInfo>(jsonReader);
            Assert.Equal("063d0c265f96e43d", microsoftAccountInfo.Id);
            Assert.Equal("Andrew", microsoftAccountInfo.FirstName);
            Assert.Equal("Arnott", microsoftAccountInfo.LastName);
            Assert.Equal(4, microsoftAccountInfo.Emails.Count);
            Assert.Equal("andrewarnott@gmail.com", microsoftAccountInfo.Emails["preferred"]);
            Assert.Equal("andrewarnott@live.com", microsoftAccountInfo.Emails["account"]);
            Assert.Null(microsoftAccountInfo.Emails["personal"]);
            Assert.Null(microsoftAccountInfo.Emails["business"]);
        }
    }
}
