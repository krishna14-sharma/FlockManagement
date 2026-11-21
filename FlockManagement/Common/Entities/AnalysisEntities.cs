using System;
using System.Collections.Generic;
using System.Data;

namespace FlockManagement.Common.Entities
{
    /// <summary>Standard vs actual for one parameter of one week (fn_WeeklyParameters).</summary>
    public class ParameterAnalysis
    {
        public int ParameterID { get; set; }
        public string ParameterCode { get; set; }
        public string ParameterName { get; set; }
        public string ParameterGroup { get; set; }
        public string UnitCode { get; set; }
        public string Direction { get; set; }
        public int DecimalPlaces { get; set; }
        public int SortOrder { get; set; }
        public decimal? StandardValue { get; set; }
        public decimal? MinValue { get; set; }
        public decimal? MaxValue { get; set; }
        public decimal? ActualValue { get; set; }
        public decimal? Variance { get; set; }
        public decimal? VariancePct { get; set; }
        public string StatusCode { get; set; }
        public decimal? PreviousActual { get; set; }
        public string PreviousStatusCode { get; set; }

        public decimal? ChangeFromPrevious
        {
            get { return ActualValue.HasValue && PreviousActual.HasValue ? ActualValue - PreviousActual : null; }
        }
    }

    /// <summary>Latest-week snapshot of one flock (dashboard, comparison, standard vs actual matrix).</summary>
    public class FlockSnapshot
    {
        public int FlockID { get; set; }
        public string FlockNumber { get; set; }
        public string LotNumber { get; set; }
        public string BirdTypeName { get; set; }
        public string BreedName { get; set; }
        public string FarmName { get; set; }
        public string HouseName { get; set; }
        public int TotalBirdCount { get; set; }
        public int CurrentFemaleCount { get; set; }
        public int CurrentMaleCount { get; set; }
        public int CurrentAge { get; set; }
        public int? FlockWeeklyID { get; set; }
        public int? WeekNo { get; set; }
        public DateTime? EntryDate { get; set; }
        public int? ClosingPopulation { get; set; }
        public decimal? PerformanceScore { get; set; }
        public string OverallStatusCode { get; set; }
        public string BandName { get; set; }
        public decimal? StdBodyWeight { get; set; }
        public decimal? BodyWeight { get; set; }
        public decimal? BodyWeightVarPct { get; set; }
        public string BodyWeightStatus { get; set; }
        public decimal? StdWeightGain { get; set; }
        public decimal? WeightGain { get; set; }
        public string WeightGainStatus { get; set; }
        public decimal? StdFeedIntake { get; set; }
        public decimal? FeedIntake { get; set; }
        public decimal? FeedIntakeVarPct { get; set; }
        public string FeedIntakeStatus { get; set; }
        public decimal? StdMortality { get; set; }
        public decimal? Mortality { get; set; }
        public string MortalityStatus { get; set; }
        public decimal? StdUniformity { get; set; }
        public decimal? Uniformity { get; set; }
        public string UniformityStatus { get; set; }
        public int NutritionOut { get; set; }
        public int NutritionChecked { get; set; }
        public int CriticalCount { get; set; }
        public int OutOfStandardCount { get; set; }
        public int OpenAlerts { get; set; }

        public bool HasData { get { return FlockWeeklyID.HasValue; } }
    }

    public class DashboardFilter
    {
        public int? FarmID { get; set; }
        public int? HouseID { get; set; }
        public int? BirdTypeID { get; set; }
        public int? FlockID { get; set; }
        public int? LotID { get; set; }
        public DateTime? AsOfDate { get; set; }
        public int? WeekNo { get; set; }
        public int? RestrictFarmID { get; set; }
    }

    public class DashboardKpis
    {
        public int ActiveFlocks { get; set; }
        public int FlocksWithData { get; set; }
        public int TotalBirds { get; set; }
        public decimal? AvgBodyWeight { get; set; }
        public decimal? AvgBodyWeightVarPct { get; set; }
        public decimal? AvgWeightGain { get; set; }
        public decimal? AvgFeedIntake { get; set; }
        public decimal? AvgMortalityPct { get; set; }
        public decimal? AvgUniformity { get; set; }
        public decimal? AvgScore { get; set; }
        public int FlocksBelowStandard { get; set; }
        public int FlocksAboveStandard { get; set; }
        public int CriticalFlocks { get; set; }
        public int OpenAlerts { get; set; }
    }

    /// <summary>One point of a trend (week x parameter).</summary>
    public class TrendPoint
    {
        public int FlockWeeklyID { get; set; }
        public int WeekNo { get; set; }
        public int AgeInWeeks { get; set; }
        public DateTime EntryDate { get; set; }
        public string ParameterCode { get; set; }
        public string ParameterName { get; set; }
        public string ParameterGroup { get; set; }
        public string UnitCode { get; set; }
        public int DecimalPlaces { get; set; }
        public int SortOrder { get; set; }
        public decimal? StandardValue { get; set; }
        public decimal? MinValue { get; set; }
        public decimal? MaxValue { get; set; }
        public decimal? ActualValue { get; set; }
        public decimal? VariancePct { get; set; }
        public string StatusCode { get; set; }
    }

    public class ExceptionRow
    {
        public int FlockID { get; set; }
        public string FlockNumber { get; set; }
        public string LotNumber { get; set; }
        public string FarmName { get; set; }
        public string HouseName { get; set; }
        public string BirdTypeName { get; set; }
        public int FlockWeeklyID { get; set; }
        public int WeekNo { get; set; }
        public int AgeInWeeks { get; set; }
        public DateTime EntryDate { get; set; }
        public string ParameterCode { get; set; }
        public string ParameterName { get; set; }
        public string ParameterGroup { get; set; }
        public string UnitCode { get; set; }
        public int DecimalPlaces { get; set; }
        public decimal? StandardValue { get; set; }
        public decimal? ActualValue { get; set; }
        public decimal? Variance { get; set; }
        public decimal? VariancePct { get; set; }
        public string StatusCode { get; set; }
        public string Severity { get; set; }
    }

