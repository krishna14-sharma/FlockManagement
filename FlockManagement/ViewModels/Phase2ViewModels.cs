using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using FlockManagement.Business.Interfaces;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Helpers;

namespace FlockManagement.Web.ViewModels
{
    /// <summary>Farm -> House -> Flock picker shared by entry and analysis screens.</summary>
    public class FlockPickerModel
    {
        public int? FarmID { get; set; }
        public int? HouseID { get; set; }
        public int? FlockID { get; set; }
        public IEnumerable<SelectListItem> Farms { get; set; }
        public IEnumerable<SelectListItem> Houses { get; set; }
        public IEnumerable<SelectListItem> Flocks { get; set; }
        public string Action { get; set; }
        public string Controller { get; set; }
        /// <summary>Extra GET fields to keep (name -> value).</summary>
        public IDictionary<string, string> Extra { get; set; }
        public bool ShowWeek { get; set; }
        public int? WeekNo { get; set; }
        public IEnumerable<SelectListItem> Weeks { get; set; }

        public FlockPickerModel()
        {
            Extra = new Dictionary<string, string>();
            Farms = Houses = Flocks = Weeks = new List<SelectListItem>();
        }
    }

    // ---------------------------------------------------------------- Standards
    public class StandardListViewModel
    {
        public StandardSearchCriteria Criteria { get; set; }
        public PagedResult<Standard> Result { get; set; }
        public IEnumerable<SelectListItem> BirdTypes { get; set; }
        public IEnumerable<SelectListItem> Breeds { get; set; }
        public IEnumerable<SelectListItem> Parameters { get; set; }
        public IEnumerable<SelectListItem> Groups { get; set; }
        public bool CanEdit { get; set; }
    }

    public class StandardGridViewModel
    {
        [Display(Name = "Bird Type"), Range(1, int.MaxValue, ErrorMessage = "Select a bird type.")]
        public int BirdTypeID { get; set; }

        [Display(Name = "Breed (blank = all breeds)")]
        public int? BreedID { get; set; }

        [Display(Name = "Feed Type (blank = any feed)")]
        public int? FeedTypeID { get; set; }

        [Display(Name = "Parameter"), Range(1, int.MaxValue, ErrorMessage = "Select a parameter.")]
        public int ParameterID { get; set; }

        [Required, DataType(DataType.Date), Display(Name = "Effective From")]
        public DateTime? EffectiveFrom { get; set; }

        [DataType(DataType.Date), Display(Name = "Effective To")]
        public DateTime? EffectiveTo { get; set; }

        [Range(1, 120), Display(Name = "Show weeks up to")]
        public int LastWeek { get; set; }

        public bool Loaded { get; set; }
        public IList<StandardGridRow> Rows { get; set; }
        public Parameter SelectedParameter { get; set; }
        public IList<DateTime> ExistingDates { get; set; }
        public bool CanEdit { get; set; }

        public IEnumerable<SelectListItem> BirdTypes { get; set; }
        public IEnumerable<SelectListItem> Breeds { get; set; }
        public IEnumerable<SelectListItem> FeedTypes { get; set; }
        public IEnumerable<SelectListItem> Parameters { get; set; }

        public StandardGridViewModel()
        {
            LastWeek = 66;
            Rows = new List<StandardGridRow>();
            ExistingDates = new List<DateTime>();
        }

        public StandardGridKey ToKey()
        {
            return new StandardGridKey
            {
                BirdTypeID = BirdTypeID,
                BreedID = BreedID,
                FeedTypeID = FeedTypeID,
                ParameterID = ParameterID,
                EffectiveFrom = (EffectiveFrom ?? new DateTime(2020, 1, 1)).Date,
                EffectiveTo = EffectiveTo
            };
        }
    }

    // ---------------------------------------------------------------- Mortality reason
    public class MortalityReasonViewModel : MasterEditViewModel
    {
        public int MortalityReasonID { get; set; }

        [Required, StringLength(20), Display(Name = "Code")]
        [RegularExpression(@"^[A-Za-z0-9\-_]+$", ErrorMessage = "Use letters, digits, '-' or '_' only.")]
        public string ReasonCode { get; set; }

        [Required, StringLength(100), Display(Name = "Reason")]
        public string ReasonName { get; set; }

        [StringLength(50)]
        public string Category { get; set; }

