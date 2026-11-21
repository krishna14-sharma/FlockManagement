using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using FlockManagement.Business.Interfaces;
using FlockManagement.Common.Constants;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Helpers;
using FlockManagement.Web.Infrastructure;
using FlockManagement.Web.ViewModels;

namespace FlockManagement.Web.Controllers
{
    // ================================================================ ANALYSIS
    public class AnalysisController : FlockScreenController
    {
        private readonly IAnalysisService _analysis;
        private readonly IWeeklyService _weekly;
        private readonly IBirdTypeService _birdTypes;

        public AnalysisController(IAnalysisService analysis, IWeeklyService weekly, IBirdTypeService birdTypes,
            IFarmService farms, IHouseService houses, IFlockService flocks)
            : base(farms, houses, flocks)
        {
            _analysis = analysis;
            _weekly = weekly;
            _birdTypes = birdTypes;
        }

        /// <summary>Flock Weight Management Analysis - standard vs actual for one week, all sections.</summary>
        [PermissionAuthorize(PermissionCodes.AnalysisWeekly)]
        public ActionResult Weekly(int? id, int? farmId, int? houseId, int? flockId, int? weekNo)
        {
            return WeekScreen("Weekly", id, farmId, houseId, flockId, weekNo);
        }

        /// <summary>Flock Production Performance Analysis - the 24-row production sheet for one week.</summary>
        [PermissionAuthorize(PermissionCodes.AnalysisProduction)]
        public ActionResult Production(int? id, int? farmId, int? houseId, int? flockId, int? weekNo)
        {
            return WeekScreen("Production", id, farmId, houseId, flockId, weekNo);
        }

        private ActionResult WeekScreen(string action, int? id, int? farmId, int? houseId, int? flockId, int? weekNo)
        {
            if (!id.HasValue && flockId.HasValue)
            {
                var list = _weekly.Search(new WeeklySearchCriteria { FlockID = flockId, PageSize = 200 }).Items;
                var pick = weekNo.HasValue ? list.FirstOrDefault(w => w.WeekNo == weekNo.Value) : list.OrderByDescending(w => w.WeekNo).FirstOrDefault();
                if (pick != null) id = pick.FlockWeeklyID;
            }

            var model = new WeeklyAnalysisViewModel { Picker = Picker(action, farmId, houseId, flockId, false), CanEdit = CurrentUser.HasAnyPermission(PermissionCodes.AnyWeeklyEntry) };
            if (id.HasValue)
            {
                model.Analysis = _analysis.Weekly(id.Value);
                if (model.Analysis == null) return NotFoundView();
                var w = model.Analysis.Week;
                model.Picker = Picker(action, w.FarmID, null, w.FlockID, false);
                var weeks = _weekly.Search(new WeeklySearchCriteria { FlockID = w.FlockID, PageSize = 200 }).Items.OrderBy(x => x.WeekNo).ToList();
                model.Picker.ShowWeek = true;
                model.Picker.WeekNo = w.WeekNo;
                model.Picker.Weeks = weeks.Select(x => new SelectListItem { Value = x.WeekNo.ToString(), Text = "Week " + x.WeekNo, Selected = x.WeekNo == w.WeekNo }).ToList();
                var idx = weeks.FindIndex(x => x.FlockWeeklyID == w.FlockWeeklyID);
                model.PrevWeekID = idx > 0 ? weeks[idx - 1].FlockWeeklyID : (int?)null;
                model.NextWeekID = idx >= 0 && idx < weeks.Count - 1 ? weeks[idx + 1].FlockWeeklyID : (int?)null;
            }
            return View(action, model);
        }

        /// <summary>All active flocks x key parameters, coloured by status.</summary>
        [PermissionAuthorize(PermissionCodes.AnalysisStdVsActual)]
        public ActionResult StandardVsActual(int? farmId, int? houseId, int? birdTypeId)
        {
            return View(new StandardVsActualViewModel
            {
                Picker = Picker("StandardVsActual", farmId, houseId, null),
                BirdTypeID = birdTypeId,
                BirdTypes = SelectLists.From(_birdTypes.Lookup(), birdTypeId, "All bird types"),
                Flocks = _analysis.Dashboard(new DashboardFilter { FarmID = farmId, HouseID = houseId, BirdTypeID = birdTypeId }).Flocks
            });
        }

        [PermissionAuthorize(PermissionCodes.AnalysisWeight)]
        public ActionResult Weight(int? farmId, int? houseId, int? flockId) { return Focus("weight", "Body Weight Analysis", "bi-graph-up", farmId, houseId, flockId); }

