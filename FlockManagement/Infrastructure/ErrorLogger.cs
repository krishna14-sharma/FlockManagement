using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Hosting;

namespace FlockManagement.Web.Infrastructure
{
    /// <summary>
    /// Writes unhandled errors to App_Data\Logs\error-yyyyMMdd.log (one file per day).
    /// The IIS application pool identity needs modify rights on App_Data\Logs.
    /// </summary>
    public static class ErrorLogger
    {
        private static readonly object Sync = new object();

        public static void Log(Exception ex, HttpContextBase context = null)
        {
            if (ex == null) return;
            try
            {
                var folder = HostingEnvironment.MapPath("~/App_Data/Logs");
                if (string.IsNullOrEmpty(folder)) return;
                Directory.CreateDirectory(folder);

                var sb = new StringBuilder();
                sb.AppendLine(new string('-', 80));
                sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                if (context != null && context.Request != null)
                {
                    sb.AppendLine("URL : " + context.Request.HttpMethod + " " + context.Request.RawUrl);
                    sb.AppendLine("User: " + (context.User != null && context.User.Identity.IsAuthenticated ? context.User.Identity.Name : "(anonymous)"));
                    sb.AppendLine("IP  : " + context.Request.UserHostAddress);
                }
                sb.AppendLine(ex.ToString());

                var file = Path.Combine(folder, "error-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");
                lock (Sync)
                {
                    File.AppendAllText(file, sb.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception logEx)
            {
                // Never let logging break the request.
                Trace.TraceError("ErrorLogger failed: " + logEx.Message + Environment.NewLine + ex);
            }
        }
    }
}