        public IEnumerable<string> Categories
        {
            get { return new[] { "Disease", "Management", "Culling", "Other" }; }
        }

        public static MortalityReasonViewModel From(MortalityReason e)
        {
            return new MortalityReasonViewModel
            {
                MortalityReasonID = e.MortalityReasonID, ReasonCode = e.ReasonCode, ReasonName = e.ReasonName,
                Category = e.Category, IsActive = e.IsActive
            };
        }

        public MortalityReason ToEntity()
        {
            return new MortalityReason
            {
                MortalityReasonID = MortalityReasonID, ReasonCode = ReasonCode, ReasonName = ReasonName,
                Category = Category, IsActive = IsActive
            };
        }
    }

    // ---------------------------------------------------------------- Feed formula
    public class FeedFormulaListViewModel
    {
        public string Search { get; set; }
        public int? FeedTypeID { get; set; }
        public string Status { get; set; }
        public PagedResult<FeedFormula> Result { get; set; }
        public IEnumerable<SelectListItem> FeedTypes { get; set; }
        public bool CanEdit { get; set; }
    }

    public class FeedFormulaEditViewModel
    {
        public int FeedFormulaID { get; set; }

        [Display(Name = "Feed Type"), Range(1, int.MaxValue, ErrorMessage = "Feed type is required.")]
        public int FeedTypeID { get; set; }

        [Required, StringLength(30), Display(Name = "Formula Code")]
        [RegularExpression(@"^[A-Za-z0-9\-_/]+$", ErrorMessage = "Use letters, digits, '-', '_' or '/' only.")]
        public string FormulaCode { get; set; }

        [Required, StringLength(100), Display(Name = "Formula Name")]
        public string FormulaName { get; set; }

        [Required, DataType(DataType.Date), Display(Name = "Effective From")]
        public DateTime? EffectiveFrom { get; set; }

        [DataType(DataType.Date), Display(Name = "Effective To")]
        public DateTime? EffectiveTo { get; set; }

        [StringLength(250)]
        public string Remarks { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }

        public IList<FeedFormulaDetail> Details { get; set; }
        public IEnumerable<SelectListItem> FeedTypes { get; set; }

        public FeedFormulaEditViewModel()
        {
            IsActive = true;
            Details = new List<FeedFormulaDetail>();
        }

        public static FeedFormulaEditViewModel From(FeedFormula f)
        {
            return new FeedFormulaEditViewModel
            {
                FeedFormulaID = f.FeedFormulaID, FeedTypeID = f.FeedTypeID, FormulaCode = f.FormulaCode, FormulaName = f.FormulaName,
                EffectiveFrom = f.EffectiveFrom, EffectiveTo = f.EffectiveTo, Remarks = f.Remarks, IsActive = f.IsActive, Details = f.Details
            };
        }

        public FeedFormula ToEntity()
        {
            return new FeedFormula
            {
                FeedFormulaID = FeedFormulaID, FeedTypeID = FeedTypeID, FormulaCode = FormulaCode, FormulaName = FormulaName,
                EffectiveFrom = (EffectiveFrom ?? DateTime.Today).Date, EffectiveTo = EffectiveTo, Remarks = Remarks, IsActive = IsActive,
                Details = Details ?? new List<FeedFormulaDetail>()
            };
        }
    }

    // ---------------------------------------------------------------- Movements
    public class MovementListViewModel<T>
    {
        public FlockPickerModel Picker { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public PagedResult<T> Result { get; set; }
        public bool CanEdit { get; set; }
    }

    public class PlacementCreateViewModel
    {
        [Display(Name = "Flock"), Range(1, int.MaxValue, ErrorMessage = "Select a flock.")]
        public int FlockID { get; set; }

        [Required, DataType(DataType.Date), Display(Name = "Placement Date")]
        public DateTime? PlacementDate { get; set; }

        [Display(Name = "House (blank = flock's house)")]
        public int? HouseID { get; set; }

        [Range(0, 1000000), Display(Name = "Females")]
        public int FemaleCount { get; set; }

        [Range(0, 1000000), Display(Name = "Males")]
        public int MaleCount { get; set; }

        [StringLength(100), Display(Name = "Source / Hatchery")]
        public string Source { get; set; }