        [PermissionAuthorize(PermissionCodes.AnalysisFeed)]
        public ActionResult Feed(int? farmId, int? houseId, int? flockId) { return Focus("feed", "Feed Analysis", "bi-basket3", farmId, houseId, flockId); }

        [PermissionAuthorize(PermissionCodes.AnalysisNutrition)]
        public ActionResult Nutrition(int? farmId, int? houseId, int? flockId) { return Focus("nutrition", "Nutritional Analysis", "bi-droplet-half", farmId, houseId, flockId); }

        [PermissionAuthorize(PermissionCodes.AnalysisUniformity)]
        public ActionResult Uniformity(int? farmId, int? houseId, int? flockId) { return Focus("uniformity", "Uniformity Analysis", "bi-distribute-vertical", farmId, houseId, flockId); }

        [PermissionAuthorize(PermissionCodes.AnalysisMortality)]
        public ActionResult Mortality(int? farmId, int? houseId, int? flockId) { return Focus("mortality", "Mortality Analysis", "bi-activity", farmId, houseId, flockId); }

        private ActionResult Focus(string area, string title, string icon, int? farmId, int? houseId, int? flockId)
        {
            var model = new FocusViewModel { Area = area, Title = title, Icon = icon, Picker = Picker(area, farmId, houseId, flockId, false) };
            if (flockId.HasValue)
            {
                model.Flock = Flocks.Get(flockId.Value);
                if (model.Flock == null) return NotFoundView();
                model.Points = _analysis.Trend(flockId.Value);
                model.Weeks = model.Points.Select(p => p.WeekNo).Distinct().OrderBy(w => w).ToList();
                if (area == "uniformity")
                    foreach (var p in model.Points.Where(p => p.ParameterCode == "UNIFORMITY"))
                        model.UniformityGrades[p.WeekNo] = _analysis.UniformityGrade(p.ActualValue);
            }
            else
            {
                model.AllFlocks = _analysis.Dashboard(new DashboardFilter { FarmID = farmId, HouseID = houseId }).Flocks;
            }
            return View("Focus", model);
        }

        /// <summary>Age-wise trend of any combination of parameters.</summary>
        [PermissionAuthorize(PermissionCodes.AnalysisTrend)]
        public ActionResult Trend(int? farmId, int? houseId, int? flockId, string[] p)
        {
            var model = new TrendViewModel { Picker = Picker("Trend", farmId, houseId, flockId, false) };
            model.Selected = (p == null || p.Length == 0) ? new List<string> { "BODY_WEIGHT", "FEED_INTAKE", "UNIFORMITY", "MORTALITY" } : p.ToList();
            if (flockId.HasValue)
            {
                model.Flock = Flocks.Get(flockId.Value);
                if (model.Flock == null) return NotFoundView();
                model.Points = _analysis.Trend(flockId.Value);
                model.Available = model.Points.GroupBy(x => new { x.ParameterCode, x.ParameterName, x.SortOrder })
                    .OrderBy(g => g.Key.SortOrder).Select(g => new KeyValuePair<string, string>(g.Key.ParameterCode, g.Key.ParameterName)).ToList();
            }
            return View(model);
        }

        [PermissionAuthorize(PermissionCodes.AnalysisCompare)]
        public ActionResult Comparison(int[] flockIds, int? ageWeek)
        {
            var ids = (flockIds ?? new int[0]).Distinct().Take(12).ToList();
            var model = new ComparisonViewModel
            {
                FlockIDs = ids,
                AgeWeek = ageWeek,
                AvailableFlocks = Flocks.Search(new FlockSearchCriteria { PageSize = 200 }).Items
                    .Select(f => new LookupItem(f.FlockID, f.FlockNumber + " - " + f.BirdTypeName + " - " + f.FarmName + (f.Status == "Active" ? "" : " (" + f.Status + ")"))).ToList()
            };
            if (ids.Count == 0)
                model.FlockIDs = Flocks.ActiveLookup(null, null).Take(4).Select(x => x.Id).ToList();
            model.Rows = _analysis.Comparison(model.FlockIDs, ageWeek);
            return View(model);
        }

        [PermissionAuthorize(PermissionCodes.ExceptionView)]
        public ActionResult Exceptions(int? farmId, int? houseId, int? birdTypeId, bool criticalOnly = false)
        {
            return View(new ExceptionsViewModel
            {
                FarmID = farmId,
                HouseID = houseId,
                BirdTypeID = birdTypeId,
                CriticalOnly = criticalOnly,
                Farms = SelectLists.From(Farms.Lookup(), farmId, "All farms"),
                Houses = SelectLists.From(farmId.HasValue ? Houses.Lookup(farmId) : new List<LookupItem>(), houseId, "All houses"),
                BirdTypes = SelectLists.From(_birdTypes.Lookup(), birdTypeId, "All bird types"),
                Rows = _analysis.Exceptions(farmId, houseId, birdTypeId, criticalOnly)
            });
        }

