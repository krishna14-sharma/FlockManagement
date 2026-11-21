using System;
using System.Collections.Generic;

namespace FlockManagement.Common.Entities
{
    /// <summary>Everything the weekly entry screen needs before the user types anything.</summary>
    public class WeeklyContext
    {
        public int FlockID { get; set; }
        public string FlockNumber { get; set; }
        public string LotNumber { get; set; }
        public int BirdTypeID { get; set; }
        public string BirdTypeName { get; set; }
        public int BreedID { get; set; }
        public string BreedName { get; set; }
        public int FarmID { get; set; }
        public string FarmName { get; set; }
        public int HouseID { get; set; }
        public string HouseName { get; set; }
        public DateTime HatchDate { get; set; }
        public DateTime PlacementDate { get; set; }
        public string Status { get; set; }
        public int PlacedMales { get; set; }
        public int PlacedFemales { get; set; }
        public int FirstWeek { get; set; }
        public int? LastWeek { get; set; }
        public int NextWeek { get; set; }
        public int WeekNo { get; set; }
        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }
        public int? FlockWeeklyID { get; set; }
        public int OpeningMale { get; set; }
        public int OpeningFemale { get; set; }
        public int ReceivedMale { get; set; }
        public int ReceivedFemale { get; set; }
        public int TransferOutMale { get; set; }
        public int TransferOutFemale { get; set; }
        public decimal? PrevBodyWeight { get; set; }
        public decimal? PrevWeightGain { get; set; }
        public decimal? PrevFeedIntake { get; set; }
        public decimal? PrevUniformity { get; set; }
        public decimal? PrevMortalityPct { get; set; }
        public decimal? PrevScore { get; set; }
        public decimal? ChickAvgWeight { get; set; }
        public int? SuggestedFeedTypeID { get; set; }
        public bool CanEnter { get; set; }