        [Range(typeof(decimal), "0.1", "5000"), Display(Name = "Avg. Weight (g)")]
        public decimal? ChickAvgWeight { get; set; }

        [StringLength(500)]
        public string Remarks { get; set; }

        public IEnumerable<SelectListItem> Flocks { get; set; }
        public IEnumerable<SelectListItem> Houses { get; set; }
    }

    public class TransferCreateViewModel
    {
        [Display(Name = "Flock"), Range(1, int.MaxValue, ErrorMessage = "Select a flock.")]
        public int FlockID { get; set; }

        [Required, Display(Name = "Transfer Type")]
        public string TransferType { get; set; }

        [Required, DataType(DataType.Date), Display(Name = "Transfer Date")]
        public DateTime? TransferDate { get; set; }

        [Display(Name = "New House")]
        public int? ToHouseID { get; set; }

        [Display(Name = "Other Flock (optional)")]
        public int? CounterFlockID { get; set; }

        [Range(0, 1000000), Display(Name = "Females")]
        public int FemaleCount { get; set; }

        [Range(0, 1000000), Display(Name = "Males")]
        public int MaleCount { get; set; }

        [StringLength(250)]
        public string Reason { get; set; }

        public IEnumerable<SelectListItem> Flocks { get; set; }
        public IEnumerable<SelectListItem> Houses { get; set; }

        public TransferCreateViewModel()
        {
            TransferType = "OUT";
        }
    }

    public class AdjustmentCreateViewModel
    {
        [Display(Name = "Flock"), Range(1, int.MaxValue, ErrorMessage = "Select a flock.")]
        public int FlockID { get; set; }

        [Required, DataType(DataType.Date), Display(Name = "Adjustment Date")]
        public DateTime? AdjustmentDate { get; set; }

        [Range(-1000000, 1000000), Display(Name = "Female Adjustment (+/-)")]
        public int FemaleAdjustment { get; set; }

        [Range(-1000000, 1000000), Display(Name = "Male Adjustment (+/-)")]
        public int MaleAdjustment { get; set; }

        [Required, StringLength(250, MinimumLength = 5), Display(Name = "Reason")]
        public string Reason { get; set; }

        public IEnumerable<SelectListItem> Flocks { get; set; }
    }

    // ---------------------------------------------------------------- Hatch results
    public class HatchResultListViewModel
    {
        public FlockPickerModel Picker { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public PagedResult<HatchResult> Result { get; set; }
        public bool CanEdit { get; set; }
    }

    public class HatchResultEditViewModel
    {
        public int HatchResultID { get; set; }

        [Display(Name = "Flock"), Range(1, int.MaxValue, ErrorMessage = "Select a flock.")]
        public int FlockID { get; set; }

        [Display(Name = "Eggs laid in week"), Range(1, 120, ErrorMessage = "Select the week the eggs were laid in.")]
        public int WeekNo { get; set; }

        [Required, DataType(DataType.Date), Display(Name = "Set Date")]
        public DateTime? SetDate { get; set; }

        [DataType(DataType.Date), Display(Name = "Hatch Date")]
        public DateTime? HatchDate { get; set; }

        [Range(1, 10000000, ErrorMessage = "Enter the number of eggs set."), Display(Name = "Eggs Set")]
        public int SettableEggs { get; set; }

        [Range(0, 10000000), Display(Name = "Infertile Eggs (candling)")]
        public int InfertileEggs { get; set; }

        [Range(0, 10000000), Display(Name = "Dead in Shell")]
        public int DeadInShell { get; set; }

        [Range(0, 10000000), Display(Name = "Chicks Hatched")]
        public int HatchedChicks { get; set; }

        [Range(0, 10000000), Display(Name = "Saleable Chicks")]
        public int? SaleableChicks { get; set; }

        [StringLength(250), Display(Name = "Remarks")]
        public string Remarks { get; set; }

        public IEnumerable<SelectListItem> Flocks { get; set; }
        public IEnumerable<SelectListItem> Weeks { get; set; }
    }

    // ---------------------------------------------------------------- Weekly entry
    public class WeeklyRegisterViewModel
    {
        public FlockPickerModel Picker { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public PagedResult<WeeklyListItem> Result { get; set; }
        public bool CanEnter { get; set; }
    }

