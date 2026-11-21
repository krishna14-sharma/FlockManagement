using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Interfaces;
using Newtonsoft.Json;

namespace FlockManagement.Web.Infrastructure
{
    /// <summary>Data carried in the encrypted forms-authentication ticket.</summary>
    public class AuthTicketData
    {
        public int UserID { get; set; }
        public int RoleID { get; set; }
        public string RoleName { get; set; }
        public string FullName { get; set; }
        public int? FarmID { get; set; }
        public bool MustChangePassword { get; set; }
    }

    public class AppIdentity : GenericIdentity
    {
        public AppIdentity(string userName, AuthTicketData data) : base(userName, "Forms")
        {
            Data = data ?? new AuthTicketData();
        }

        public AuthTicketData Data { get; private set; }
    }

    /// <summary>Authenticated user with the permission set of the role.</summary>
    public class AppPrincipal : IPrincipal
    {
        private readonly ISet<string> _permissions;

        public AppPrincipal(AppIdentity identity, ISet<string> permissions)
        {
            Identity = identity;
            AppIdentity = identity;
            _permissions = permissions ?? new HashSet<string>();
        }

        public IIdentity Identity { get; private set; }
        public AppIdentity AppIdentity { get; private set; }

        public int UserID { get { return AppIdentity.Data.UserID; } }
        public int RoleID { get { return AppIdentity.Data.RoleID; } }
        public string RoleName { get { return AppIdentity.Data.RoleName; } }
        public string FullName { get { return AppIdentity.Data.FullName; } }
        public int? FarmID { get { return AppIdentity.Data.FarmID; } }
        public bool MustChangePassword { get { return AppIdentity.Data.MustChangePassword; } }

        public bool IsInRole(string role)
        {
            return string.Equals(RoleName, role, StringComparison.OrdinalIgnoreCase);
        }

        public bool HasPermission(string code)
        {
            return !string.IsNullOrEmpty(code) && _permissions.Contains(code);
        }

        public bool HasAnyPermission(IEnumerable<string> codes)
        {
            return codes != null && codes.Any(HasPermission);
        }
    }

    public static class PrincipalExtensions
    {
        public static AppPrincipal AsApp(this IPrincipal principal)
        {
            return principal as AppPrincipal;
        }

        /// <summary>For views: User.Can(PermissionCodes.FarmEdit)</summary>
        public static bool Can(this IPrincipal principal, string permissionCode)
        {
            var p = principal as AppPrincipal;
            return p != null && p.HasPermission(permissionCode);
        }
    }

    /// <summary>Creates and reads the forms-authentication cookie.</summary>
    public static class AuthCookie
    {
        public static void Issue(HttpContextBase context, UserCredential user, bool mustChangePassword)
        {
            var data = new AuthTicketData
            {
                UserID = user.UserID,
                RoleID = user.RoleID,
                RoleName = user.RoleName,
                FullName = user.FullName,
                FarmID = user.FarmID,
                MustChangePassword = mustChangePassword
            };
            Issue(context, user.UserName, data);
        }

