using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using FlockManagement.Business.Interfaces;
using FlockManagement.Common.Constants;
using FlockManagement.Common.Entities;
using FlockManagement.Web.Infrastructure;
using FlockManagement.Web.ViewModels;

namespace FlockManagement.Web.Controllers
{
    /// <summary>
    /// Weekly Flock Entry - one screen with tabs (population & mortality, body weight & uniformity,
    /// feed, nutrition, summary). The Body Weight / Feed / Nutrition / Mortality menu entries open
    /// the same screen on their tab.
    /// </summary>
    public class WeeklyController : FlockScreenController
    {
        private readonly IWeeklyService _weekly;
        private readonly IFeedTypeService _feedTypes;
        private readonly IFeedFormulaService _formulas;
        private readonly IMortalityReasonService _reasons;
        private readonly IStandardService _standards;

        public WeeklyController(IWeeklyService weekly, IFeedTypeService feedTypes, IFeedFormulaService formulas, IMortalityReasonService reasons,
            IStandardService standards, IFarmService farms, IHouseService houses, IFlockService flocks)
            : base(farms, houses, flocks)
        {
            _weekly = weekly;
            _feedTypes = feedTypes;
            _formulas = formulas;
            _reasons = reasons;
            _standards = standards;
        }

        private bool CanEnter { get { return CurrentUser != null && CurrentUser.HasAnyPermission(PermissionCodes.AnyWeeklyEntry); } }

        [PermissionAuthorize(PermissionCodes.WeeklyView)]
        public ActionResult Index(int? farmId, int? houseId, int? flockId, DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            return View(new WeeklyRegisterViewModel
            {
                Picker = Picker("Index", farmId, houseId, flockId, false),
                FromDate = fromDate,
                ToDate = toDate,
                Result = _weekly.Search(new WeeklySearchCriteria { FarmID = farmId, HouseID = houseId, FlockID = flockId, FromDate = fromDate, ToDate = toDate, PageNo = page }),
                CanEnter = CanEnter
            });
        }

        [PermissionAuthorize(PermissionCodes.WeeklyEntry, PermissionCodes.BodyWeightEntry, PermissionCodes.FeedEntry, PermissionCodes.NutritionEntry, PermissionCodes.MortalityEntry, PermissionCodes.ProductionEntry)]
        public ActionResult Entry(int? farmId, int? houseId, int? flockId, int? weekNo, string tab, int? feedTypeId)
        {
            var model = new WeeklyEntryViewModel
            {
                Picker = Picker("Entry", farmId, houseId, flockId),
                Tab = string.IsNullOrEmpty(tab) ? "population" : tab.ToLowerInvariant()
            };
            if (!string.IsNullOrEmpty(tab)) model.Picker.Extra["tab"] = model.Tab;   // keep the tab the user came in on (menu highlight follows it)

            if (flockId.HasValue)
            {
                model.Data = _weekly.GetEntry(flockId.Value, weekNo, feedTypeId);
                if (model.Data == null) return NotFoundView();
                Fill(model, model.Data);
                PrepareWeeks(model);
            }
            PrepareLists(model);
            return View(model);
        }

        [PermissionAuthorize(PermissionCodes.BodyWeightEntry, PermissionCodes.WeeklyEntry)]
        public ActionResult BodyWeight(int? flockId) { return RedirectToAction("Entry", new { flockId, tab = "weight" }); }

        [PermissionAuthorize(PermissionCodes.FeedEntry, PermissionCodes.WeeklyEntry)]
        public ActionResult Feed(int? flockId) { return RedirectToAction("Entry", new { flockId, tab = "feed" }); }

        [PermissionAuthorize(PermissionCodes.NutritionEntry, PermissionCodes.WeeklyEntry)]
        public ActionResult Nutrition(int? flockId) { return RedirectToAction("Entry", new { flockId, tab = "nutrition" }); }

        [PermissionAuthorize(PermissionCodes.MortalityEntry, PermissionCodes.WeeklyEntry)]
        public ActionResult Mortality(int? flockId) { return RedirectToAction("Entry", new { flockId, tab = "population" }); }

        [PermissionAuthorize(PermissionCodes.ProductionEntry, PermissionCodes.WeeklyEntry)]
        public ActionResult Production(int? flockId) { return RedirectToAction("Entry", new { flockId, tab = "production" }); }

