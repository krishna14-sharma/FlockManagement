using System;
using System.Web.Mvc;
using FlockManagement.Common.Helpers;
using FlockManagement.Web.Infrastructure;

namespace FlockManagement.Web.Controllers
{
    /// <summary>Common helpers for all controllers.</summary>
    [Authorize]
    public abstract class BaseController : Controller
    {
        protected const string FlashSuccess = "Flash.Success";
        protected const string FlashError = "Flash.Error";

        protected AppPrincipal CurrentUser
        {
            get { return User as AppPrincipal; }
        }

        protected bool Can(string permissionCode)
        {
            return CurrentUser != null && CurrentUser.HasPermission(permissionCode);
        }

        protected void Success(string message)
        {
            if (!string.IsNullOrEmpty(message)) TempData[FlashSuccess] = message;
        }

        protected void Error(string message)
        {
            if (!string.IsNullOrEmpty(message)) TempData[FlashError] = message;
        }

        /// <summary>Copies a failed OperationResult into ModelState so it shows next to the field.</summary>
        protected void AddErrors(OperationResult result)
        {
            if (result == null || result.Success) return;
            if (result.FieldErrors.Count > 0)
            {
                foreach (var pair in result.FieldErrors)
                    ModelState.AddModelError(pair.Key, pair.Value);
            }
            else
            {
                ModelState.AddModelError(string.Empty, result.Message);
            }
        }

        protected ActionResult RedirectToLocal(string returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && !returnUrl.StartsWith("//", StringComparison.Ordinal)
                && !returnUrl.StartsWith("/\\", StringComparison.Ordinal))
                return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }

        protected ActionResult NotFoundView()
        {
            Response.StatusCode = 404;
            Response.TrySkipIisCustomErrors = true;
            return View("~/Views/Shared/NotFound.cshtml");
        }

        protected override void OnException(ExceptionContext filterContext)
        {
            if (filterContext.ExceptionHandled) return;
            ErrorLogger.Log(filterContext.Exception, filterContext.HttpContext);

            var business = filterContext.Exception as BusinessException;
            if (business != null && !filterContext.HttpContext.Request.IsAjaxRequest())
            {
                // A rule broken outside the normal save flow: show the message, go back to the list.
                TempData[FlashError] = business.Message;
                filterContext.Result = RedirectToAction("Index");
                filterContext.ExceptionHandled = true;
                return;
            }
            base.OnException(filterContext);
        }
    }
}
