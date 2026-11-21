using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using FlockManagement.Business.Interfaces;
using FlockManagement.Common.Constants;
using FlockManagement.Common.Entities;
using FlockManagement.Web.ViewModels;

namespace FlockManagement.Web.Controllers
{
    /// <summary>Builds dropdown lists from lookup items.</summary>
    public static class SelectLists
    {
        public static IEnumerable<SelectListItem> From(IEnumerable<LookupItem> items, int? selected, string placeholder = null)
        {
            var list = new List<SelectListItem>();
            if (placeholder != null) list.Add(new SelectListItem { Value = string.Empty, Text = placeholder });
            if (items != null)
            {
                list.AddRange(items.Select(i => new SelectListItem
                {
                    Value = i.Id.ToString(),
                    Text = i.Text,
                    Selected = selected.HasValue && selected.Value == i.Id
                }));
            }
            return list;
        }

        public static IEnumerable<SelectListItem> FromStrings(IEnumerable<string> items, string selected, string placeholder = null)
        {
            var list = new List<SelectListItem>();
            if (placeholder != null) list.Add(new SelectListItem { Value = string.Empty, Text = placeholder });
            if (items != null)
                list.AddRange(items.Select(s => new SelectListItem { Value = s, Text = s, Selected = s == selected }));
            return list;
        }
    }

    public class FarmController : MasterController<Farm, FarmViewModel>
    {
        public FarmController(IFarmService service) : base(service) { }

        protected override string ViewPermission { get { return PermissionCodes.FarmView; } }
        protected override string EditPermission { get { return PermissionCodes.FarmEdit; } }
        protected override string EntityTitle { get { return "Farm"; } }
        protected override FarmViewModel ToViewModel(Farm e) { return FarmViewModel.From(e); }
        protected override Farm ToEntity(FarmViewModel m) { return m.ToEntity(); }
        protected override int GetId(FarmViewModel m) { return m.FarmID; }
    }

    public class HouseController : MasterController<House, HouseViewModel>
    {
        private readonly IFarmService _farms;

        public HouseController(IHouseService service, IFarmService farms) : base(service)
        {
            _farms = farms;
        }

        protected override string ViewPermission { get { return PermissionCodes.HouseView; } }
        protected override string EditPermission { get { return PermissionCodes.HouseEdit; } }
        protected override string EntityTitle { get { return "House / Shed"; } }
        protected override HouseViewModel ToViewModel(House e) { return HouseViewModel.From(e); }
        protected override House ToEntity(HouseViewModel m) { return m.ToEntity(); }
        protected override int GetId(HouseViewModel m) { return m.HouseID; }

        protected override void PrepareEdit(HouseViewModel model)
        {
            model.Farms = SelectLists.From(_farms.Lookup(), model.FarmID, "-- Select farm --");
        }

        protected override void PrepareList(MasterListViewModel<House> model)
        {
            model.ParentOptions = SelectLists.From(_farms.Lookup(), model.ParentID, "All farms");
        }
    }

    public class BirdTypeController : MasterController<BirdType, BirdTypeViewModel>
    {
        public BirdTypeController(IBirdTypeService service) : base(service) { }

        protected override string ViewPermission { get { return PermissionCodes.BirdTypeView; } }
        protected override string EditPermission { get { return PermissionCodes.BirdTypeEdit; } }
        protected override string EntityTitle { get { return "Bird Type"; } }
        protected override BirdTypeViewModel ToViewModel(BirdType e) { return BirdTypeViewModel.From(e); }
        protected override BirdType ToEntity(BirdTypeViewModel m) { return m.ToEntity(); }
        protected override int GetId(BirdTypeViewModel m) { return m.BirdTypeID; }
    }

    public class BreedController : MasterController<Breed, BreedViewModel>
    {
        public BreedController(IBreedService service) : base(service) { }

        protected override string ViewPermission { get { return PermissionCodes.BreedView; } }
        protected override string EditPermission { get { return PermissionCodes.BreedEdit; } }
        protected override string EntityTitle { get { return "Breed"; } }
        protected override BreedViewModel ToViewModel(Breed e) { return BreedViewModel.From(e); }
        protected override Breed ToEntity(BreedViewModel m) { return m.ToEntity(); }
        protected override int GetId(BreedViewModel m) { return m.BreedID; }
    }

    public class LotController : MasterController<Lot, LotViewModel>
    {
        private readonly IFarmService _farms;

        public LotController(ILotService service, IFarmService farms) : base(service)
        {
            _farms = farms;
        }

        protected override string ViewPermission { get { return PermissionCodes.LotView; } }
        protected override string EditPermission { get { return PermissionCodes.LotEdit; } }
        protected override string EntityTitle { get { return "Lot"; } }
        protected override LotViewModel ToViewModel(Lot e) { return LotViewModel.From(e); }
        protected override Lot ToEntity(LotViewModel m) { return m.ToEntity(); }
        protected override int GetId(LotViewModel m) { return m.LotID; }

        protected override void PrepareEdit(LotViewModel model)
        {
            model.Farms = SelectLists.From(_farms.Lookup(), model.FarmID, "-- Any farm --");
        }

        protected override void PrepareList(MasterListViewModel<Lot> model)
        {
            model.ParentOptions = SelectLists.From(_farms.Lookup(), model.ParentID, "All farms");
        }
    }

    public class FeedTypeController : MasterController<FeedType, FeedTypeViewModel>
    {
        public FeedTypeController(IFeedTypeService service) : base(service) { }

        protected override string ViewPermission { get { return PermissionCodes.FeedTypeView; } }
        protected override string EditPermission { get { return PermissionCodes.FeedTypeEdit; } }
        protected override string EntityTitle { get { return "Feed Type"; } }
        protected override FeedTypeViewModel ToViewModel(FeedType e) { return FeedTypeViewModel.From(e); }
        protected override FeedType ToEntity(FeedTypeViewModel m) { return m.ToEntity(); }
        protected override int GetId(FeedTypeViewModel m) { return m.FeedTypeID; }
    }
}
