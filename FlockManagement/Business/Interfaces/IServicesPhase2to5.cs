using System;
using System.Collections.Generic;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Helpers;

namespace FlockManagement.Business.Interfaces
{
    public interface IStandardService
    {
        PagedResult<Standard> Search(StandardSearchCriteria criteria);
        IList<Parameter> Parameters();
        /// <summary>Week rows 0..lastWeek for the grid, filled with existing values.</summary>
        IList<StandardGridRow> GetGrid(StandardGridKey key, int lastWeek);
        OperationResult SaveGrid(StandardGridKey key, IList<StandardGridRow> rows);
        OperationResult SetActive(int standardId, bool isActive);
        IList<DateTime> EffectiveDates(int birdTypeId, int parameterId);
        IList<ParameterStandard> ForFlock(int flockId, int ageWeek, int? feedTypeId, DateTime? asOf);
    }

    public interface IMortalityReasonService : IMasterService<MortalityReason> { }

    public interface IFeedFormulaService
    {
        PagedResult<FeedFormula> Search(string search, int? feedTypeId, bool? isActive, int pageNo);
        FeedFormula Get(int id);
        FeedFormula New();
        OperationResult<int> Save(FeedFormula formula);
        IList<LookupItem> Lookup(int? feedTypeId, DateTime? onDate);
    }

    public interface IMovementService
    {
        PagedResult<PlacementRecord> Placements(MovementSearchCriteria criteria);
        OperationResult<int> AddPlacement(PlacementRecord placement);
        PagedResult<TransferRecord> Transfers(MovementSearchCriteria criteria);
        OperationResult<int> SaveTransfer(TransferRecord transfer);
        PagedResult<AdjustmentRecord> Adjustments(MovementSearchCriteria criteria);
        OperationResult<int> SaveAdjustment(AdjustmentRecord adjustment);
    }

    public interface IHatchResultService
    {
        PagedResult<HatchResult> Search(HatchResultSearchCriteria criteria);
        HatchResult Get(int hatchResultId);
        OperationResult<int> Save(HatchResult result);
        OperationResult Delete(int hatchResultId);
    }

    /// <summary>Data for the weekly entry screen.</summary>
    public class WeeklyEntryData
    {
        public WeeklyContext Context { get; set; }
        public WeeklyRecord Existing { get; set; }
        public IList<ParameterStandard> Standards { get; set; }
        public decimal UniformityRangePct { get; set; }
    }

    public interface IWeeklyService
    {
        WeeklyEntryData GetEntry(int flockId, int? weekNo, int? feedTypeId);
        OperationResult<int> Save(WeeklyEntryInput input);
        OperationResult SaveMortality(WeeklyEntryInput input);
        OperationResult DeleteLast(int flockId);
        WeeklyRecord Get(int flockWeeklyId);
        PagedResult<WeeklyListItem> Search(WeeklySearchCriteria criteria);
    }

    public class WeeklyAnalysis
    {
        public WeeklyRecord Week { get; set; }
        public IList<ParameterAnalysis> Parameters { get; set; }
        public string UniformityGrade { get; set; }
        public decimal UniformityRangePct { get; set; }
    }

    public class DashboardData
    {
        public DashboardKpis Kpis { get; set; }
        public IList<FlockSnapshot> Flocks { get; set; }
    }

    public interface IAnalysisService
    {
        WeeklyAnalysis Weekly(int flockWeeklyId);
        DashboardData Dashboard(DashboardFilter filter);
        IList<TrendPoint> Trend(int flockId);
        IList<FlockSnapshot> Comparison(IEnumerable<int> flockIds, int? ageWeek);
        IList<ExceptionRow> Exceptions(int? farmId, int? houseId, int? birdTypeId, bool criticalOnly);
        string UniformityGrade(decimal? uniformityPct);
    }

    public interface IAlertService
    {
        PagedResult<AlertRecord> Search(AlertSearchCriteria criteria);
        OperationResult Acknowledge(long alertId, string actionTaken, bool close);
        int OpenCount();
    }

    public class ExportFile
    {
        public byte[] Content { get; set; }
        public string ContentType { get; set; }
        public string FileName { get; set; }
    }

    public interface IReportService
    {
        ReportResult Run(ReportRequest request, IDictionary<string, string> filterDescriptions);
        ExportFile Export(ReportResult result, string format);
    }

    public interface IConfigurationService
    {
        ConfigurationSet Load();
        OperationResult SaveTolerances(IList<ToleranceSetting> rows);
        OperationResult SaveWeights(IList<ScoreWeight> rows);
        OperationResult SaveBands(IList<ScoreBandSetting> rows);
        OperationResult SaveSettings(IList<SystemSetting> rows);
        OperationResult SaveModules(IList<ModuleFlag> rows);
    }
}
