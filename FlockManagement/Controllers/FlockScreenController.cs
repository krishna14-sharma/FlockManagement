using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using FlockManagement.Business.Interfaces;
using FlockManagement.Common.Entities;
using FlockManagement.Web.ViewModels;

namespace FlockManagement.Web.Controllers
{
    /// <summary>Base for screens that start with a Farm -> House -> Flock selection.</summary>
    public abstract class FlockScreenController : BaseController
    {
        protected readonly IFarmService Farms;
        protected readonly IHouseService Houses;
        protected readonly IFlockService Flocks;

        protected FlockScreenController(IFarmService farms, IHouseService houses, IFlockService flocks)
        {
            Farms = farms;
            Houses = houses;
            Flocks = flocks;
        }

        /// <summary>Builds the picker; when only a flock id is given, its farm and house are filled in.</summary>
        protected FlockPickerModel Picker(string action, int? farmId, int? houseId, int? flockId, bool activeOnly = true)
        {
            if (flockId.HasValue && (!farmId.HasValue || !houseId.HasValue))
            {
                var f = Flocks.Get(flockId.Value);
                if (f != null)
                {
                    farmId = farmId ?? f.FarmID;
                    houseId = houseId ?? f.HouseID;
                }
            }

            IList<LookupItem> flockItems = activeOnly
                ? Flocks.ActiveLookup(farmId, houseId)
                : Flocks.Search(new FlockSearchCriteria { FarmID = farmId, HouseID = houseId, PageSize = 200 }).Items
                        .Select(x => new LookupItem(x.FlockID, x.FlockNumber + " - " + x.LotNumber + (x.Status == "Active" ? "" : " (" + x.Status + ")"), x.HouseID)).ToList();

            if (flockId.HasValue && flockItems.All(i => i.Id != flockId.Value))
            {
                var f = Flocks.Get(flockId.Value);
                if (f != null) flockItems.Insert(0, new LookupItem(f.FlockID, f.FlockNumber + " - " + f.LotNumber + " (" + f.Status + ")", f.HouseID));
            }

            return new FlockPickerModel
            {
                FarmID = farmId,
                HouseID = houseId,
                FlockID = flockId,
                Action = action,
                Controller = (string)RouteData.Values["controller"],
                Farms = SelectLists.From(Farms.Lookup(), farmId, "All farms"),
                Houses = SelectLists.From(farmId.HasValue ? Houses.Lookup(farmId) : new List<LookupItem>(), houseId, "All houses"),
                Flocks = SelectLists.From(flockItems, flockId, "-- Select flock --")
            };
        }
    }
}
