using System;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using FlockManagement.Business.Interfaces;
using FlockManagement.Web.Infrastructure;

namespace FlockManagement.Web
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            MvcHandler.DisableMvcResponseHeader = true;
            AntiForgeryConfig.SuppressXFrameOptionsHeader = false;
            // Our principal carries a Name claim (GenericIdentity); bind anti-forgery tokens to it.
            AntiForgeryConfig.UniqueClaimTypeIdentifier = System.Security.Claims.ClaimTypes.Name;

            AreaRegistration.RegisterAllAreas();
            DependencyConfig.Register();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            // Only Razor (C#) views - faster view lookup
            ViewEngines.Engines.Clear();
            ViewEngines.Engines.Add(new RazorViewEngine());
        }

        /// <summary>
        /// Rebuilds the principal from the forms ticket on every request and attaches the
        /// role's permission set (cached in memory by PermissionService).
        /// </summary>
        protected void Application_PostAuthenticateRequest(object sender, EventArgs e)
        {
            var cookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (cookie == null || string.IsNullOrEmpty(cookie.Value)) return;

            FormsAuthenticationTicket ticket;
            try
            {
                ticket = FormsAuthentication.Decrypt(cookie.Value);
            }
            catch (Exception)
            {
                ticket = null;
            }
            if (ticket == null || ticket.Expired) return;

            var data = AuthCookie.Read(ticket);
            if (data == null || data.UserID <= 0) return;

            var permissions = DependencyConfig.Container.Resolve<IPermissionService>().GetPermissionCodes(data.RoleID);
            var principal = new AppPrincipal(new AppIdentity(ticket.Name, data), permissions);
            Context.User = principal;
            System.Threading.Thread.CurrentPrincipal = principal;
        }

        protected void Application_PreSendRequestHeaders(object sender, EventArgs e)
        {
            var headers = Response.Headers;
            headers.Remove("Server");
            headers.Remove("X-AspNet-Version");
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var ex = Server.GetLastError();
            var http = ex as HttpException;
            if (http != null && http.GetHttpCode() == 404) return; // handled by customErrors / NotFound page
            ErrorLogger.Log(ex, new HttpContextWrapper(Context));
        }
    }
}
