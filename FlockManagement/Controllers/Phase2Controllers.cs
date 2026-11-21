using System;
using System.Collections.Generic;
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
    // ================================================================ STANDARDS
    [PermissionAuthorize(PermissionCodes.StandardView)]
    public class StandardController : BaseController
    {
        private static readonly string[] Groups = { "BODYWEIGHT", "WEIGHTGAIN", "FEED", "NUTRITION", "MORTALITY", "UNIFORMITY", "PRODUCTION", "FERTILITY", "HATCH", "ENVIRONMENT" };

        private readonly IStandardService _standards;
        private readonly IBirdTypeService _birdTypes;
        private readonly IBreedService _breeds;
        private readonly IFeedTypeService _feedTypes;

        public StandardController(IStandardService standards, IBirdTypeService birdTypes, IBreedService breeds, IFeedTypeService feedTypes)
        {
            _standards = standards;
            _birdTypes = birdTypes;
            _breeds = breeds;
            _feedTypes = feedTypes;
        }

        public ActionResult Index(int? birdTypeId, int? breedId, int? parameterId, string group, int? fromWeek, int? toWeek, bool activeOnly = true, int page = 1)
        {
            var criteria = new StandardSearchCriteria
            {
                BirdTypeID = birdTypeId, BreedID = breedId, ParameterID = parameterId, ParameterGroup = string.IsNullOrEmpty(group) ? null : group,
                FromWeek = fromWeek, ToWeek = toWeek, ActiveOnly = activeOnly, PageNo = page, PageSize = 50
            };
            var parameters = _standards.Parameters();
            return View(new StandardListViewModel
            {
                Criteria = criteria,
                Result = _standards.Search(criteria),
                BirdTypes = SelectLists.From(_birdTypes.Lookup(), birdTypeId, "All bird types"),
                Breeds = SelectLists.From(_breeds.Lookup(), breedId, "All breeds"),
                Parameters = SelectLists.From(parameters.Select(p => new LookupItem(p.ParameterID, p.ParameterName + " (" + p.UnitCode + ")")), parameterId, "All parameters"),
                Groups = SelectLists.FromStrings(Groups, group, "All groups"),
                CanEdit = Can(PermissionCodes.StandardEdit)
            });
        }

        /// <summary>Week-by-week grid for one bird type / breed / feed / parameter / effective date.</summary>
        public ActionResult Grid(int? birdTypeId, int? breedId, int? feedTypeId, int? parameterId, DateTime? effectiveFrom, int lastWeek = 66)
        {
            var model = new StandardGridViewModel
            {
                BirdTypeID = birdTypeId ?? 0,
                BreedID = breedId,
                FeedTypeID = feedTypeId,
                ParameterID = parameterId ?? 0,
                EffectiveFrom = effectiveFrom,
                LastWeek = Math.Max(1, Math.Min(120, lastWeek))
            };
            if (model.BirdTypeID > 0 && model.ParameterID > 0)
            {
                model.ExistingDates = _standards.EffectiveDates(model.BirdTypeID, model.ParameterID);
                if (!model.EffectiveFrom.HasValue)
                    model.EffectiveFrom = model.ExistingDates.Count > 0 ? model.ExistingDates[0] : DateTime.Today;
                model.Rows = _standards.GetGrid(model.ToKey(), model.LastWeek);
                model.Loaded = true;
            }
            Prepare(model);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.StandardEdit)]
        public ActionResult Grid(StandardGridViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Loaded = true;
                Prepare(model);
                return View(model);
            }
            var result = _standards.SaveGrid(model.ToKey(), model.Rows ?? new List<StandardGridRow>());
            if (!result.Success)
            {
                AddErrors(result);
                model.Loaded = true;
                Prepare(model);
                return View(model);
            }
            Success(result.Message);
            return RedirectToAction("Grid", new
            {
                birdTypeId = model.BirdTypeID, breedId = model.BreedID, feedTypeId = model.FeedTypeID, parameterId = model.ParameterID,
                effectiveFrom = model.EffectiveFrom.Value.ToString("yyyy-MM-dd"), lastWeek = model.LastWeek
            });
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.StandardEdit)]
        public ActionResult ToggleActive(int id, bool active, string returnUrl)
        {
            var r = _standards.SetActive(id, active);
            if (r.Success) Success(r.Message); else Error(r.Message);
            return RedirectToLocal(returnUrl);
        }

        private void Prepare(StandardGridViewModel m)
        {
            var parameters = _standards.Parameters();
            m.SelectedParameter = parameters.FirstOrDefault(p => p.ParameterID == m.ParameterID);
            m.BirdTypes = SelectLists.From(_birdTypes.Lookup(), m.BirdTypeID, "-- Select bird type --");
            m.Breeds = SelectLists.From(_breeds.Lookup(), m.BreedID, "All breeds");
            m.FeedTypes = SelectLists.From(_feedTypes.Lookup(), m.FeedTypeID, "Any feed type");
            m.Parameters = SelectLists.From(parameters.Select(p => new LookupItem(p.ParameterID, p.ParameterName + " (" + p.UnitCode + ")")), m.ParameterID, "-- Select parameter --");
            m.CanEdit = Can(PermissionCodes.StandardEdit);
        }
    }

    // ================================================================ MORTALITY REASON
    public class MortalityReasonController : MasterController<MortalityReason, MortalityReasonViewModel>
    {
        public MortalityReasonController(IMortalityReasonService service) : base(service) { }

        protected override string ViewPermission { get { return PermissionCodes.MortalityReasonView; } }
        protected override string EditPermission { get { return PermissionCodes.MortalityReasonEdit; } }
        protected override string EntityTitle { get { return "Mortality Reason"; } }
        protected override MortalityReasonViewModel ToViewModel(MortalityReason e) { return MortalityReasonViewModel.From(e); }
        protected override MortalityReason ToEntity(MortalityReasonViewModel m) { return m.ToEntity(); }
        protected override int GetId(MortalityReasonViewModel m) { return m.MortalityReasonID; }
    }

    // ================================================================ FEED FORMULA
    [PermissionAuthorize(PermissionCodes.FeedFormulaView)]
    public class FeedFormulaController : BaseController
    {
        private readonly IFeedFormulaService _formulas;
        private readonly IFeedTypeService _feedTypes;

        public FeedFormulaController(IFeedFormulaService formulas, IFeedTypeService feedTypes)
        {
            _formulas = formulas;
            _feedTypes = feedTypes;
        }

        public ActionResult Index(string search, int? feedTypeId, string status = "active", int page = 1)
        {
            return View(new FeedFormulaListViewModel
            {
                Search = search,
                FeedTypeID = feedTypeId,
                Status = status,
                Result = _formulas.Search(search, feedTypeId, MasterListViewModel<FeedFormula>.ParseStatus(status), page),
                FeedTypes = SelectLists.From(_feedTypes.Lookup(), feedTypeId, "All feed types"),
                CanEdit = Can(PermissionCodes.FeedFormulaEdit)
            });
        }

        public ActionResult Details(int id)
        {
            var f = _formulas.Get(id);
            if (f == null) return NotFoundView();
            ViewBag.CanEdit = Can(PermissionCodes.FeedFormulaEdit);
            return View(f);
        }

        [PermissionAuthorize(PermissionCodes.FeedFormulaEdit)]
        public ActionResult Create()
        {
            var m = FeedFormulaEditViewModel.From(_formulas.New());
            m.FeedTypes = SelectLists.From(_feedTypes.Lookup(), null, "-- Select feed type --");
            return View("Edit", m);
        }

        [PermissionAuthorize(PermissionCodes.FeedFormulaEdit)]
        public ActionResult Edit(int id)
        {
            var f = _formulas.Get(id);
            if (f == null) return NotFoundView();
            var m = FeedFormulaEditViewModel.From(f);
            m.FeedTypes = SelectLists.From(_feedTypes.Lookup(), m.FeedTypeID, "-- Select feed type --");
            return View("Edit", m);
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.FeedFormulaEdit)]
        public ActionResult Save(FeedFormulaEditViewModel model)
        {
            model.Details = model.Details ?? new List<FeedFormulaDetail>();
            if (ModelState.IsValid)
            {
                var result = _formulas.Save(model.ToEntity());
                if (result.Success)
                {
                    Success(result.Message);
                    return RedirectToAction("Details", new { id = result.Data });
                }
                AddErrors(result);
            }
            // re-read labels for the nutrient rows
            var blank = _formulas.New().Details.ToDictionary(d => d.ParameterID);
            foreach (var d in model.Details)
            {
                FeedFormulaDetail b;
                if (blank.TryGetValue(d.ParameterID, out b))
                {
                    d.ParameterName = b.ParameterName;
                    d.UnitCode = b.UnitCode;
                    d.DecimalPlaces = b.DecimalPlaces;
                    d.ParameterCode = b.ParameterCode;
                }
            }
            model.FeedTypes = SelectLists.From(_feedTypes.Lookup(), model.FeedTypeID, "-- Select feed type --");
            return View("Edit", model);
        }
    }

    // ================================================================ PLACEMENT
    [PermissionAuthorize(PermissionCodes.PlacementView)]
    public class PlacementController : FlockScreenController
    {
        private readonly IMovementService _movements;

        public PlacementController(IMovementService movements, IFarmService farms, IHouseService houses, IFlockService flocks)
            : base(farms, houses, flocks)
        {
            _movements = movements;
        }

        public ActionResult Index(int? farmId, int? houseId, int? flockId, DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            return View(new MovementListViewModel<PlacementRecord>
            {
                Picker = Picker("Index", farmId, houseId, flockId, false),
                FromDate = fromDate,
                ToDate = toDate,
                Result = _movements.Placements(new MovementSearchCriteria { FlockID = flockId, FarmID = farmId, FromDate = fromDate, ToDate = toDate, PageNo = page }),
                CanEdit = Can(PermissionCodes.PlacementEdit)
            });
        }

        [PermissionAuthorize(PermissionCodes.PlacementEdit)]
        public ActionResult Create(int? flockId)
        {
            var m = new PlacementCreateViewModel { FlockID = flockId ?? 0, PlacementDate = DateTime.Today };
            Prepare(m);
            return View(m);
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.PlacementEdit)]
        public ActionResult Create(PlacementCreateViewModel m)
        {
            if (ModelState.IsValid)
            {
                var r = _movements.AddPlacement(new PlacementRecord
                {
                    FlockID = m.FlockID, PlacementDate = m.PlacementDate.Value, HouseID = m.HouseID ?? 0, MaleCount = m.MaleCount,
                    FemaleCount = m.FemaleCount, Source = m.Source, ChickAvgWeight = m.ChickAvgWeight, Remarks = m.Remarks
                });
                if (r.Success)
                {
                    Success(r.Message);
                    return RedirectToAction("Index", new { flockId = m.FlockID });
                }
                AddErrors(r);
            }
            Prepare(m);
            return View(m);
        }

        private void Prepare(PlacementCreateViewModel m)
        {
            m.Flocks = SelectLists.From(Flocks.ActiveLookup(null, null), m.FlockID, "-- Select flock --");
            m.Houses = SelectLists.From(Houses.Lookup(), m.HouseID, "Flock's own house");
        }
    }

    // ================================================================ TRANSFER
    [PermissionAuthorize(PermissionCodes.TransferView)]
    public class TransferController : FlockScreenController
    {
        private readonly IMovementService _movements;

        public TransferController(IMovementService movements, IFarmService farms, IHouseService houses, IFlockService flocks)
            : base(farms, houses, flocks)
        {
            _movements = movements;
        }

        public ActionResult Index(int? farmId, int? houseId, int? flockId, DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            return View(new MovementListViewModel<TransferRecord>
            {
                Picker = Picker("Index", farmId, houseId, flockId, false),
                FromDate = fromDate,
                ToDate = toDate,
                Result = _movements.Transfers(new MovementSearchCriteria { FlockID = flockId, FarmID = farmId, FromDate = fromDate, ToDate = toDate, PageNo = page }),
                CanEdit = Can(PermissionCodes.TransferEdit)
            });
        }

        [PermissionAuthorize(PermissionCodes.TransferEdit)]
        public ActionResult Create(int? flockId)
        {
            var m = new TransferCreateViewModel { FlockID = flockId ?? 0, TransferDate = DateTime.Today };
            Prepare(m);
            return View(m);
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.TransferEdit)]
        public ActionResult Create(TransferCreateViewModel m)
        {
            if (ModelState.IsValid)
            {
                var r = _movements.SaveTransfer(new TransferRecord
                {
                    FlockID = m.FlockID, TransferDate = m.TransferDate.Value, TransferType = m.TransferType, ToHouseID = m.ToHouseID,
                    CounterFlockID = m.CounterFlockID, MaleCount = m.MaleCount, FemaleCount = m.FemaleCount, Reason = m.Reason
                });
                if (r.Success)
                {
                    Success(r.Message);
                    return RedirectToAction("Index", new { flockId = m.FlockID });
                }
                AddErrors(r);
            }
            Prepare(m);
            return View(m);
        }

        private void Prepare(TransferCreateViewModel m)
        {
            m.Flocks = SelectLists.From(Flocks.ActiveLookup(null, null), m.FlockID, "-- Select flock --");
            m.Houses = SelectLists.From(Houses.Lookup(), m.ToHouseID, "-- Select house --");
        }
    }

    // ================================================================ ADJUSTMENT
    [PermissionAuthorize(PermissionCodes.AdjustView)]
    public class AdjustmentController : FlockScreenController
    {
        private readonly IMovementService _movements;

        public AdjustmentController(IMovementService movements, IFarmService farms, IHouseService houses, IFlockService flocks)
            : base(farms, houses, flocks)
        {
            _movements = movements;
        }

        public ActionResult Index(int? farmId, int? houseId, int? flockId, DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            return View(new MovementListViewModel<AdjustmentRecord>
            {
                Picker = Picker("Index", farmId, houseId, flockId, false),
                FromDate = fromDate,
                ToDate = toDate,
                Result = _movements.Adjustments(new MovementSearchCriteria { FlockID = flockId, FarmID = farmId, FromDate = fromDate, ToDate = toDate, PageNo = page }),
                CanEdit = Can(PermissionCodes.AdjustEdit)
            });
        }

        [PermissionAuthorize(PermissionCodes.AdjustEdit)]
        public ActionResult Create(int? flockId)
        {
            var m = new AdjustmentCreateViewModel { FlockID = flockId ?? 0, AdjustmentDate = DateTime.Today };
            m.Flocks = SelectLists.From(Flocks.ActiveLookup(null, null), m.FlockID, "-- Select flock --");
            return View(m);
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.AdjustEdit)]
        public ActionResult Create(AdjustmentCreateViewModel m)
        {
            if (ModelState.IsValid)
            {
                var r = _movements.SaveAdjustment(new AdjustmentRecord
                {
                    FlockID = m.FlockID, AdjustmentDate = m.AdjustmentDate.Value, MaleAdjustment = m.MaleAdjustment,
                    FemaleAdjustment = m.FemaleAdjustment, Reason = m.Reason
                });
                if (r.Success)
                {
                    Success(r.Message);
                    return RedirectToAction("Index", new { flockId = m.FlockID });
                }
                AddErrors(r);
            }
            m.Flocks = SelectLists.From(Flocks.ActiveLookup(null, null), m.FlockID, "-- Select flock --");
            return View(m);
        }
    }
}