    public class FeedLineInput
    {
        public int FeedTypeID { get; set; }
        public int? FeedFormulaID { get; set; }
        public string Sex { get; set; }
        public decimal? QuantityKg { get; set; }
        public decimal? WastageKg { get; set; }
    }

    public class NutrientInput
    {
        public int ParameterID { get; set; }
        public string ParameterCode { get; set; }
        public decimal? Value { get; set; }
    }

    public class MortalityReasonInput
    {
        public int MortalityReasonID { get; set; }
        public string Sex { get; set; }
        public int? BirdCount { get; set; }
    }

    public class WeeklyEntryViewModel
    {
        // selection
        public FlockPickerModel Picker { get; set; }
        public string Tab { get; set; }
        public WeeklyEntryData Data { get; set; }
        public bool CanDelete { get; set; }
        public string Message { get; set; }

        // posted fields
        public int FlockWeeklyID { get; set; }
        public int FlockID { get; set; }
        public int WeekNo { get; set; }

        [Required, DataType(DataType.Date), Display(Name = "Entry Date")]
        public DateTime? EntryDate { get; set; }

        [Display(Name = "Feed Type")]
        public int? FeedTypeID { get; set; }

        [Display(Name = "Feed Formula")]
        public int? FeedFormulaID { get; set; }

        [Range(0, 1000000), Display(Name = "Male Mortality")]
        public int MaleMortality { get; set; }

        [Range(0, 1000000), Display(Name = "Female Mortality")]
        public int FemaleMortality { get; set; }

        [Range(0, 1000000), Display(Name = "Male Culling")]
        public int MaleCulling { get; set; }

        [Range(0, 1000000), Display(Name = "Female Culling")]
        public int FemaleCulling { get; set; }

        [Range(typeof(decimal), "0", "9999"), Display(Name = "Body Weight (g) if not sampling")]
        public decimal? ManualBodyWeight { get; set; }

        [Display(Name = "Female sample weights (g)")]
        public string SamplesFemale { get; set; }

        [Display(Name = "Male sample weights (g)")]
        public string SamplesMale { get; set; }

        public string NutritionSource { get; set; }

        [StringLength(50), Display(Name = "Lab sample ref.")]
        public string LabSampleRef { get; set; }

        [DataType(DataType.Date), Display(Name = "Analysis date")]
        public DateTime? AnalysisDate { get; set; }

        [Display(Name = "Main mortality reason")]
        public int? MortalityReasonID { get; set; }

        [StringLength(500)]
        public string Remarks { get; set; }

        public string RowVersion { get; set; }

        public IList<FeedLineInput> FeedLines { get; set; }
        public IList<NutrientInput> Nutrients { get; set; }
        public IList<MortalityReasonInput> MortalityReasons { get; set; }
        public ProductionInput Production { get; set; }

        // lists
        public IEnumerable<SelectListItem> FeedTypes { get; set; }
        public IEnumerable<SelectListItem> Formulas { get; set; }
        public IEnumerable<SelectListItem> Reasons { get; set; }

        public WeeklyEntryViewModel()
        {
            Tab = "population";
            FeedLines = new List<FeedLineInput>();
            Nutrients = new List<NutrientInput>();
            MortalityReasons = new List<MortalityReasonInput>();
            Production = new ProductionInput();
            NutritionSource = "F";
        }