        [HttpPost, ValidateAntiForgeryToken]
        [PermissionAuthorize(PermissionCodes.WeeklyEntry, PermissionCodes.BodyWeightEntry, PermissionCodes.FeedEntry, PermissionCodes.NutritionEntry, PermissionCodes.MortalityEntry, PermissionCodes.ProductionEntry)]
        public ActionResult Save(WeeklyEntryViewModel model)
        {
            WeeklyEntryInput input = null;
            try
            {
                input = model.ToInput();
            }
            catch (FormatException ex)
            {
                ModelState.AddModelError("SamplesFemale", ex.Message);
            }

            if (ModelState.IsValid && input != null)
            {
                var result = _weekly.Save(input);
                if (result.Success)
                {
                    Success(result.Message);
                    return CurrentUser.HasPermission(PermissionCodes.AnalysisWeekly)
                        ? RedirectToAction("Weekly", "Analysis", new { id = result.Data })
                        : RedirectToAction("Entry", new { flockId = model.FlockID, weekNo = model.WeekNo });
                }
                AddErrors(result);
            }

            // redisplay with the user's values
            model.Picker = Picker("Entry", null, null, model.FlockID);
            model.Data = _weekly.GetEntry(model.FlockID, model.WeekNo, model.FeedTypeID);
            if (model.Data == null) return NotFoundView();
            PrepareWeeks(model);
            PrepareLists(model);
            return View("Entry", model);
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.WeeklyDelete)]
        public ActionResult DeleteLast(int flockId)
        {
            var r = _weekly.DeleteLast(flockId);
            if (r.Success) Success(r.Message); else Error(r.Message);
            return RedirectToAction("Entry", new { flockId });
        }

        /// <summary>JSON: standards for a flock / week / feed type (used when the feed type changes).</summary>
        [HttpGet]
        public JsonResult Standards(int flockId, int weekNo, int? feedTypeId)
        {
            if (!CanEnter) return Json(new { error = "Access denied" }, JsonRequestBehavior.AllowGet);
            var data = _weekly.GetEntry(flockId, weekNo, feedTypeId);
            if (data == null) return Json(new object[0], JsonRequestBehavior.AllowGet);
            return Json(StandardsJson(data.Standards), JsonRequestBehavior.AllowGet);
        }

        /// <summary>JSON: declared nutrient values of a feed formula.</summary>
        [HttpGet]
        public JsonResult FormulaValues(int id)
        {
            var f = _formulas.Get(id);
            if (f == null) return Json(new object[0], JsonRequestBehavior.AllowGet);
            return Json(f.Details.Where(d => d.Value.HasValue).Select(d => new { code = d.ParameterCode, value = d.Value }), JsonRequestBehavior.AllowGet);
        }

        public static object StandardsJson(IEnumerable<ParameterStandard> standards)
        {
            return standards.Select(s => new
            {
                code = s.ParameterCode,
                name = s.ParameterName,
                group = s.ParameterGroup,
                unit = s.UnitCode,
                dec = s.DecimalPlaces,
                dir = s.Direction,
                std = s.StandardValue,
                min = s.MinValue,
                max = s.MaxValue,
                cmin = s.CriticalMinValue,
                cmax = s.CriticalMaxValue,
                warn = s.WarningPct,
                crit = s.CriticalPct,
                lmin = s.LogicalMin,
                lmax = s.LogicalMax
            }).ToList();
        }

