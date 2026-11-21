using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Mvc;
using FlockManagement.Business.Interfaces;
using FlockManagement.Common.Constants;
using FlockManagement.Common.Entities;
using FlockManagement.Web.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FlockManagement.Web.Controllers
{
    /// <summary>JSON result serialised with Newtonsoft (camelCase, ISO dates).</summary>
    public class NewtonsoftJsonResult : ActionResult
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            DateFormatString = "yyyy-MM-ddTHH:mm:ss",
            NullValueHandling = NullValueHandling.Include,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        public object Data { get; set; }
        public int StatusCode { get; set; }

        public NewtonsoftJsonResult(object data, int statusCode = 200)
        {
            Data = data;
            StatusCode = statusCode;
        }

        public override void ExecuteResult(ControllerContext context)
        {
            var response = context.HttpContext.Response;
            response.StatusCode = StatusCode;
            response.TrySkipIisCustomErrors = true;
            response.ContentType = "application/json";
            response.ContentEncoding = Encoding.UTF8;
            response.Write(JsonConvert.SerializeObject(Data, Settings));
        }
    }

    /// <summary>
    /// Read-only JSON API (v1) for a mobile app or integrations (ERP, feed mill, IoT scales).
    /// Uses the same services and permission rules as the screens. Authentication is the
    /// forms cookie today; add a token handler (e.g. OWIN JWT) for native apps - controllers stay as they are.
    /// </summary>
    [PermissionAuthorize(PermissionCodes.ApiAccess)]
    [RoutePrefix("api/v1")]
    public class ApiController : BaseController
    {
        private readonly IFlockService _flocks;
        private readonly IAnalysisService _analysis;
        private readonly IAlertService _alerts;
        private readonly IWeeklyService _weekly;

        public ApiController(IFlockService flocks, IAnalysisService analysis, IAlertService alerts, IWeeklyService weekly)
        {
            _flocks = flocks;
            _analysis = analysis;
            _alerts = alerts;
            _weekly = weekly;
        }

        [HttpGet, Route("flocks")]
        public ActionResult Flocks(int? farmId, string status = "Active", int page = 1, int pageSize = 50)
        {
            var r = _flocks.Search(new FlockSearchCriteria { FarmID = farmId, Status = status == "All" ? null : status, PageNo = page, PageSize = Math.Min(200, pageSize) });
            return new NewtonsoftJsonResult(new { r.TotalCount, r.PageNo, r.PageSize, items = r.Items.Select(f => new
            {
                f.FlockID, f.FlockNumber, f.LotNumber, f.BirdTypeName, f.BreedName, f.FarmName, f.HouseName, f.HatchDate, f.PlacementDate,
                f.CurrentAge, f.CurrentFemaleCount, f.CurrentMaleCount, f.TotalBirdCount, f.Status
            }) });
        }

        [HttpGet, Route("flocks/{id:int}")]
        public ActionResult Flock(int id)
        {
            var f = _flocks.Get(id);
            return f == null ? NotFoundJson() : new NewtonsoftJsonResult(f);
        }

        [HttpGet, Route("flocks/{id:int}/trend")]
        public ActionResult Trend(int id)
        {
            if (_flocks.Get(id) == null) return NotFoundJson();
            return new NewtonsoftJsonResult(AnalysisController.TrendJson(_analysis.Trend(id)));
        }

        [HttpGet, Route("weekly/{id:int}")]
        public ActionResult Weekly(int id)
        {
            var a = _analysis.Weekly(id);
            if (a == null) return NotFoundJson();
            return new NewtonsoftJsonResult(new { week = a.Week, parameters = a.Parameters, uniformityGrade = a.UniformityGrade });
        }

        [HttpGet, Route("dashboard")]
        public ActionResult Dashboard(int? farmId, int? birdTypeId)
        {
            return new NewtonsoftJsonResult(_analysis.Dashboard(new DashboardFilter { FarmID = farmId, BirdTypeID = birdTypeId }));
        }

        [HttpGet, Route("exceptions")]
        public ActionResult Exceptions(int? farmId, bool criticalOnly = false)
        {
            return new NewtonsoftJsonResult(_analysis.Exceptions(farmId, null, null, criticalOnly));
        }

        [HttpGet, Route("alerts")]
        public ActionResult Alerts(string status = "Open", int page = 1)
        {
            return new NewtonsoftJsonResult(_alerts.Search(new AlertSearchCriteria { Status = status == "All" ? null : status, PageNo = page, PageSize = 100 }));
        }

        private ActionResult NotFoundJson()
        {
            return new NewtonsoftJsonResult(new { error = "Not found" }, (int)HttpStatusCode.NotFound);
        }
    }
}
