using System;
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
    /// Hatch results: one setting of eggs from one flock-week. Eggs set and infertile eggs (candling) give
    /// fertility %; chicks hatched give hatchability %. Both appear against the week the eggs were laid in.
    /// </summary>
    [PermissionAuthorize(PermissionCodes.HatchView)]
    public class HatchResultController : FlockScreenController
    {
        private readonly IHatchResultService _hatch;
        private readonly IWeeklyService _weekly;

        public HatchResultController(IHatchResultService hatch, IWeeklyService weekly, IFarmService farms, IHouseService houses, IFlockService flocks)
            : base(farms, houses, flocks)
        {
            _hatch = hatch;
            _weekly = weekly;
        }

        public ActionResult Index(int? farmId, int? houseId, int? flockId, DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            return View(new HatchResultListViewModel
            {
                Picker = Picker("Index", farmId, houseId, flockId, false),
                FromDate = fromDate,
                ToDate = toDate,
                Result = _hatch.Search(new HatchResultSearchCriteria { FarmID = farmId, FlockID = flockId, FromDate = fromDate, ToDate = toDate, PageNo = page }),
                CanEdit = Can(PermissionCodes.HatchEdit)
            });
        }

        [PermissionAuthorize(PermissionCodes.HatchEdit)]
        public ActionResult Create(int? flockId, int? weekNo)
        {
            var m = new HatchResultEditViewModel { FlockID = flockId ?? 0, WeekNo = weekNo ?? 0, SetDate = DateTime.Today };
            Prepare(m);
            return View("Edit", m);
        }

        [PermissionAuthorize(PermissionCodes.HatchEdit)]
        public ActionResult Edit(int id)
        {
            var h = _hatch.Get(id);
            if (h == null) return NotFoundView();
            var m = new HatchResultEditViewModel
            {
                HatchResultID = h.HatchResultID, FlockID = h.FlockID, WeekNo = h.WeekNo, SetDate = h.SetDate, HatchDate = h.HatchDate,
                SettableEggs = h.SettableEggs, InfertileEggs = h.InfertileEggs, DeadInShell = h.DeadInShell, HatchedChicks = h.HatchedChicks,
                SaleableChicks = h.SaleableChicks, Remarks = h.Remarks
            };
            Prepare(m);
            return View(m);
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.HatchEdit)]
        public ActionResult Save(HatchResultEditViewModel m)
        {
            if (ModelState.IsValid)
            {
                var r = _hatch.Save(new HatchResult
                {
                    HatchResultID = m.HatchResultID, FlockID = m.FlockID, WeekNo = m.WeekNo, SetDate = m.SetDate.Value, HatchDate = m.HatchDate,
                    SettableEggs = m.SettableEggs, InfertileEggs = m.InfertileEggs, DeadInShell = m.DeadInShell, HatchedChicks = m.HatchedChicks,
                    SaleableChicks = m.SaleableChicks, Remarks = m.Remarks
                });
                if (r.Success)
                {
                    Success(r.Message);
                    return RedirectToAction("Index", new { flockId = m.FlockID });
                }
                AddErrors(r);
            }
            Prepare(m);
            return View("Edit", m);
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.HatchEdit)]
        public ActionResult Delete(int id, int? flockId)
        {
            var r = _hatch.Delete(id);
            if (r.Success) Success(r.Message); else Error(r.Message);
            return RedirectToAction("Index", new { flockId });
        }

        /// <summary>JSON: the saved weeks of a flock (the weeks eggs can come from).</summary>
        [HttpGet]
        public JsonResult Weeks(int flockId)
        {
            var weeks = _weekly.Search(new WeeklySearchCriteria { FlockID = flockId, PageSize = 200 }).Items
                .OrderByDescending(w => w.WeekNo)
                .Select(w => new { id = w.WeekNo, text = "Week " + w.WeekNo + " (" + w.EntryDate.ToString("dd-MMM-yyyy") + ")" });
            return Json(weeks, JsonRequestBehavior.AllowGet);
        }

        private void Prepare(HatchResultEditViewModel m)
        {
            m.Flocks = SelectLists.From(Flocks.ActiveLookup(null, null), m.FlockID, "-- Select flock --");
            var weeks = m.FlockID > 0
                ? _weekly.Search(new WeeklySearchCriteria { FlockID = m.FlockID, PageSize = 200 }).Items.OrderByDescending(w => w.WeekNo).ToList()
                : new System.Collections.Generic.List<WeeklyListItem>();
            m.Weeks = weeks.Select(w => new SelectListItem
            {
                Value = w.WeekNo.ToString(),
                Text = "Week " + w.WeekNo + " (" + w.EntryDate.ToString("dd-MMM-yyyy") + ")",
                Selected = w.WeekNo == m.WeekNo
            }).ToList();
        }
    }
}
