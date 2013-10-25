namespace IronPigeon.Relay {
	using System;
	using System.Collections.Generic;
	using System.Data.Services.Client;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Web;
	using System.Web.Mvc;
	using Validation;

	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
	public class InboxOwnerAuthorizeAttribute : AuthorizeAttribute {
		private const string BearerTokenPrefix = "Bearer ";

		public override void OnAuthorization(AuthorizationContext filterContext) {
			Requires.NotNull(filterContext, "filterContext");
			Verify.Operation(
				!OutputCacheAttribute.IsChildActionCacheActive((ControllerContext)filterContext),
				"AuthorizeAttribute_CannotUseWithinChildActionCache");

			bool inherit = true;
			if (!filterContext.ActionDescriptor.IsDefined(typeof(AllowAnonymousAttribute), inherit) &&
				!filterContext.ActionDescriptor.ControllerDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true)) {
				if (this.AuthorizeCore(filterContext)) {
					HttpCachePolicyBase cache = filterContext.HttpContext.Response.Cache;
					cache.SetProxyMaxAge(new TimeSpan(0L));
					cache.AddValidationCallback(this.CacheValidateHandler, null);
				} else {
					this.HandleUnauthorizedRequest(filterContext);
				}
			}
		}

		protected virtual bool AuthorizeCore(AuthorizationContext authorizationContext) {
			Requires.NotNull(authorizationContext, "authorizationContext");

			var httpContext = authorizationContext.HttpContext;
			object idObject;
			if (httpContext.Request.RequestContext.RouteData.Values.TryGetValue("id", out idObject)) {
				var id = idObject as string;
				if (id != null) {
					string authorization = httpContext.Request.Headers["Authorization"];
					if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith(BearerTokenPrefix, StringComparison.Ordinal)) {
						var bearerToken = authorization.Substring(BearerTokenPrefix.Length);
						var controller = (Controllers.InboxController)authorizationContext.Controller;
						var inboxEntity = controller.InboxTable.Get(id).FirstOrDefault();
						if (inboxEntity != null) {
							bool match = bearerToken == inboxEntity.InboxOwnerCode;
							if (match) {
								inboxEntity.LastAuthenticatedInteractionUtc = DateTime.UtcNow;
								controller.InboxTable.UpdateObject(inboxEntity);
								Task.Run(() => controller.InboxTable.SaveChangesWithMergeAsync(inboxEntity)).GetAwaiter().GetResult();
							}

							return match;
						}
					}
				}
			}

			return false;
		}

		protected override HttpValidationStatus OnCacheAuthorization(HttpContextBase httpContext) {
			Requires.NotNull(httpContext, "httpContext");

			// We don't have enough details like the name of the azure table to answer this.
			return HttpValidationStatus.IgnoreThisRequest;
		}

		#region Not supported methods

		protected override bool AuthorizeCore(HttpContextBase httpContext) {
			throw new NotSupportedException();
		}

		#endregion

		private void CacheValidateHandler(HttpContext context, object data, ref HttpValidationStatus validationStatus) {
			validationStatus = this.OnCacheAuthorization(new HttpContextWrapper(context));
		}
	}
}