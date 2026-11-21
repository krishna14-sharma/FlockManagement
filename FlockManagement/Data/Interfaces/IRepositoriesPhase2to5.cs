using System;
using System.Collections.Generic;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Helpers;
using FlockManagement.Common.Interfaces;

namespace FlockManagement.Data.Interfaces
{
    public interface IParameterRepository
    {
        IList<Parameter> GetAll(bool activeOnly = true);
        IList<LookupItem> GetUnits();
    }

    public interface IStandardRepository
    {
        PagedResult<Standard> Search(StandardSearchCriteria criteria);
        IList<StandardGridRow> GetGrid(StandardGridKey key);
        void SaveGrid(StandardGridKey key, IEnumerable<StandardGridRow> rows, IUserContext user);
        void SetActive(int standardId, bool isActive, IUserContext user);
        IList<ParameterStandard> GetFlockStandards(int flockId, int ageWeek, int? feedTypeId, DateTime? asOf);
        IList<DateTime> GetEffectiveDates(int birdTypeId, int parameterId);
    }

    public interface IMortalityReasonRepository : IMasterRepository<MortalityReason> { }

    public interface IFeedFormulaRepository
    {
        PagedResult<FeedFormula> Search(string search, int? feedTypeId, bool? isActive, int pageNo, int pageSize);
        FeedFormula GetById(int feedFormulaId);
        int Save(FeedFormula formula, IUserContext user);
        IList<LookupItem> GetLookup(int? feedTypeId, DateTime? onDate);
        IList<FeedFormulaDetail> GetBlankDetails();
    }

    public interface IMovementRepository
    {
        PagedResult<PlacementRecord> SearchPlacements(MovementSearchCriteria criteria);
        int AddPlacement(PlacementRecord placement, IUserContext user);
        PagedResult<TransferRecord> SearchTransfers(MovementSearchCriteria criteria);
        int SaveTransfer(TransferRecord transfer, IUserContext user);
        PagedResult<AdjustmentRecord> SearchAdjustments(MovementSearchCriteria criteria);
        int SaveAdjustment(AdjustmentRecord adjustment, IUserContext user);
    }

    public interface IWeeklyRepository
    {
        WeeklyContext GetContext(int flockId, int? weekNo);
        WeeklyRecord Get(int flockWeeklyId);
        int? FindId(int flockId, int weekNo);
        int Save(WeeklyEntryInput input, IUserContext user);
        void SaveMortality(WeeklyEntryInput input, IUserContext user);
        void DeleteLast(int flockId, IUserContext user);
        PagedResult<WeeklyListItem> Search(WeeklySearchCriteria criteria);
        ProductionData GetProduction(int flockWeeklyId);
    }

    public interface IHatchResultRepository
    {
        PagedResult<HatchResult> Search(HatchResultSearchCriteria criteria);
        HatchResult Get(int hatchResultId);
        int Save(HatchResult result, IUserContext user);
        void Delete(int hatchResultId, IUserContext user);
    }

    public interface IAnalysisRepository
    {
        IList<ParameterAnalysis> GetWeeklyAnalysis(int flockWeeklyId);
        IList<FlockSnapshot> GetDashboard(DashboardFilter filter);
        IList<TrendPoint> GetTrend(int flockId, int? restrictFarmId);
        IList<FlockSnapshot> GetComparison(IEnumerable<int> flockIds, int? ageWeek, int? restrictFarmId);
        IList<ExceptionRow> GetExceptions(int? farmId, int? houseId, int? birdTypeId, bool criticalOnly, int? restrictFarmId);
    }

    public interface IAlertRepository
    {
        PagedResult<AlertRecord> Search(AlertSearchCriteria criteria);
        void Acknowledge(long alertId, string actionTaken, bool close, IUserContext user);
        int CountOpen(int? restrictFarmId);
    }

    public interface IReportRepository
    {
        System.Data.DataTable Run(ReportRequest request);
    }

    public interface IConfigurationRepository
    {
        ConfigurationSet Load();
        void SaveTolerances(IEnumerable<ToleranceSetting> rows, IUserContext user);
        void SaveWeights(IEnumerable<ScoreWeight> rows, IUserContext user);
        void SaveBands(IEnumerable<ScoreBandSetting> rows, IUserContext user);
        void SaveSettings(IEnumerable<SystemSetting> rows, IUserContext user);
        void SaveModules(IEnumerable<ModuleFlag> rows, IUserContext user);
    }
}
