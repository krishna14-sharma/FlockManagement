using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Web;
using System.Web.Mvc;
using FlockManagement.Business.Interfaces;
using FlockManagement.Common.Helpers;

namespace FlockManagement.Web.Infrastructure
{
    public static class HtmlExtensions
    {
        /// <summary>
        /// Renders a status badge. Label and CSS class come from tblStatusConfig (centralised),
        /// so no view hard-codes colours.
        /// </summary>
        public static IHtmlString StatusBadge(this HtmlHelper html, string statusCode, string overrideText = null)
        {
            var settings = DependencyResolver.Current.GetService<ISettingsService>();
            var def = settings.Status(statusCode);
            var tag = new TagBuilder("span");
            tag.AddCssClass("badge status-badge " + def.BadgeCssClass);
            tag.SetInnerText(overrideText ?? def.StatusName);
            return new MvcHtmlString(tag.ToString());
        }

        /// <summary>Status definitions as a JS object literal: { CODE: { name, css, color } } (for fwm.js).</summary>
        public static IHtmlString StatusJson(this HtmlHelper html)
        {
            var settings = DependencyResolver.Current.GetService<ISettingsService>();
            var map = new Dictionary<string, object>();
            foreach (var s in settings.Statuses())
                map[s.StatusCode] = new { name = s.StatusName, css = s.BadgeCssClass, color = s.ColorHex };
            return new MvcHtmlString(Newtonsoft.Json.JsonConvert.SerializeObject(map));
        }

        /// <summary>Badge with a value and the status colour, e.g. "1,850 g" on green.</summary>
        public static IHtmlString StatusValue(this HtmlHelper html, string statusCode, string text)
        {
            var settings = DependencyResolver.Current.GetService<ISettingsService>();
            var def = settings.Status(statusCode);
            var tag = new TagBuilder("span");
            tag.AddCssClass("badge status-badge " + def.BadgeCssClass);
            tag.MergeAttribute("title", def.StatusName);
            tag.SetInnerText(text);
            return new MvcHtmlString(tag.ToString());
        }

        /// <summary>Number formatted with fixed decimals, "-" when null.</summary>
        public static string Num(this HtmlHelper html, decimal? value, int decimals = 0)
        {
            return value.HasValue ? value.Value.ToString("N" + Math.Max(0, Math.Min(4, decimals))) : "-";
        }

        /// <summary>Signed percentage, e.g. "+3.20%".</summary>
        public static string Pct(this HtmlHelper html, decimal? value, int decimals = 2)
        {
            if (!value.HasValue) return "-";
            return (value.Value > 0 ? "+" : string.Empty) + value.Value.ToString("N" + decimals) + "%";
        }

        /// <summary>Open alert count for the top bar (one query per request).</summary>
        public static int OpenAlertCount(this HtmlHelper html)
        {
            var items = html.ViewContext.HttpContext.Items;
            if (items["fwm.alerts"] is int) return (int)items["fwm.alerts"];
            var count = 0;
            try
            {
                count = DependencyResolver.Current.GetService<IAlertService>().OpenCount();
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex);
            }
            items["fwm.alerts"] = count;
            return count;
        }

        /// <summary>Active / inactive badge for master records.</summary>
        public static IHtmlString ActiveBadge(this HtmlHelper html, bool isActive)
        {
            var tag = new TagBuilder("span");
            tag.AddCssClass("badge " + (isActive ? "status-within" : "status-nodata"));
            tag.SetInnerText(isActive ? "Active" : "Inactive");
            return new MvcHtmlString(tag.ToString());
        }

        /// <summary>Flock status badge (Active / Closed / Depleted / Transferred).</summary>
        public static IHtmlString FlockStatusBadge(this HtmlHelper html, string status)
        {
            string css;
            switch (status)
            {
                case "Active": css = "status-within"; break;
                case "Transferred": css = "status-info"; break;
                case "Depleted": css = "status-warning"; break;
                default: css = "status-nodata"; break;
            }
            var tag = new TagBuilder("span");
            tag.AddCssClass("badge " + css);
            tag.SetInnerText(status ?? "-");
            return new MvcHtmlString(tag.ToString());
        }

        /// <summary>Bootstrap 5 pagination that keeps all current query-string filters.</summary>
        public static IHtmlString Pager<T>(this HtmlHelper html, PagedResult<T> result, string pageParam = "page")
        {
            if (result == null || result.TotalCount == 0) return MvcHtmlString.Empty;

            var request = html.ViewContext.HttpContext.Request;
            var path = request.Path;
            var first = (result.PageNo - 1) * result.PageSize + 1;
            var last = Math.Min(result.PageNo * result.PageSize, result.TotalCount);

            var sb = new StringBuilder();
            sb.Append("<div class=\"d-flex flex-column flex-sm-row justify-content-between align-items-center gap-2 mt-3\">");
            sb.AppendFormat("<div class=\"text-muted small\">Showing {0:N0}-{1:N0} of {2:N0}</div>", first, last, result.TotalCount);

            if (result.TotalPages > 1)
            {
                sb.Append("<nav aria-label=\"Pagination\"><ul class=\"pagination pagination-sm mb-0\">");
                AppendPage(sb, request.QueryString, path, pageParam, result.PageNo - 1, "&laquo;", !result.HasPrevious, false);

                var start = Math.Max(1, result.PageNo - 2);
                var end = Math.Min(result.TotalPages, start + 4);
                start = Math.Max(1, end - 4);
                for (var i = start; i <= end; i++)
                    AppendPage(sb, request.QueryString, path, pageParam, i, i.ToString(), false, i == result.PageNo);

                AppendPage(sb, request.QueryString, path, pageParam, result.PageNo + 1, "&raquo;", !result.HasNext, false);
                sb.Append("</ul></nav>");
            }
            sb.Append("</div>");
            return new MvcHtmlString(sb.ToString());
        }

        private static void AppendPage(StringBuilder sb, NameValueCollection query, string path, string pageParam,
            int page, string label, bool disabled, bool active)
        {
            var qs = HttpUtility.ParseQueryString(query.ToString());
            qs[pageParam] = page.ToString();
            var href = path + "?" + qs;
            sb.AppendFormat("<li class=\"page-item{0}{1}\"><a class=\"page-link\" href=\"{2}\"{3}>{4}</a></li>",
                disabled ? " disabled" : string.Empty,
                active ? " active" : string.Empty,
                HttpUtility.HtmlAttributeEncode(href),
                active ? " aria-current=\"page\"" : string.Empty,
                label);
        }

        /// <summary>Adds "active" when the current controller matches (sidebar highlighting).</summary>
        public static string ActiveIf(this HtmlHelper html, string controller)
        {
            var current = html.ViewContext.RouteData.Values["controller"] as string;
            return string.Equals(current, controller, StringComparison.OrdinalIgnoreCase) ? "active" : string.Empty;
        }
    }
}
