namespace IronPigeon.Relay.Controllers {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Web;
	using System.Web.Mvc;

	public class InboxController : Controller {
		[HttpGet, ActionName("Index")]
		public async Task<ActionResult> List(string thumbprint) {
			var list = new IncomingList() {
				Items = new List<IncomingList.IncomingItem>(),
			};

			return new JsonResult() {
				Data = list,
				JsonRequestBehavior = JsonRequestBehavior.AllowGet
			};
		}

		[HttpPost, ActionName("Index")]
		public async Task<ActionResult> PostNotification(string thumbprint) {
			return new EmptyResult();
		}

		[HttpGet, ActionName("Notification")]
		public async Task<ActionResult> GetNotification(string thumbprint, string notificationId) {
			return new EmptyResult();
		}

		[HttpDelete, ActionName("Notification")]
		public async Task<ActionResult> DeleteNotification(string thumbprint, string notificationId) {
			return new EmptyResult();
		}
	}
}