        public bool IsNew { get { return !FlockWeeklyID.HasValue; } }
        public int OpeningPopulation { get { return OpeningMale + OpeningFemale; } }
        /// <summary>Sex used for body-weight sampling: females when the flock has any.</summary>
        public string MainSex { get { return PlacedFemales > 0 ? "F" : "M"; } }
    }

    public class WeightSample
    {
        public string Sex { get; set; }
        public int SampleNo { get; set; }
        public decimal Weight { get; set; }
    }

    public class FeedLine
    {
        public int FeedTypeID { get; set; }
        public string FeedTypeName { get; set; }
        public int? FeedFormulaID { get; set; }
        public string FormulaCode { get; set; }
        public string Sex { get; set; }
        public decimal QuantityKg { get; set; }
        public decimal WastageKg { get; set; }
        public int BirdDays { get; set; }
        public decimal? IntakeGPerBirdDay { get; set; }
    }

    public class NutrientValue
    {
        public int ParameterID { get; set; }
        public string ParameterCode { get; set; }
        public decimal? Value { get; set; }
    }

    public class MortalityReasonLine
    {
        public int MortalityReasonID { get; set; }
        public string ReasonName { get; set; }
        public string Sex { get; set; }
        public int BirdCount { get; set; }
    }

    /// <summary>What the user enters for one flock-week (saved by sp_FlockWeekly_Save).</summary>
    public class WeeklyEntryInput
    {
        public int FlockWeeklyID { get; set; }
        public int FlockID { get; set; }
        public int WeekNo { get; set; }
        public DateTime EntryDate { get; set; }
        public int? FeedTypeID { get; set; }
        public int? FeedFormulaID { get; set; }
        public int MaleMortality { get; set; }
        public int FemaleMortality { get; set; }
        public int MaleCulling { get; set; }
        public int FemaleCulling { get; set; }
        public decimal? ManualBodyWeight { get; set; }
        public decimal? TotalFeedKg { get; set; }
        public decimal? FeedWastageKg { get; set; }
        /// <summary>L = lab analysis, F = feed formula values.</summary>
        public string NutritionSource { get; set; }
        public string LabSampleRef { get; set; }
        public DateTime? AnalysisDate { get; set; }
        public int? MortalityReasonID { get; set; }
        public string Remarks { get; set; }
        public byte[] RowVer { get; set; }
        public IList<WeightSample> Samples { get; set; }
        public IList<FeedLine> FeedLines { get; set; }
        public IList<NutrientValue> Nutrients { get; set; }
        public IList<MortalityReasonLine> MortalityReasons { get; set; }
        public ProductionInput Production { get; set; }

        public WeeklyEntryInput()
        {
            Production = new ProductionInput();
            Samples = new List<WeightSample>();
            FeedLines = new List<FeedLine>();
            Nutrients = new List<NutrientValue>();
            MortalityReasons = new List<MortalityReasonLine>();
        }
    }

    /// <summary>A saved weekly row with its calculated values (sp_FlockWeekly_Get).</summary>
    public class WeeklyRecord
    {
        public int FlockWeeklyID { get; set; }
        public int FlockID { get; set; }
        public string FlockNumber { get; set; }
        public string LotNumber { get; set; }
        public int BirdTypeID { get; set; }
        public string BirdTypeName { get; set; }
        public string BreedName { get; set; }
        public int FarmID { get; set; }
        public string FarmName { get; set; }
        public string HouseName { get; set; }
        public int WeekNo { get; set; }
        public int AgeInWeeks { get; set; }
        public DateTime EntryDate { get; set; }
        public int OpeningPopulation { get; set; }
        public int OpeningMale { get; set; }
        public int OpeningFemale { get; set; }
        public int BirdsReceived { get; set; }
        public int MaleMortality { get; set; }
        public int FemaleMortality { get; set; }
        public int TotalMortality { get; set; }
        public int MaleCulling { get; set; }
        public int FemaleCulling { get; set; }
        public int TransferOut { get; set; }
        public int ClosingPopulation { get; set; }
        public int ClosingMale { get; set; }
        public int ClosingFemale { get; set; }
        public decimal? MortalityPercentage { get; set; }
        public decimal? StandardMortalityPct { get; set; }
        public int? FeedTypeID { get; set; }
        public string FeedTypeName { get; set; }
        public int? FeedFormulaID { get; set; }
        public string FormulaCode { get; set; }
        public decimal? TotalFeedKg { get; set; }
        public decimal? FeedWastageKg { get; set; }
        public decimal? StandardFeedIntake { get; set; }
        public decimal? ActualFeedIntake { get; set; }
        public decimal? StandardWeightGain { get; set; }
        public decimal? ActualWeightGain { get; set; }
        public decimal? StandardBodyWeight { get; set; }
        public decimal? ActualBodyWeight { get; set; }
        public decimal? ManualBodyWeight { get; set; }
        public decimal? MinBodyWeight { get; set; }
        public decimal? MaxBodyWeight { get; set; }
        public decimal? StdDevBodyWeight { get; set; }
        public decimal? CVPercentage { get; set; }
        public int? SampleSize { get; set; }
        public decimal? StandardUniformity { get; set; }
        public decimal? UniformityPercentage { get; set; }
        public decimal? PerformanceScore { get; set; }
        public string OverallStatusCode { get; set; }
        public string BandName { get; set; }
        public decimal? BodyWeightScore { get; set; }
        public decimal? WeightGainScore { get; set; }
        public decimal? FeedIntakeScore { get; set; }
        public decimal? UniformityScore { get; set; }
        public decimal? MortalityScore { get; set; }
        public decimal? NutritionScore { get; set; }
        public string NutritionSource { get; set; }
        public string LabSampleRef { get; set; }
        public DateTime? AnalysisDate { get; set; }
        public string Remarks { get; set; }
        public byte[] RowVer { get; set; }
        public int LastWeek { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }

        public IList<WeightSample> Samples { get; set; }
        public IList<FeedLine> FeedLines { get; set; }
        public IList<NutrientValue> Nutrients { get; set; }
        public IList<MortalityReasonLine> MortalityReasons { get; set; }
        public int? MortalityReasonID { get; set; }
        public ProductionData Production { get; set; }

        public bool IsLatest { get { return WeekNo == LastWeek; } }

        public WeeklyRecord()
        {
            Samples = new List<WeightSample>();
            FeedLines = new List<FeedLine>();
            Nutrients = new List<NutrientValue>();
            MortalityReasons = new List<MortalityReasonLine>();
        }
    }

    public class WeeklySearchCriteria
    {
        public int? FarmID { get; set; }
        public int? HouseID { get; set; }
        public int? FlockID { get; set; }
        public int? FromWeek { get; set; }
        public int? ToWeek { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? RestrictFarmID { get; set; }
        public int PageNo { get; set; }
        public int PageSize { get; set; }

        public WeeklySearchCriteria()
        {
            PageNo = 1;
            PageSize = 25;
        }
    }

    public class WeeklyListItem
    {
        public int FlockWeeklyID { get; set; }
        public int FlockID { get; set; }
        public string FlockNumber { get; set; }
        public string FarmName { get; set; }
        public string HouseName { get; set; }
        public int WeekNo { get; set; }
        public DateTime EntryDate { get; set; }
        public int OpeningPopulation { get; set; }
        public int TotalMortality { get; set; }
        public int ClosingPopulation { get; set; }
        public decimal? ActualBodyWeight { get; set; }
        public decimal? StandardBodyWeight { get; set; }
        public decimal? ActualFeedIntake { get; set; }
        public decimal? UniformityPercentage { get; set; }
        public decimal? MortalityPercentage { get; set; }
        public decimal? PerformanceScore { get; set; }
        public string OverallStatusCode { get; set; }
        public bool IsLatest { get; set; }
        public string CreatedByName { get; set; }
    }
}
