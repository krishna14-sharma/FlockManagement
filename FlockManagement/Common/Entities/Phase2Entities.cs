using System;
using System.Collections.Generic;

namespace FlockManagement.Common.Entities
{
    /// <summary>Measurable parameter (body weight, feed intake, each nutrient, mortality, uniformity...).</summary>
    public class Parameter
    {
        public int ParameterID { get; set; }
        public string ParameterCode { get; set; }
        public string ParameterName { get; set; }
        public string ParameterGroup { get; set; }
        public int UnitID { get; set; }
        public string UnitCode { get; set; }
        public int DecimalPlaces { get; set; }
        /// <summary>T = target, H = higher is better, L = lower is better.</summary>
        public string Direction { get; set; }
        public decimal? LogicalMin { get; set; }
        public decimal? LogicalMax { get; set; }
        public int SortOrder { get; set; }
    }

    public class Standard
    {
        public int StandardID { get; set; }
        public int BirdTypeID { get; set; }
        public string BirdTypeName { get; set; }
        public int? BreedID { get; set; }
        public string BreedName { get; set; }
        public int? FeedTypeID { get; set; }
        public string FeedTypeName { get; set; }
        public int AgeWeek { get; set; }
        public int ParameterID { get; set; }
        public string ParameterName { get; set; }
        public string ParameterGroup { get; set; }
        public string UnitCode { get; set; }
        public int DecimalPlaces { get; set; }
        public decimal StandardValue { get; set; }
        public decimal? MinValue { get; set; }
        public decimal? MaxValue { get; set; }
        public decimal? CriticalMinValue { get; set; }
        public decimal? CriticalMaxValue { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public bool IsActive { get; set; }
    }

    public class StandardSearchCriteria
    {
        public int? BirdTypeID { get; set; }
        public int? BreedID { get; set; }
        public int? ParameterID { get; set; }
        public string ParameterGroup { get; set; }
        public int? FromWeek { get; set; }
        public int? ToWeek { get; set; }
        public bool ActiveOnly { get; set; }
        public int PageNo { get; set; }
        public int PageSize { get; set; }

        public StandardSearchCriteria()
        {
            ActiveOnly = true;
            PageNo = 1;
            PageSize = 50;
        }
    }

    /// <summary>Identifies one standard set edited as a week-by-week grid.</summary>
    public class StandardGridKey
    {
        public int BirdTypeID { get; set; }
        public int? BreedID { get; set; }
        public int? FeedTypeID { get; set; }
        public int ParameterID { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
    }

    public class StandardGridRow
    {
        public int AgeWeek { get; set; }
        public decimal? StandardValue { get; set; }
        public decimal? MinValue { get; set; }
        public decimal? MaxValue { get; set; }
        public decimal? CriticalMinValue { get; set; }
        public decimal? CriticalMaxValue { get; set; }
    }

    /// <summary>The standard that applies to a flock at an age (fn_FlockStandard).</summary>
    public class ParameterStandard
    {
        public int ParameterID { get; set; }
        public string ParameterCode { get; set; }
        public string ParameterName { get; set; }
        public string ParameterGroup { get; set; }
        public string UnitCode { get; set; }
        public string Direction { get; set; }
        public int DecimalPlaces { get; set; }
        public int SortOrder { get; set; }
        public decimal? LogicalMin { get; set; }
        public decimal? LogicalMax { get; set; }
        public decimal? StandardValue { get; set; }
        public decimal? MinValue { get; set; }
        public decimal? MaxValue { get; set; }
        public decimal? CriticalMinValue { get; set; }
        public decimal? CriticalMaxValue { get; set; }
        public decimal WarningPct { get; set; }
        public decimal CriticalPct { get; set; }
    }

    public class MortalityReason : AuditableEntity
    {
        public int MortalityReasonID { get; set; }
        public string ReasonCode { get; set; }
        public string ReasonName { get; set; }
        public string Category { get; set; }
    }

    public class FeedFormula
    {
        public int FeedFormulaID { get; set; }
        public int FeedTypeID { get; set; }
        public string FeedTypeName { get; set; }
        public string FormulaCode { get; set; }
        public string FormulaName { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string Remarks { get; set; }
        public bool IsActive { get; set; }
        public int NutrientCount { get; set; }
        public IList<FeedFormulaDetail> Details { get; set; }

        public FeedFormula()
        {
            IsActive = true;
            Details = new List<FeedFormulaDetail>();
        }
    }

    public class FeedFormulaDetail
    {
        public int ParameterID { get; set; }
        public string ParameterCode { get; set; }
        public string ParameterName { get; set; }
        public string UnitCode { get; set; }
        public int DecimalPlaces { get; set; }
        public decimal? LogicalMin { get; set; }
        public decimal? LogicalMax { get; set; }
        public decimal? Value { get; set; }
    }

    public class PlacementRecord
    {
        public int FlockPlacementID { get; set; }
        public int FlockID { get; set; }
        public string FlockNumber { get; set; }
        public DateTime PlacementDate { get; set; }
        public int HouseID { get; set; }
        public string HouseName { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public string Source { get; set; }
        public decimal? ChickAvgWeight { get; set; }
        public bool IsInitial { get; set; }
        public string Remarks { get; set; }
        public string CreatedByName { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class TransferRecord
    {
        public int FlockTransferID { get; set; }
        public int FlockID { get; set; }
        public string FlockNumber { get; set; }
        public DateTime TransferDate { get; set; }
        public int? WeekNo { get; set; }
        public string TransferType { get; set; }
        public int? FromHouseID { get; set; }
        public string FromHouseName { get; set; }
        public int? ToHouseID { get; set; }
        public string ToHouseName { get; set; }
        public int? CounterFlockID { get; set; }
        public string CounterFlockNumber { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public string Reason { get; set; }
        public int? ApprovedBy { get; set; }
        public string CreatedByName { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class AdjustmentRecord
    {
        public int FlockAdjustmentID { get; set; }
        public int FlockID { get; set; }
        public string FlockNumber { get; set; }
        public int? WeekNo { get; set; }
        public DateTime AdjustmentDate { get; set; }
        public int MaleAdjustment { get; set; }
        public int FemaleAdjustment { get; set; }
        public string Reason { get; set; }
        public string ApprovedByName { get; set; }
        public string CreatedByName { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class MovementSearchCriteria
    {
        public int? FlockID { get; set; }
        public int? FarmID { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? RestrictFarmID { get; set; }
        public int PageNo { get; set; }
        public int PageSize { get; set; }

        public MovementSearchCriteria()
        {
            PageNo = 1;
            PageSize = 25;
        }
    }
}