    public class AlertRecord
    {
        public long AlertID { get; set; }
        public int FlockID { get; set; }
        public string FlockNumber { get; set; }
        public string FarmName { get; set; }
        public string HouseName { get; set; }
        public int? FlockWeeklyID { get; set; }
        public int WeekNo { get; set; }
        public DateTime AlertDate { get; set; }
        public string AlertType { get; set; }
        public int? ParameterID { get; set; }
        public string ParameterName { get; set; }
        public string UnitCode { get; set; }
        public int DecimalPlaces { get; set; }
        public decimal? StandardValue { get; set; }
        public decimal? ActualValue { get; set; }
        public decimal? Deviation { get; set; }
        public decimal? DeviationPct { get; set; }
        public string Severity { get; set; }
        public string Message { get; set; }
        public string Status { get; set; }
        public string ActionTaken { get; set; }
        public string AcknowledgedByName { get; set; }
        public DateTime? AcknowledgedDate { get; set; }
    }

    public class AlertSearchCriteria
    {
        public string Status { get; set; }
        public string Severity { get; set; }
        public string AlertType { get; set; }
        public int? FlockID { get; set; }
        public int? FarmID { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? RestrictFarmID { get; set; }
        public int PageNo { get; set; }
        public int PageSize { get; set; }

        public AlertSearchCriteria()
        {
            PageNo = 1;
            PageSize = 25;
        }
    }

    public class ReportRequest
    {
        public string ReportType { get; set; }
        public int? FarmID { get; set; }
        public int? HouseID { get; set; }
        public int? BirdTypeID { get; set; }
        public int? FlockID { get; set; }
        public int? LotID { get; set; }
        public int? FromWeek { get; set; }
        public int? ToWeek { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public bool ActiveOnly { get; set; }
        public int? RestrictFarmID { get; set; }
    }

    public class ReportResult
    {
        public string ReportType { get; set; }
        public string Title { get; set; }
        public IList<string> Filters { get; set; }
        public DataTable Table { get; set; }
        public DateTime GeneratedAt { get; set; }
        public string GeneratedBy { get; set; }

        public ReportResult()
        {
            Filters = new List<string>();
            GeneratedAt = DateTime.Now;
        }
    }

    /// <summary>Report catalogue (type code, title, description).</summary>
    public static class ReportCatalog
    {
        public static readonly IList<KeyValuePair<string, string>> Types = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("WEEKLY_PERFORMANCE", "Weekly Flock Performance Report"),
            new KeyValuePair<string, string>("PRODUCTION_PERFORMANCE", "Flock Production Performance Analysis"),
            new KeyValuePair<string, string>("FLOCK_WEIGHT", "Flock Weight Report"),
            new KeyValuePair<string, string>("FEED", "Feed Consumption Report"),
            new KeyValuePair<string, string>("MORTALITY", "Mortality Report"),
            new KeyValuePair<string, string>("NUTRITION", "Nutritional Report"),
            new KeyValuePair<string, string>("UNIFORMITY", "Uniformity Report"),
            new KeyValuePair<string, string>("STD_VS_ACTUAL", "Standard vs Actual Report"),
            new KeyValuePair<string, string>("FLOCK_COMPARISON", "Flock Comparison Report"),
            new KeyValuePair<string, string>("FARM_WISE", "Farm-wise Performance Report"),
            new KeyValuePair<string, string>("AGE_WISE", "Flock Age-wise Performance Report")
        };

        public static string TitleOf(string type)
        {
            foreach (var t in Types)
                if (string.Equals(t.Key, type, StringComparison.OrdinalIgnoreCase)) return t.Value;
            return null;
        }
    }

    public class ToleranceSetting
    {
        public int ToleranceID { get; set; }
        public int? ParameterID { get; set; }
        public string ParameterName { get; set; }
        public decimal WarningPct { get; set; }
        public decimal CriticalPct { get; set; }
    }

    public class ScoreWeight
    {
        public int ScoreWeightageID { get; set; }
        public string ComponentCode { get; set; }
        public string ComponentName { get; set; }
        public decimal Weightage { get; set; }
        public decimal ZeroScoreDeviationPct { get; set; }
    }

    public class ScoreBandSetting
    {
        public int ScoreBandID { get; set; }
        public string BandName { get; set; }
        public decimal MinScore { get; set; }
        public decimal MaxScore { get; set; }
        public string StatusCode { get; set; }
    }

    public class SystemSetting
    {
        public string SettingKey { get; set; }
        public string SettingValue { get; set; }
        public string DataType { get; set; }
        public string Description { get; set; }
    }

    public class ModuleFlag
    {
        public string ModuleCode { get; set; }
        public string ModuleName { get; set; }
        public bool IsEnabled { get; set; }
    }

    public class ConfigurationSet
    {
        public IList<ToleranceSetting> Tolerances { get; set; }
        public IList<ScoreWeight> Weights { get; set; }
        public IList<ScoreBandSetting> Bands { get; set; }
        public IList<SystemSetting> Settings { get; set; }
        public IList<ModuleFlag> Modules { get; set; }

        public ConfigurationSet()
        {
            Tolerances = new List<ToleranceSetting>();
            Weights = new List<ScoreWeight>();
            Bands = new List<ScoreBandSetting>();
            Settings = new List<SystemSetting>();
            Modules = new List<ModuleFlag>();
        }
    }
}