        /// <summary>JSON trend for charts (dashboard and analysis screens).</summary>
        [HttpGet]
        public JsonResult TrendData(int flockId)
        {
            if (!Can(PermissionCodes.DashboardView) && !Can(PermissionCodes.AnalysisTrend) && !Can(PermissionCodes.AnalysisWeekly))
                return Json(new { error = "Access denied" }, JsonRequestBehavior.AllowGet);
            var points = _analysis.Trend(flockId);
            return Json(TrendJson(points), JsonRequestBehavior.AllowGet);
        }

        public static object TrendJson(IEnumerable<TrendPoint> points)
        {
            var list = points.ToList();
            return new
            {
                weeks = list.Select(p => p.WeekNo).Distinct().OrderBy(w => w).ToList(),
                series = list.GroupBy(p => p.ParameterCode).Select(g => new
                {
                    code = g.Key,
                    name = g.First().ParameterName,
                    group = g.First().ParameterGroup,
                    unit = g.First().UnitCode,
                    dec = g.First().DecimalPlaces,
                    points = g.OrderBy(p => p.WeekNo).Select(p => new
                    {
                        w = p.WeekNo,
                        id = p.FlockWeeklyID,
                        s = p.StandardValue,
                        mn = p.MinValue,
                        mx = p.MaxValue,
                        a = p.ActualValue,
                        v = p.VariancePct,
                        st = p.StatusCode
                    })
                })
            };
        }
    }

    // ================================================================ ALERT CENTER
    [PermissionAuthorize(PermissionCodes.AlertView)]
    public class AlertController : BaseController
    {
        private readonly IAlertService _alerts;
        private readonly IFarmService _farms;
        private readonly IFlockService _flocks;

        public AlertController(IAlertService alerts, IFarmService farms, IFlockService flocks)
        {
            _alerts = alerts;
            _farms = farms;
            _flocks = flocks;
        }

