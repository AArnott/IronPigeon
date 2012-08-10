namespace IronPigeon.Relay.Tests.Controllers {
	using System;
	using System.Web.Mvc;

	using IronPigeon.Relay.Controllers;

	using NUnit.Framework;

	[TestFixture]
	public class InboxControllerTest {
		[Test]
		public void List() {
			var controller = new InboxController();
			string thumbprint = null;
			ActionResult result = controller.List(thumbprint).Result;

			Assert.That(result, Is.InstanceOf<JsonResult>());
			var jsonResult = (JsonResult)result;
			Assert.That(jsonResult.JsonRequestBehavior, Is.EqualTo(JsonRequestBehavior.AllowGet));
			var data = (IncomingList)jsonResult.Data;
			Assert.That(data, Is.Not.Null);
		}

		[Test, Ignore("Not yet implemented")]
		public void PostNotification() {

		}

		[Test, Ignore("Not yet implemented")]
		public void GetNotification() {

		}

		[Test, Ignore("Not yet implemented")]
		public void DeleteNotification() {

		}
	}
}
