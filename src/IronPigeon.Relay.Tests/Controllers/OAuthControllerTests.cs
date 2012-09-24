namespace IronPigeon.Relay.Tests.Controllers {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using IronPigeon.Relay.Controllers;
	using Newtonsoft.Json;
	using NUnit.Framework;

	[TestFixture]
	public class OAuthControllerTests {
		[Test]
		public void MicrosoftAccountMeInfoDeserialization() {
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
			Assert.That(microsoftAccountInfo.Id, Is.EqualTo("063d0c265f96e43d"));
			Assert.That(microsoftAccountInfo.FirstName, Is.EqualTo("Andrew"));
			Assert.That(microsoftAccountInfo.LastName, Is.EqualTo("Arnott"));
			Assert.That(microsoftAccountInfo.Emails.Count, Is.EqualTo(4));
			Assert.That(microsoftAccountInfo.Emails["preferred"], Is.EqualTo("andrewarnott@gmail.com"));
			Assert.That(microsoftAccountInfo.Emails["account"], Is.EqualTo("andrewarnott@live.com"));
			Assert.That(microsoftAccountInfo.Emails["personal"], Is.Null);
			Assert.That(microsoftAccountInfo.Emails["business"], Is.Null);
		}
	}
}