        public static void Issue(HttpContextBase context, string userName, AuthTicketData data)
        {
            var now = DateTime.Now;
            var ticket = new FormsAuthenticationTicket(2, userName, now, now.Add(FormsAuthentication.Timeout),
                false, JsonConvert.SerializeObject(data), FormsAuthentication.FormsCookiePath);

            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticket))
            {
                HttpOnly = true,
                Secure = FormsAuthentication.RequireSSL,
                Path = FormsAuthentication.FormsCookiePath
            };
            if (!string.IsNullOrEmpty(FormsAuthentication.CookieDomain)) cookie.Domain = FormsAuthentication.CookieDomain;
            context.Response.Cookies.Add(cookie);
        }

        public static AuthTicketData Read(FormsAuthenticationTicket ticket)
        {
            if (ticket == null || string.IsNullOrEmpty(ticket.UserData)) return null;
            try
            {
                return JsonConvert.DeserializeObject<AuthTicketData>(ticket.UserData);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static void SignOut(HttpContextBase context)
        {
            FormsAuthentication.SignOut();
            if (context.Session != null) context.Session.Abandon();
            var expired = new HttpCookie(FormsAuthentication.FormsCookieName, string.Empty)
            {
                Expires = DateTime.Now.AddYears(-1),
                HttpOnly = true,
                Path = FormsAuthentication.FormsCookiePath
            };
            context.Response.Cookies.Add(expired);
        }
    }

    /// <summary>
    /// Allows access when the user holds at least one of the permission codes.
    /// Authenticated users without the permission get the Access Denied page (403),
    /// anonymous users are sent to the login page.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class PermissionAuthorizeAttribute : AuthorizeAttribute
    {
        private readonly string[] _codes;

        public PermissionAuthorizeAttribute(params string[] permissionCodes)
        {
            _codes = permissionCodes ?? new string[0];
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            var principal = httpContext.User as AppPrincipal;
            if (principal == null || !principal.Identity.IsAuthenticated) return false;
            return _codes.Length == 0 || principal.HasAnyPermission(_codes);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            AccessDenied.Handle(filterContext);
        }
    }

    internal static class AccessDenied
    {
        public static void Handle(AuthorizationContext filterContext)
        {
            var http = filterContext.HttpContext;
            if (http.User == null || !http.User.Identity.IsAuthenticated)
            {
                if (http.Request.IsAjaxRequest())
                {
                    filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Unauthorized, "Session expired");
                    return;
                }
                filterContext.Result = new HttpUnauthorizedResult(); // forms auth redirects to login
                return;
            }

            if (http.Request.IsAjaxRequest())
            {
                filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Forbidden, "Access denied");
                return;
            }
            filterContext.Result = new ViewResult { ViewName = "~/Views/Account/AccessDenied.cshtml" };
            http.Response.StatusCode = 403;
            http.Response.TrySkipIisCustomErrors = true;
        }
    }

    /// <summary>Forces users flagged MustChangePassword to the change-password screen.</summary>
    public class MustChangePasswordFilter : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext.IsChildAction) return;
            var principal = filterContext.HttpContext.User as AppPrincipal;
            if (principal == null || !principal.MustChangePassword) return;

            var controller = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            if (string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase)) return;

            filterContext.Result = new RedirectToRouteResult(new System.Web.Routing.RouteValueDictionary
            {
                { "controller", "Account" }, { "action", "ChangePassword" }, { "area", "" }
            });
        }
    }

    /// <summary>IUserContext backed by the current HTTP request.</summary>
    public class HttpUserContext : IUserContext
    {
        private readonly HttpContextBase _http;

        public HttpUserContext() : this(HttpContext.Current != null ? new HttpContextWrapper(HttpContext.Current) : null) { }

        public HttpUserContext(HttpContextBase http)
        {
            _http = http;
        }

        private AppPrincipal Principal
        {
            get { return _http == null ? null : _http.User as AppPrincipal; }
        }

        public int UserID { get { return Principal != null ? Principal.UserID : 0; } }
        public string UserName { get { return Principal != null ? Principal.Identity.Name : null; } }
        public int RoleID { get { return Principal != null ? Principal.RoleID : 0; } }
        public int? FarmID { get { return Principal != null ? Principal.FarmID : null; } }
        public bool IsAuthenticated { get { return Principal != null && Principal.Identity.IsAuthenticated; } }

        public string IPAddress
        {
            get
            {
                if (_http == null || _http.Request == null) return null;
                var trustProxy = string.Equals(System.Configuration.ConfigurationManager.AppSettings["TrustForwardedFor"], "true",
                    StringComparison.OrdinalIgnoreCase);
                if (trustProxy)
                {
                    var forwarded = _http.Request.Headers["X-Forwarded-For"];
                    if (!string.IsNullOrWhiteSpace(forwarded))
                    {
                        var first = forwarded.Split(',')[0].Trim();
                        if (first.Length <= 50) return first;
                    }
                }
                return _http.Request.UserHostAddress;
            }
        }
    }
}
