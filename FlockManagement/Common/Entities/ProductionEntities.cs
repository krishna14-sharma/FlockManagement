using System;
using System.Collections.Generic;

namespace FlockManagement.Common.Entities
{
    /// <summary>Production and environment figures typed on the weekly entry (Production & Environment tab).</summary>
    public class ProductionInput
    {
        public int? TotalEggs { get; set; }
        public int? HatchingEggs { get; set; }
        public int? FloorEggs { get; set; }
        public int? DirtyEggs { get; set; }
        public int? BrokenEggs { get; set; }
        public int? CrackedEggs { get; set; }
        public int? DoubleYolkEggs { get; set; }
        public decimal? AvgEggWeight { get; set; }
        public decimal? LightHours { get; set; }
        public decimal? LightIntensity { get; set; }
        public decimal? TempDay { get; set; }
        public decimal? TempNight { get; set; }

        public bool IsEmpty
        {
            get
            {
                return !TotalEggs.HasValue && !HatchingEggs.HasValue && !FloorEggs.HasValue && !DirtyEggs.HasValue && !BrokenEggs.HasValue
                       && !CrackedEggs.HasValue && !DoubleYolkEggs.HasValue && !AvgEggWeight.HasValue && !LightHours.HasValue
                       && !LightIntensity.HasValue && !TempDay.HasValue && !TempNight.HasValue;
            }
        }
    }

    /// <summary>Saved production / environment block of a week (sp_FlockWeekly_GetProduction).</summary>
    public class ProductionData : ProductionInput
    {
        public int FlockWeeklyID { get; set; }
        public decimal? HenDayProductionPct { get; set; }
        public decimal? HenHousedEggsCum { get; set; }
        public int? HenHousedCount { get; set; }
        public decimal? HenDays { get; set; }
        public IList<HatchResult> HatchResults { get; set; }

        public decimal? HatchingEggPct
        {
            get { return TotalEggs.HasValue && TotalEggs.Value > 0 && HatchingEggs.HasValue ? HatchingEggs.Value * 100m / TotalEggs.Value : (decimal?)null; }
        }

        public ProductionData()
        {
            HatchResults = new List<HatchResult>();
        }
    }

    /// <summary>One setting of eggs from one flock-week and its candling / hatch result.</summary>
    public class HatchResult
    {
        public int HatchResultID { get; set; }
        public int FlockID { get; set; }
        public string FlockNumber { get; set; }
        public string FarmName { get; set; }
        public string HouseName { get; set; }
        public string BirdTypeName { get; set; }
        public int WeekNo { get; set; }
        public DateTime SetDate { get; set; }
        public DateTime? HatchDate { get; set; }
        public int SettableEggs { get; set; }
        public int InfertileEggs { get; set; }
        public int FertileEggs { get; set; }
        public int DeadInShell { get; set; }
        public int HatchedChicks { get; set; }
        public int? SaleableChicks { get; set; }
        public decimal? FertilityPct { get; set; }
        public decimal? HatchabilityPct { get; set; }
        public decimal? HatchOfFertilePct { get; set; }
        public string Remarks { get; set; }
        public string CreatedByName { get; set; }
        public DateTime CreatedDate { get; set; }

        public bool IsHatched { get { return HatchDate.HasValue; } }
    }

    public class HatchResultSearchCriteria
    {
        public int? FarmID { get; set; }
        public int? FlockID { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? RestrictFarmID { get; set; }
        public int PageNo { get; set; }
        public int PageSize { get; set; }

        public HatchResultSearchCriteria()
        {
            PageNo = 1;
            PageSize = 25;
        }
    }
}
