using System.Web.Mvc;
using FlockManagement.Business.Interfaces;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Enums;
using FlockManagement.Web.Infrastructure;
using FlockManagement.Web.ViewModels;

namespace FlockManagement.Web.Controllers
{
    public class AccountController : BaseController
    {
        private readonly IAuthService _auth;

        public AccountController(IAuthService auth)
        {
            _auth = auth;
        }

        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            if (User.Identity.IsAuthenticated) return RedirectToLocal(returnUrl);
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var result = _auth.Login(model.UserName, model.Password, Request.UserHostAddress);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                model.Password = null;
                return View(model);
            }

            var mustChange = result.Outcome == LoginOutcome.MustChangePassword;
            AuthCookie.Issue(HttpContext, result.User, mustChange);

            if (mustChange)
            {
                TempData[FlashSuccess] = "Welcome " + result.User.FullName + ". Please set a new password to continue.";
                return RedirectToAction("ChangePassword");
            }
            return RedirectToLocal(model.ReturnUrl);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            if (CurrentUser != null)
                _auth.RecordLogout(CurrentUser.UserID, CurrentUser.Identity.Name, Request.UserHostAddress);
            AuthCookie.SignOut(HttpContext);
            return RedirectToAction("Login");
        }

        public ActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel { IsForced = CurrentUser != null && CurrentUser.MustChangePassword });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ChangePasswordViewModel model)
        {
            model.IsForced = CurrentUser.MustChangePassword;
            if (!ModelState.IsValid) return View(model);

            var result = _auth.ChangePassword(CurrentUser.UserID, model.CurrentPassword, model.NewPassword);
            if (!result.Success)
            {
                AddErrors(result);
                return View(model);
            }

            // Re-issue the cookie without the must-change flag.
            var data = CurrentUser.AppIdentity.Data;
            data.MustChangePassword = false;
            AuthCookie.Issue(HttpContext, CurrentUser.Identity.Name, data);

            Success(result.Message);
            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        public ActionResult AccessDenied()
        {
            Response.StatusCode = 403;
            Response.TrySkipIisCustomErrors = true;
            return View();
        }

        /// <summary>Keeps the session alive while the user is typing in a long form (called by app.js).</summary>
        [HttpPost]
        public ActionResult KeepAlive()
        {
            return Json(new { ok = true });
        }
    }
}