        public static IList<decimal> ParseWeights(string text)
        {
            var list = new List<decimal>();
            if (string.IsNullOrWhiteSpace(text)) return list;
            foreach (var part in text.Split(new[] { ' ', ',', ';', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                decimal d;
                if (decimal.TryParse(part, NumberStyles.Number, CultureInfo.InvariantCulture, out d)) list.Add(d);
                else throw new FormatException("'" + part + "' is not a weight in grams.");
            }
            return list;
        }

        public WeeklyEntryInput ToInput()
        {
            byte[] rowVer = null;
            if (!string.IsNullOrEmpty(RowVersion))
            {
                try { rowVer = Convert.FromBase64String(RowVersion); }
                catch (FormatException) { rowVer = null; }
            }
            var input = new WeeklyEntryInput
            {
                FlockWeeklyID = FlockWeeklyID,
                FlockID = FlockID,
                WeekNo = WeekNo,
                EntryDate = (EntryDate ?? DateTime.Today).Date,
                FeedTypeID = FeedTypeID,
                FeedFormulaID = FeedFormulaID,
                MaleMortality = MaleMortality,
                FemaleMortality = FemaleMortality,
                MaleCulling = MaleCulling,
                FemaleCulling = FemaleCulling,
                ManualBodyWeight = ManualBodyWeight,
                NutritionSource = NutritionSource,
                LabSampleRef = LabSampleRef,
                AnalysisDate = AnalysisDate,
                MortalityReasonID = MortalityReasonID,
                Remarks = Remarks,
                RowVer = rowVer,
                Production = Production ?? new ProductionInput()
            };
            var n = 0;
            foreach (var w in ParseWeights(SamplesFemale)) input.Samples.Add(new WeightSample { Sex = "F", SampleNo = ++n, Weight = w });
            n = 0;
            foreach (var w in ParseWeights(SamplesMale)) input.Samples.Add(new WeightSample { Sex = "M", SampleNo = ++n, Weight = w });

            foreach (var f in (FeedLines ?? new List<FeedLineInput>()).Where(f => f != null && f.FeedTypeID > 0 && f.QuantityKg.HasValue))
                input.FeedLines.Add(new FeedLine
                {
                    FeedTypeID = f.FeedTypeID,
                    FeedFormulaID = f.FeedFormulaID,
                    Sex = string.IsNullOrEmpty(f.Sex) ? "B" : f.Sex,
                    QuantityKg = f.QuantityKg.Value,
                    WastageKg = f.WastageKg ?? 0
                });
            if (NutritionSource == "L")
                foreach (var nu in (Nutrients ?? new List<NutrientInput>()).Where(x => x != null && x.Value.HasValue))
                    input.Nutrients.Add(new NutrientValue { ParameterID = nu.ParameterID, ParameterCode = nu.ParameterCode, Value = nu.Value });
            foreach (var m in (MortalityReasons ?? new List<MortalityReasonInput>()).Where(x => x != null && x.MortalityReasonID > 0 && x.BirdCount > 0))
                input.MortalityReasons.Add(new MortalityReasonLine { MortalityReasonID = m.MortalityReasonID, Sex = m.Sex ?? "F", BirdCount = m.BirdCount.Value });
            return input;
        }
    }

    // ---------------------------------------------------------------- Analysis
    public class WeeklyAnalysisViewModel
    {
        public FlockPickerModel Picker { get; set; }
        public WeeklyAnalysis Analysis { get; set; }
        public int? PrevWeekID { get; set; }
        public int? NextWeekID { get; set; }
        public bool CanEdit { get; set; }

        /// <summary>Spec order for the main analysis table.</summary>
        public static readonly string[] MainOrder =
        {
            "FEED_INTAKE", "WEIGHT_GAIN", "BODY_WEIGHT", "ENERGY", "PROTEIN", "METHIONINE", "LYSINE", "LINOLEIC_ACID",
            "CALCIUM", "PHOSPHORUS", "FAT", "FIBRE", "SALT", "MORTALITY", "UNIFORMITY"
        };

        public ParameterAnalysis P(string code)
        {
            return Analysis == null ? null : Analysis.Parameters.FirstOrDefault(p => p.ParameterCode == code);
        }
    }

    public class StandardVsActualViewModel
    {
        public FlockPickerModel Picker { get; set; }
        public int? BirdTypeID { get; set; }
        public IEnumerable<SelectListItem> BirdTypes { get; set; }
        public IList<FlockSnapshot> Flocks { get; set; }
    }

    public class FocusViewModel
    {
        public string Area { get; set; }
        public string Title { get; set; }
        public string Icon { get; set; }
        public FlockPickerModel Picker { get; set; }
        public Flock Flock { get; set; }
        public IList<TrendPoint> Points { get; set; }
        public IList<int> Weeks { get; set; }
        public IList<FlockSnapshot> AllFlocks { get; set; }
        public IDictionary<int, string> UniformityGrades { get; set; }

        public TrendPoint Get(int week, string code)
        {
            return Points == null ? null : Points.FirstOrDefault(p => p.WeekNo == week && p.ParameterCode == code);
        }

        public FocusViewModel()
        {
            Points = new List<TrendPoint>();
            Weeks = new List<int>();
            UniformityGrades = new Dictionary<int, string>();
        }
    }

    public class TrendViewModel
    {
        public FlockPickerModel Picker { get; set; }
        public Flock Flock { get; set; }
        public IList<string> Selected { get; set; }
        public IList<KeyValuePair<string, string>> Available { get; set; }
        public IList<TrendPoint> Points { get; set; }

        public TrendViewModel()
        {
            Selected = new List<string>();
            Available = new List<KeyValuePair<string, string>>();
            Points = new List<TrendPoint>();
        }
    }

    public class ComparisonViewModel
    {
        public IList<int> FlockIDs { get; set; }
        public int? AgeWeek { get; set; }
        public IList<LookupItem> AvailableFlocks { get; set; }
        public IList<FlockSnapshot> Rows { get; set; }

        public ComparisonViewModel()
        {
            FlockIDs = new List<int>();
            AvailableFlocks = new List<LookupItem>();
            Rows = new List<FlockSnapshot>();
        }
    }

    public class ExceptionsViewModel
    {
        public int? FarmID { get; set; }
        public int? HouseID { get; set; }
        public int? BirdTypeID { get; set; }
        public bool CriticalOnly { get; set; }
        public IEnumerable<SelectListItem> Farms { get; set; }
        public IEnumerable<SelectListItem> Houses { get; set; }
        public IEnumerable<SelectListItem> BirdTypes { get; set; }
        public IList<ExceptionRow> Rows { get; set; }
    }

    public class DashboardFilterViewModel
    {
        public int? FarmID { get; set; }
        public int? HouseID { get; set; }
        public int? BirdTypeID { get; set; }
        public int? FlockID { get; set; }
        public int? LotID { get; set; }
        public DateTime? AsOfDate { get; set; }
        public int? WeekNo { get; set; }
        public int? ChartFlockID { get; set; }

        public IEnumerable<SelectListItem> Farms { get; set; }
        public IEnumerable<SelectListItem> Houses { get; set; }
        public IEnumerable<SelectListItem> BirdTypes { get; set; }
        public IEnumerable<SelectListItem> Flocks { get; set; }
        public IEnumerable<SelectListItem> Lots { get; set; }
        public IEnumerable<SelectListItem> ChartFlocks { get; set; }
    }

    public class AnalysisDashboardViewModel
    {
        public DashboardFilterViewModel Filter { get; set; }
        public DashboardData Data { get; set; }
        public string UserFullName { get; set; }
        public string RoleName { get; set; }
        public bool CanSeeFlocks { get; set; }
    }

    // ---------------------------------------------------------------- Alerts
    public class AlertListViewModel
    {
        public string Status { get; set; }
        public string Severity { get; set; }
        public string AlertType { get; set; }
        public int? FarmID { get; set; }
        public int? FlockID { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public PagedResult<AlertRecord> Result { get; set; }
        public IEnumerable<SelectListItem> Farms { get; set; }
        public IEnumerable<SelectListItem> Flocks { get; set; }
        public bool CanAcknowledge { get; set; }
    }

    // ---------------------------------------------------------------- Reports
    public class ReportViewModel
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

        public ReportResult Result { get; set; }
        public bool CanExport { get; set; }
        public int MaxScreenRows { get; set; }

        public IEnumerable<SelectListItem> ReportTypes { get; set; }
        public IEnumerable<SelectListItem> Farms { get; set; }
        public IEnumerable<SelectListItem> Houses { get; set; }
        public IEnumerable<SelectListItem> BirdTypes { get; set; }
        public IEnumerable<SelectListItem> Flocks { get; set; }
        public IEnumerable<SelectListItem> Lots { get; set; }

        public ReportViewModel()
        {
            MaxScreenRows = 1000;
        }

        public ReportRequest ToRequest()
        {
            return new ReportRequest
            {
                ReportType = ReportType, FarmID = FarmID, HouseID = HouseID, BirdTypeID = BirdTypeID, FlockID = FlockID, LotID = LotID,
                FromWeek = FromWeek, ToWeek = ToWeek, FromDate = FromDate, ToDate = ToDate, ActiveOnly = ActiveOnly
            };
        }
    }

    // ---------------------------------------------------------------- Configuration
    public class ConfigurationViewModel
    {
        public ConfigurationSet Config { get; set; }
        public string Tab { get; set; }
        public IEnumerable<StatusDefinition> Statuses { get; set; }
    }
}
