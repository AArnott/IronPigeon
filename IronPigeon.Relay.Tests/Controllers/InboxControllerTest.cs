namespace IronPigeon.Relay.Tests.Controllers {
	using System;
	using System.Web.Mvc;

	using IronPigeon.Relay.Controllers;

	using NUnit.Framework;

	[TestFixture]
	public class InboxControllerTest {
		private InboxController controller;

		[SetUp]
		public void SetUp() {
			AzureStorageConfig.RegisterConfiguration();
			this.controller = new InboxController();
		}

		[Test, Ignore("Not yet implemented")]
		public void CreateAction() {
		}

		[Test]
		public void GetInboxItemsAsyncAction() {
			const string Thumbprint = "someThumbprint";
			ActionResult result = this.controller.GetInboxItemsAsync(Thumbprint).Result;

			Assert.That(result, Is.InstanceOf<JsonResult>());
			var jsonResult = (JsonResult)result;
			Assert.That(jsonResult.JsonRequestBehavior, Is.EqualTo(JsonRequestBehavior.AllowGet));
			var data = (IncomingList)jsonResult.Data;
			Assert.That(data, Is.Not.Null);
		}

		[Test, Ignore("Not yet implemented")]
		public void PostNotificationAction() {

		}

		[Test, Ignore("Not yet implemented")]
		public void GetNotificationAction() {

		}

		[Test, Ignore("Not yet implemented")]
		public void DeleteNotificationAction() {

		}
	}
}
