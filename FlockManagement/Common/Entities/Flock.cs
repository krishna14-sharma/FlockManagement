using System;

namespace FlockManagement.Common.Entities
{
    public class Flock
    {
        public int FlockID { get; set; }
        public string FlockNumber { get; set; }
        public int LotID { get; set; }
        public string LotNumber { get; set; }
        public int BirdTypeID { get; set; }
        public string BirdTypeName { get; set; }
        public int BreedID { get; set; }
        public string BreedName { get; set; }
        public int FarmID { get; set; }
        public string FarmName { get; set; }
        public int HouseID { get; set; }
        public string HouseName { get; set; }
        public DateTime PlacementDate { get; set; }
        public DateTime HatchDate { get; set; }
        public int InitialBirdCount { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public int CurrentMaleCount { get; set; }
        public int CurrentFemaleCount { get; set; }
        public int TotalBirdCount { get; set; }
        public string Source { get; set; }
        /// <summary>Completed weeks since hatch (computed by SQL Server).</summary>
        public int CurrentAge { get; set; }
        public string Status { get; set; }
        public DateTime? DepletionDate { get; set; }
        public string Remarks { get; set; }
        public decimal? ChickAvgWeight { get; set; }
        public int? CreatedBy { get; set; }
        public string CreatedByName { get; set; }
        public DateTime CreatedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public string ModifiedByName { get; set; }
        public DateTime? ModifiedDate { get; set; }
        /// <summary>SQL rowversion used for optimistic concurrency.</summary>
        public byte[] RowVer { get; set; }
        public bool HasWeeklyData { get; set; }

        /// <summary>Livability since placement, %.</summary>
        public decimal LivabilityPct
        {
            get
            {
                return InitialBirdCount <= 0 ? 0m
                    : Math.Round(TotalBirdCount * 100m / InitialBirdCount, 2, MidpointRounding.AwayFromZero);
            }
        }
    }

    public class FlockSearchCriteria
    {
        public string Search { get; set; }
        public int? FarmID { get; set; }
        public int? HouseID { get; set; }
        public int? BirdTypeID { get; set; }
        public int? LotID { get; set; }
        public string Status { get; set; }
        public int? RestrictFarmID { get; set; }
        public int PageNo { get; set; }
        public int PageSize { get; set; }

        public FlockSearchCriteria()
        {
            PageNo = 1;
            PageSize = 25;
        }
    }
}