        public ActionResult Index(string status = "Open", string severity = null, string alertType = null, int? farmId = null, int? flockId = null,
            DateTime? fromDate = null, DateTime? toDate = null, int page = 1)
        {
            return View(new AlertListViewModel
            {
                Status = status,
                Severity = severity,
                AlertType = alertType,
                FarmID = farmId,
                FlockID = flockId,
                FromDate = fromDate,
                ToDate = toDate,
                Result = _alerts.Search(new AlertSearchCriteria
                {
                    Status = status == "All" ? null : status,
                    Severity = string.IsNullOrEmpty(severity) ? null : severity,
                    AlertType = string.IsNullOrEmpty(alertType) ? null : alertType,
                    FarmID = farmId,
                    FlockID = flockId,
                    FromDate = fromDate,
                    ToDate = toDate,
                    PageNo = page
                }),
                Farms = SelectLists.From(_farms.Lookup(), farmId, "All farms"),
                Flocks = SelectLists.From(_flocks.ActiveLookup(farmId, null), flockId, "All flocks"),
                CanAcknowledge = Can(PermissionCodes.AlertAck)
            });
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.AlertAck)]
        public ActionResult Acknowledge(long alertId, string actionTaken, bool close = false, string returnUrl = null)
        {
            var r = _alerts.Acknowledge(alertId, actionTaken, close);
            if (r.Success) Success(r.Message); else Error(r.Message);
            return RedirectToLocal(returnUrl ?? Url.Action("Index"));
        }
    }

    // ================================================================ REPORTS
    [PermissionAuthorize(PermissionCodes.ReportView)]
    public class ReportController : BaseController
    {
        private readonly IReportService _reports;
        private readonly IFarmService _farms;
        private readonly IHouseService _houses;
        private readonly IBirdTypeService _birdTypes;
        private readonly IFlockService _flocks;
        private readonly ILotService _lots;

        public ReportController(IReportService reports, IFarmService farms, IHouseService houses, IBirdTypeService birdTypes,
            IFlockService flocks, ILotService lots)
        {
            _reports = reports;
            _farms = farms;
            _houses = houses;
            _birdTypes = birdTypes;
            _flocks = flocks;
            _lots = lots;
        }

        public ActionResult Index(ReportViewModel model)
        {
            model = model ?? new ReportViewModel();
            ModelState.Clear();
            if (!string.IsNullOrEmpty(model.ReportType))
            {
                try
                {
                    model.Result = _reports.Run(model.ToRequest(), Describe(model));
                }
                catch (BusinessException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
            }
            Prepare(model);
            return View(model);
        }

        [PermissionAuthorize(PermissionCodes.ReportExport)]
        public ActionResult Export(ReportViewModel model, string format)
        {
            try
            {
                var result = _reports.Run(model.ToRequest(), Describe(model));
                var file = _reports.Export(result, format);
                return File(file.Content, file.ContentType, file.FileName);
            }
            catch (BusinessException ex)
            {
                Error(ex.Message);
                return RedirectToAction("Index");
            }
        }

        private IDictionary<string, string> Describe(ReportViewModel m)
        {
            Func<IEnumerable<LookupItem>, int?, string> name = (items, id) =>
                id.HasValue ? items.Where(i => i.Id == id.Value).Select(i => i.Text).FirstOrDefault() : null;
            var d = new Dictionary<string, string>();
            d["Farm"] = name(_farms.Lookup(), m.FarmID);
            d["House"] = m.FarmID.HasValue ? name(_houses.Lookup(m.FarmID), m.HouseID) : null;
            d["Bird Type"] = name(_birdTypes.Lookup(), m.BirdTypeID);
            d["Flock"] = m.FlockID.HasValue ? (_flocks.Get(m.FlockID.Value) ?? new Flock()).FlockNumber : null;
            d["Lot"] = name(_lots.Lookup(), m.LotID);
            d["Weeks"] = m.FromWeek.HasValue || m.ToWeek.HasValue ? (m.FromWeek.HasValue ? m.FromWeek.ToString() : "first") + " to " + (m.ToWeek.HasValue ? m.ToWeek.ToString() : "last") : null;
            d["Dates"] = m.FromDate.HasValue || m.ToDate.HasValue
                ? (m.FromDate.HasValue ? m.FromDate.Value.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture) : "start") + " to " +
                  (m.ToDate.HasValue ? m.ToDate.Value.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture) : "today")
                : null;
            d["Flocks"] = m.ActiveOnly ? "Active only" : null;
            return d;
        }

        private void Prepare(ReportViewModel m)
        {
            m.CanExport = Can(PermissionCodes.ReportExport);
            m.ReportTypes = ReportCatalog.Types.Select(t => new SelectListItem { Value = t.Key, Text = t.Value, Selected = t.Key == m.ReportType }).ToList();
            m.Farms = SelectLists.From(_farms.Lookup(), m.FarmID, "All farms");
            m.Houses = SelectLists.From(m.FarmID.HasValue ? _houses.Lookup(m.FarmID) : new List<LookupItem>(), m.HouseID, "All houses");
            m.BirdTypes = SelectLists.From(_birdTypes.Lookup(), m.BirdTypeID, "All bird types");
            m.Flocks = SelectLists.From(_flocks.Search(new FlockSearchCriteria { FarmID = m.FarmID, PageSize = 200 }).Items
                .Select(f => new LookupItem(f.FlockID, f.FlockNumber + (f.Status == "Active" ? "" : " (" + f.Status + ")"))), m.FlockID, "All flocks");
            m.Lots = SelectLists.From(_lots.Lookup(), m.LotID, "All lots");
        }
    }

    // ================================================================ CONFIGURATION
    [PermissionAuthorize(PermissionCodes.ConfigEdit)]
    public class ConfigurationController : BaseController
    {
        private readonly IConfigurationService _config;
        private readonly ISettingsService _settings;

        public ConfigurationController(IConfigurationService config, ISettingsService settings)
        {
            _config = config;
            _settings = settings;
        }

        public ActionResult Index(string tab = "tolerance")
        {
            return View(new ConfigurationViewModel { Config = _config.Load(), Tab = tab, Statuses = _settings.Statuses() });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Tolerances(List<ToleranceSetting> rows) { return Done(_config.SaveTolerances(rows), "tolerance"); }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Weights(List<ScoreWeight> rows) { return Done(_config.SaveWeights(rows), "score"); }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Bands(List<ScoreBandSetting> rows) { return Done(_config.SaveBands(rows), "score"); }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Settings(List<SystemSetting> rows) { return Done(_config.SaveSettings(rows), "settings"); }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Modules(List<ModuleFlag> rows) { return Done(_config.SaveModules(rows), "modules"); }

        private ActionResult Done(OperationResult r, string tab)
        {
            if (r.Success) Success(r.Message); else Error(r.Message);
            return RedirectToAction("Index", new { tab });
        }
    }
}