        private void Fill(WeeklyEntryViewModel m, WeeklyEntryData d)
        {
            var ctx = d.Context;
            m.FlockID = ctx.FlockID;
            m.WeekNo = ctx.WeekNo;
            m.Picker.FlockID = ctx.FlockID;
            m.Picker.FarmID = ctx.FarmID;
            m.Picker.HouseID = ctx.HouseID;
            m.CanDelete = d.Existing != null && d.Existing.IsLatest && Can(PermissionCodes.WeeklyDelete);

            var w = d.Existing;
            if (w == null)
            {
                m.FlockWeeklyID = 0;
                m.EntryDate = ctx.WeekEnd > DateTime.Today ? DateTime.Today : ctx.WeekEnd;
                m.FeedTypeID = ctx.SuggestedFeedTypeID;
                var formula = ctx.SuggestedFeedTypeID.HasValue ? _formulas.Lookup(ctx.SuggestedFeedTypeID, m.EntryDate).FirstOrDefault() : null;
                m.FeedFormulaID = formula != null ? formula.Id : (int?)null;
                m.NutritionSource = "F";
                if (m.FeedTypeID.HasValue)
                    m.FeedLines.Add(new FeedLineInput { FeedTypeID = m.FeedTypeID.Value, FeedFormulaID = m.FeedFormulaID, Sex = ctx.PlacedMales > 0 && ctx.PlacedFemales > 0 ? "B" : ctx.MainSex });
                return;
            }

            m.FlockWeeklyID = w.FlockWeeklyID;
            m.EntryDate = w.EntryDate;
            var p = w.Production;
            if (p != null)
                m.Production = new ProductionInput
                {
                    TotalEggs = p.TotalEggs, HatchingEggs = p.HatchingEggs, FloorEggs = p.FloorEggs, DirtyEggs = p.DirtyEggs,
                    BrokenEggs = p.BrokenEggs, CrackedEggs = p.CrackedEggs, DoubleYolkEggs = p.DoubleYolkEggs, AvgEggWeight = p.AvgEggWeight,
                    LightHours = p.LightHours, LightIntensity = p.LightIntensity, TempDay = p.TempDay, TempNight = p.TempNight
                };
            m.FeedTypeID = w.FeedTypeID;
            m.FeedFormulaID = w.FeedFormulaID;
            m.MaleMortality = w.MaleMortality;
            m.FemaleMortality = w.FemaleMortality;
            m.MaleCulling = w.MaleCulling;
            m.FemaleCulling = w.FemaleCulling;
            m.ManualBodyWeight = w.ManualBodyWeight;
            m.NutritionSource = w.NutritionSource ?? "F";
            m.LabSampleRef = w.LabSampleRef;
            m.AnalysisDate = w.AnalysisDate;
            m.MortalityReasonID = w.MortalityReasonID;
            m.Remarks = w.Remarks;
            m.RowVersion = w.RowVer != null ? Convert.ToBase64String(w.RowVer) : null;
            m.SamplesFemale = string.Join(" ", w.Samples.Where(s => s.Sex == "F").Select(s => s.Weight.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)));
            m.SamplesMale = string.Join(" ", w.Samples.Where(s => s.Sex == "M").Select(s => s.Weight.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)));
            m.FeedLines = w.FeedLines.Select(f => new FeedLineInput { FeedTypeID = f.FeedTypeID, FeedFormulaID = f.FeedFormulaID, Sex = f.Sex, QuantityKg = f.QuantityKg, WastageKg = f.WastageKg }).ToList();
            if (m.FeedLines.Count == 0 && w.TotalFeedKg.HasValue && w.FeedTypeID.HasValue)
                m.FeedLines.Add(new FeedLineInput { FeedTypeID = w.FeedTypeID.Value, FeedFormulaID = w.FeedFormulaID, Sex = "B", QuantityKg = w.TotalFeedKg, WastageKg = w.FeedWastageKg });
            m.Nutrients = w.Nutrients.Select(n => new NutrientInput { ParameterID = n.ParameterID, ParameterCode = n.ParameterCode, Value = n.Value }).ToList();
            m.MortalityReasons = w.MortalityReasons.Select(r => new MortalityReasonInput { MortalityReasonID = r.MortalityReasonID, Sex = r.Sex, BirdCount = r.BirdCount }).ToList();
        }

        private void PrepareWeeks(WeeklyEntryViewModel m)
        {
            var ctx = m.Data.Context;
            var last = Math.Max(ctx.NextWeek, ctx.WeekNo);
            var weeks = new List<SelectListItem>();
            for (var wk = ctx.FirstWeek; wk <= last; wk++)
            {
                var saved = ctx.LastWeek.HasValue && wk <= ctx.LastWeek.Value;
                weeks.Add(new SelectListItem
                {
                    Value = wk.ToString(),
                    Text = "Week " + wk + (saved ? "  (saved)" : wk == ctx.NextWeek ? "  (next to enter)" : string.Empty),
                    Selected = wk == ctx.WeekNo
                });
            }
            m.Picker.ShowWeek = true;
            m.Picker.WeekNo = ctx.WeekNo;
            m.Picker.Weeks = weeks;
        }

        private void PrepareLists(WeeklyEntryViewModel m)
        {
            m.FeedTypes = SelectLists.From(_feedTypes.Lookup(), m.FeedTypeID, "-- Select feed type --");
            m.Formulas = SelectLists.From(_formulas.Lookup(null, null), m.FeedFormulaID, "-- No formula --");
            m.Reasons = SelectLists.From(_reasons.Lookup(), m.MortalityReasonID, "-- Select reason --");
        }
    }
}
