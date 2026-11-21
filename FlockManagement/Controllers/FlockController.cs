using System.Linq;
using System.Web.Mvc;
using FlockManagement.Business.Interfaces;
using FlockManagement.Common.Constants;
using FlockManagement.Common.Entities;
using FlockManagement.Web.Infrastructure;
using FlockManagement.Web.ViewModels;

namespace FlockManagement.Web.Controllers
{
    [PermissionAuthorize(PermissionCodes.FlockView)]
    public class FlockController : BaseController
    {
        private readonly IFlockService _flocks;
        private readonly IFarmService _farms;
        private readonly IHouseService _houses;
        private readonly IBirdTypeService _birdTypes;
        private readonly IBreedService _breeds;
        private readonly ILotService _lots;

        public FlockController(IFlockService flocks, IFarmService farms, IHouseService houses,
            IBirdTypeService birdTypes, IBreedService breeds, ILotService lots)
        {
            _flocks = flocks;
            _farms = farms;
            _houses = houses;
            _birdTypes = birdTypes;
            _breeds = breeds;
            _lots = lots;
        }

        public ActionResult Index(string search, int? farmId, int? houseId, int? birdTypeId, int? lotId, string status = "Active", int page = 1)
        {
            var criteria = new FlockSearchCriteria
            {
                Search = search,
                FarmID = farmId,
                HouseID = houseId,
                BirdTypeID = birdTypeId,
                LotID = lotId,
                Status = status == "All" ? null : status,
                PageNo = page,
                PageSize = 25
            };

            var model = new FlockListViewModel
            {
                Search = search,
                FarmID = farmId,
                HouseID = houseId,
                BirdTypeID = birdTypeId,
                LotID = lotId,
                Status = status,
                Result = _flocks.Search(criteria),
                CanEdit = Can(PermissionCodes.FlockEdit),
                Farms = SelectLists.From(_farms.Lookup(), farmId, "All farms"),
                Houses = SelectLists.From(_houses.Lookup(farmId), houseId, "All houses"),
                BirdTypes = SelectLists.From(_birdTypes.Lookup(), birdTypeId, "All bird types"),
                Lots = SelectLists.From(_lots.Lookup(), lotId, "All lots"),
                Statuses = SelectLists.FromStrings(new[] { "All" }.Concat(_flocks.Statuses), status)
            };
            return View(model);
        }

        public ActionResult Details(int id)
        {
            var flock = _flocks.Get(id);
            if (flock == null) return NotFoundView();
            ViewBag.CanEdit = Can(PermissionCodes.FlockEdit);
            ViewBag.CanChangeStatus = Can(PermissionCodes.FlockStatus);
            ViewBag.Statuses = _flocks.Statuses;
            return View(flock);
        }

        [PermissionAuthorize(PermissionCodes.FlockEdit)]
        public ActionResult Create()
        {
            var model = new FlockEditViewModel
            {
                PlacementDate = System.DateTime.Today,
                HatchDate = System.DateTime.Today.AddDays(-1),
                FarmID = CurrentUser.FarmID ?? 0
            };
            Prepare(model);
            return View("Edit", model);
        }

        [PermissionAuthorize(PermissionCodes.FlockEdit)]
        public ActionResult Edit(int id)
        {
            var flock = _flocks.Get(id);
            if (flock == null) return NotFoundView();
            var model = FlockEditViewModel.From(flock);
            Prepare(model);
            return View("Edit", model);
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.FlockEdit)]
        public ActionResult Save(FlockEditViewModel model)
        {
            if (model == null) return RedirectToAction("Index");
            if (!ModelState.IsValid)
            {
                Prepare(model);
                return View("Edit", model);
            }

            var result = _flocks.Save(model.ToEntity());
            if (!result.Success)
            {
                AddErrors(result);
                Prepare(model);
                return View("Edit", model);
            }

            Success(result.Message);
            return RedirectToAction("Details", new { id = result.Data });
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.FlockStatus)]
        public ActionResult SetStatus(FlockStatusViewModel model)
        {
            if (!ModelState.IsValid)
            {
                Error("Select a valid status.");
                return RedirectToAction("Details", new { id = model.FlockID });
            }
            var result = _flocks.SetStatus(model.FlockID, model.Status, model.DepletionDate);
            if (result.Success) Success(result.Message);
            else Error(result.Message);
            return RedirectToAction("Details", new { id = model.FlockID });
        }

        private void Prepare(FlockEditViewModel m)
        {
            m.Farms = SelectLists.From(_farms.Lookup(), m.FarmID, "-- Select farm --");
            m.Houses = SelectLists.From(m.FarmID > 0 ? _houses.Lookup(m.FarmID) : Enumerable.Empty<LookupItem>().ToList(), m.HouseID, "-- Select house --");
            m.BirdTypes = SelectLists.From(_birdTypes.Lookup(), m.BirdTypeID, "-- Select bird type --");
            m.Breeds = SelectLists.From(_breeds.Lookup(), m.BreedID, "-- Select breed --");
            m.Lots = SelectLists.From(_lots.Lookup(), m.LotID, "-- Select lot --");
        }
    }
}
