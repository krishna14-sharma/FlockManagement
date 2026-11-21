using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using FlockManagement.Common.Constants;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Helpers;
using FlockManagement.Common.Interfaces;
using FlockManagement.Data.Infrastructure;
using FlockManagement.Data.Interfaces;

namespace FlockManagement.Data.Repositories
{
    public class AnalysisRepository : IAnalysisRepository
    {
        private readonly IDb _db;

        public AnalysisRepository(IDb db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        public IList<ParameterAnalysis> GetWeeklyAnalysis(int flockWeeklyId)
        {
            return _db.QueryProc("dbo.sp_GetFlockWeeklyAnalysis", r => new ParameterAnalysis
            {
                ParameterID = r.Get<int>("ParameterID"),
                ParameterCode = r.Str("ParameterCode"),
                ParameterName = r.Str("ParameterName"),
                ParameterGroup = r.Str("ParameterGroup"),
                UnitCode = r.Str("UnitCode"),
                Direction = r.Str("Direction"),
                DecimalPlaces = r.Get<int>("DecimalPlaces"),
                SortOrder = r.Get<int>("SortOrder"),
                StandardValue = r.Get<decimal?>("StandardValue"),
                MinValue = r.Get<decimal?>("MinValue"),
                MaxValue = r.Get<decimal?>("MaxValue"),
                ActualValue = r.Get<decimal?>("ActualValue"),
                Variance = r.Get<decimal?>("Variance"),
                VariancePct = r.Get<decimal?>("VariancePct"),
                StatusCode = r.Str("StatusCode"),
                PreviousActual = r.Get<decimal?>("PreviousActual"),
                PreviousStatusCode = r.Str("PreviousStatusCode")
            }, Sql.P("@FlockWeeklyID", flockWeeklyId));
        }

        public IList<FlockSnapshot> GetDashboard(DashboardFilter f)
        {
            f = f ?? new DashboardFilter();
            return _db.QueryProc("dbo.sp_GetFlockDashboard", MapSnapshot,
                Sql.P("@FarmID", f.FarmID), Sql.P("@HouseID", f.HouseID), Sql.P("@BirdTypeID", f.BirdTypeID), Sql.P("@FlockID", f.FlockID),
                Sql.P("@LotID", f.LotID), Sql.Date("@AsOfDate", f.AsOfDate), Sql.P("@WeekNo", f.WeekNo), Sql.P("@RestrictFarmID", f.RestrictFarmID));
        }

        public IList<TrendPoint> GetTrend(int flockId, int? restrictFarmId)
        {
            return _db.QueryProc("dbo.sp_GetFlockTrend", r => new TrendPoint
            {
                FlockWeeklyID = r.Get<int>("FlockWeeklyID"),
                WeekNo = r.Get<int>("WeekNo"),
                AgeInWeeks = r.Get<int>("AgeInWeeks"),
                EntryDate = r.Get<DateTime>("EntryDate"),
                ParameterCode = r.Str("ParameterCode"),
                ParameterName = r.Str("ParameterName"),
                ParameterGroup = r.Str("ParameterGroup"),
                UnitCode = r.Str("UnitCode"),
                DecimalPlaces = r.Get<int>("DecimalPlaces"),
                SortOrder = r.Get<int>("SortOrder"),
                StandardValue = r.Get<decimal?>("StandardValue"),
                MinValue = r.Get<decimal?>("MinValue"),
                MaxValue = r.Get<decimal?>("MaxValue"),
                ActualValue = r.Get<decimal?>("ActualValue"),
                VariancePct = r.Get<decimal?>("VariancePct"),
                StatusCode = r.Str("StatusCode")
            }, Sql.P("@FlockID", flockId), Sql.P("@RestrictFarmID", restrictFarmId));
        }

        public IList<FlockSnapshot> GetComparison(IEnumerable<int> flockIds, int? ageWeek, int? restrictFarmId)
        {
            var csv = string.Join(",", (flockIds ?? Enumerable.Empty<int>()).Distinct());
            if (csv.Length == 0) return new List<FlockSnapshot>();
            return _db.QueryProc("dbo.sp_GetFlockComparison", MapSnapshot,
                Sql.P("@FlockIDs", csv), Sql.P("@AgeWeek", ageWeek), Sql.P("@RestrictFarmID", restrictFarmId));
        }

        public IList<ExceptionRow> GetExceptions(int? farmId, int? houseId, int? birdTypeId, bool criticalOnly, int? restrictFarmId)
        {
            return _db.QueryProc("dbo.sp_GetExceptions", r => new ExceptionRow
            {
                FlockID = r.Get<int>("FlockID"),
                FlockNumber = r.Str("FlockNumber"),
                LotNumber = r.Str("LotNumber"),
                FarmName = r.Str("FarmName"),
                HouseName = r.Str("HouseName"),
                BirdTypeName = r.Str("BirdTypeName"),
                FlockWeeklyID = r.Get<int>("FlockWeeklyID"),
                WeekNo = r.Get<int>("WeekNo"),
                AgeInWeeks = r.Get<int>("AgeInWeeks"),
                EntryDate = r.Get<DateTime>("EntryDate"),
                ParameterCode = r.Str("ParameterCode"),
                ParameterName = r.Str("ParameterName"),
                ParameterGroup = r.Str("ParameterGroup"),
                UnitCode = r.Str("UnitCode"),
                DecimalPlaces = r.Get<int>("DecimalPlaces"),
                StandardValue = r.Get<decimal?>("StandardValue"),
                ActualValue = r.Get<decimal?>("ActualValue"),
                Variance = r.Get<decimal?>("Variance"),
                VariancePct = r.Get<decimal?>("VariancePct"),
                StatusCode = r.Str("StatusCode"),
                Severity = r.Str("Severity")
            },
                Sql.P("@FarmID", farmId), Sql.P("@HouseID", houseId), Sql.P("@BirdTypeID", birdTypeId),
                Sql.P("@CriticalOnly", criticalOnly), Sql.P("@RestrictFarmID", restrictFarmId));
        }

        private static FlockSnapshot MapSnapshot(IDataRecord r)
        {
            return new FlockSnapshot
            {
                FlockID = r.Get<int>("FlockID"),
                FlockNumber = r.Str("FlockNumber"),
                LotNumber = r.Str("LotNumber"),
                BirdTypeName = r.Str("BirdTypeName"),
                BreedName = r.HasColumn("BreedName") ? r.Str("BreedName") : null,
                FarmName = r.Str("FarmName"),
                HouseName = r.Str("HouseName"),
                TotalBirdCount = r.HasColumn("TotalBirdCount") ? r.Get<int>("TotalBirdCount") : r.Get<int>("ClosingPopulation"),
                CurrentFemaleCount = r.HasColumn("CurrentFemaleCount") ? r.Get<int>("CurrentFemaleCount") : 0,
                CurrentMaleCount = r.HasColumn("CurrentMaleCount") ? r.Get<int>("CurrentMaleCount") : 0,
                CurrentAge = r.HasColumn("CurrentAge") ? r.Get<int>("CurrentAge") : r.Get<int>("AgeInWeeks"),
                FlockWeeklyID = r.Get<int?>("FlockWeeklyID"),
                WeekNo = r.Get<int?>("WeekNo"),
                EntryDate = r.Get<DateTime?>("EntryDate"),
                ClosingPopulation = r.Get<int?>("ClosingPopulation"),
                PerformanceScore = r.Get<decimal?>("PerformanceScore"),
                OverallStatusCode = r.Str("OverallStatusCode"),
                BandName = r.Str("BandName"),
                StdBodyWeight = r.Get<decimal?>("StdBodyWeight"),
                BodyWeight = r.Get<decimal?>("BodyWeight"),
                BodyWeightVarPct = r.Get<decimal?>("BodyWeightVarPct"),
                BodyWeightStatus = r.Str("BodyWeightStatus"),
                StdWeightGain = r.Get<decimal?>("StdWeightGain"),
                WeightGain = r.Get<decimal?>("WeightGain"),
                WeightGainStatus = r.Str("WeightGainStatus"),
                StdFeedIntake = r.Get<decimal?>("StdFeedIntake"),
                FeedIntake = r.Get<decimal?>("FeedIntake"),
                FeedIntakeVarPct = r.Get<decimal?>("FeedIntakeVarPct"),
                FeedIntakeStatus = r.Str("FeedIntakeStatus"),
                StdMortality = r.Get<decimal?>("StdMortality"),
                Mortality = r.Get<decimal?>("Mortality"),
                MortalityStatus = r.Str("MortalityStatus"),
                StdUniformity = r.Get<decimal?>("StdUniformity"),
                Uniformity = r.Get<decimal?>("Uniformity"),
                UniformityStatus = r.Str("UniformityStatus"),
                NutritionOut = r.Get<int>("NutritionOut"),
                NutritionChecked = r.Get<int>("NutritionChecked"),
                CriticalCount = r.HasColumn("CriticalCount") ? r.Get<int>("CriticalCount") : 0,
                OutOfStandardCount = r.HasColumn("OutOfStandardCount") ? r.Get<int>("OutOfStandardCount") : 0,
                OpenAlerts = r.HasColumn("OpenAlerts") ? r.Get<int>("OpenAlerts") : 0
            };
        }
    }

    public class AlertRepository : IAlertRepository
    {
        private readonly IDb _db;

        public AlertRepository(IDb db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        public PagedResult<AlertRecord> Search(AlertSearchCriteria c)
        {
            c = c ?? new AlertSearchCriteria();
            int page = Math.Max(1, c.PageNo), size = c.PageSize < 1 || c.PageSize > 500 ? 25 : c.PageSize, total = 0;
            var items = _db.QueryProc("dbo.sp_GetAlerts", r =>
            {
                total = r.Get<int>("TotalCount");
                return new AlertRecord
                {
                    AlertID = r.Get<long>("AlertID"),
                    FlockID = r.Get<int>("FlockID"),
                    FlockNumber = r.Str("FlockNumber"),
                    FarmName = r.Str("FarmName"),
                    HouseName = r.Str("HouseName"),
                    FlockWeeklyID = r.Get<int?>("FlockWeeklyID"),
                    WeekNo = r.Get<int>("WeekNo"),
                    AlertDate = r.Get<DateTime>("AlertDate"),
                    AlertType = r.Str("AlertType"),
                    ParameterID = r.Get<int?>("ParameterID"),
                    ParameterName = r.Str("ParameterName"),
                    UnitCode = r.Str("UnitCode"),
                    DecimalPlaces = r.Get<int>("DecimalPlaces"),
                    StandardValue = r.Get<decimal?>("StandardValue"),
                    ActualValue = r.Get<decimal?>("ActualValue"),
                    Deviation = r.Get<decimal?>("Deviation"),
                    DeviationPct = r.Get<decimal?>("DeviationPct"),
                    Severity = r.Str("Severity"),
                    Message = r.Str("Message"),
                    Status = r.Str("Status"),
                    ActionTaken = r.Str("ActionTaken"),
                    AcknowledgedByName = r.Str("AcknowledgedByName"),
                    AcknowledgedDate = r.Get<DateTime?>("AcknowledgedDate")
                };
            },
                Sql.NVarChar("@Status", c.Status, 20), Sql.NVarChar("@Severity", c.Severity, 20), Sql.NVarChar("@AlertType", c.AlertType, 30),
                Sql.P("@FlockID", c.FlockID), Sql.P("@FarmID", c.FarmID), Sql.Date("@FromDate", c.FromDate), Sql.Date("@ToDate", c.ToDate),
                Sql.P("@RestrictFarmID", c.RestrictFarmID), Sql.P("@PageNo", page), Sql.P("@PageSize", size));
            return new PagedResult<AlertRecord>(items, total, page, size);
        }

        public void Acknowledge(long alertId, string actionTaken, bool close, IUserContext user)
        {
            _db.ExecuteProc("dbo.sp_AcknowledgeAlert", Sql.P("@AlertID", alertId), Sql.NVarChar("@ActionTaken", actionTaken, 500),
                Sql.NVarChar("@NewStatus", close ? "Closed" : "Acknowledged", 20), Sql.P("@UserID", user.UserID),
                Sql.NVarChar("@UserName", user.UserName, 50), Sql.NVarChar("@IPAddress", user.IPAddress, 50));
        }

        public int CountOpen(int? restrictFarmId)
        {
            return Sql.ToInt(_db.Scalar(
                "SELECT COUNT(*) FROM dbo.tblAlert a INNER JOIN dbo.tblFlock f ON f.FlockID = a.FlockID WHERE a.Status = N'Open' AND (@R IS NULL OR f.FarmID = @R);",
                Sql.P("@R", restrictFarmId)));
        }
    }

    public class ReportRepository : IReportRepository
    {
        private readonly IDb _db;

        public ReportRepository(IDb db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        public DataTable Run(ReportRequest q)
        {
            return _db.QueryTableProc("dbo.sp_GetFlockReport",
                Sql.NVarChar("@ReportType", q.ReportType, 30), Sql.P("@FarmID", q.FarmID), Sql.P("@HouseID", q.HouseID),
                Sql.P("@BirdTypeID", q.BirdTypeID), Sql.P("@FlockID", q.FlockID), Sql.P("@LotID", q.LotID), Sql.P("@FromWeek", q.FromWeek),
                Sql.P("@ToWeek", q.ToWeek), Sql.Date("@FromDate", q.FromDate), Sql.Date("@ToDate", q.ToDate), Sql.P("@ActiveOnly", q.ActiveOnly),
                Sql.P("@RestrictFarmID", q.RestrictFarmID));
        }
    }

    public class ConfigurationRepository : IConfigurationRepository
    {
        private readonly IDb _db;

        public ConfigurationRepository(IDb db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        public ConfigurationSet Load()
        {
            return new ConfigurationSet
            {
                Tolerances = _db.Query(
                    "SELECT t.ToleranceID, t.ParameterID, ISNULL(p.ParameterName, N'(Default for all parameters)') AS ParameterName, t.WarningPct, t.CriticalPct " +
                    "FROM dbo.tblToleranceConfig t LEFT JOIN dbo.tblParameter p ON p.ParameterID = t.ParameterID WHERE t.IsActive = 1 " +
                    "ORDER BY CASE WHEN t.ParameterID IS NULL THEN 0 ELSE 1 END, p.SortOrder;",
                    r => new ToleranceSetting
                    {
                        ToleranceID = r.Get<int>("ToleranceID"),
                        ParameterID = r.Get<int?>("ParameterID"),
                        ParameterName = r.Str("ParameterName"),
                        WarningPct = r.Get<decimal>("WarningPct"),
                        CriticalPct = r.Get<decimal>("CriticalPct")
                    }),
                Weights = _db.Query(
                    "SELECT ScoreWeightageID, ComponentCode, ComponentName, Weightage, ZeroScoreDeviationPct FROM dbo.tblScoreWeightage WHERE IsActive = 1 ORDER BY ScoreWeightageID;",
                    r => new ScoreWeight
                    {
                        ScoreWeightageID = r.Get<int>("ScoreWeightageID"),
                        ComponentCode = r.Str("ComponentCode"),
                        ComponentName = r.Str("ComponentName"),
                        Weightage = r.Get<decimal>("Weightage"),
                        ZeroScoreDeviationPct = r.Get<decimal>("ZeroScoreDeviationPct")
                    }),
                Bands = _db.Query(
                    "SELECT ScoreBandID, BandName, MinScore, MaxScore, StatusCode FROM dbo.tblScoreBand ORDER BY MinScore DESC;",
                    r => new ScoreBandSetting
                    {
                        ScoreBandID = r.Get<int>("ScoreBandID"),
                        BandName = r.Str("BandName"),
                        MinScore = r.Get<decimal>("MinScore"),
                        MaxScore = r.Get<decimal>("MaxScore"),
                        StatusCode = r.Str("StatusCode")
                    }),
                Settings = _db.Query(
                    "SELECT SettingKey, SettingValue, DataType, Description FROM dbo.tblSystemSetting ORDER BY SettingKey;",
                    r => new SystemSetting
                    {
                        SettingKey = r.Str("SettingKey"),
                        SettingValue = r.Str("SettingValue"),
                        DataType = r.Str("DataType"),
                        Description = r.Str("Description")
                    }),
                Modules = _db.Query("SELECT ModuleCode, ModuleName, IsEnabled FROM dbo.tblModule ORDER BY ModuleName;",
                    r => new ModuleFlag { ModuleCode = r.Str("ModuleCode"), ModuleName = r.Str("ModuleName"), IsEnabled = r.Get<bool>("IsEnabled") })
            };
        }

        public void SaveTolerances(IEnumerable<ToleranceSetting> rows, IUserContext user)
        {
            SaveAll(rows, "tblToleranceConfig", user, (db, t) => db.Execute(
                "UPDATE dbo.tblToleranceConfig SET WarningPct = @W, CriticalPct = @C, ModifiedBy = @U, ModifiedDate = SYSDATETIME() WHERE ToleranceID = @ID;",
                Sql.Decimal("@W", t.WarningPct, 9, 4), Sql.Decimal("@C", t.CriticalPct, 9, 4), Sql.P("@U", user.UserID), Sql.P("@ID", t.ToleranceID)));
        }

        public void SaveWeights(IEnumerable<ScoreWeight> rows, IUserContext user)
        {
            SaveAll(rows, "tblScoreWeightage", user, (db, w) => db.Execute(
                "UPDATE dbo.tblScoreWeightage SET Weightage = @W, ZeroScoreDeviationPct = @Z, ModifiedBy = @U, ModifiedDate = SYSDATETIME() WHERE ScoreWeightageID = @ID;",
                Sql.Decimal("@W", w.Weightage, 9, 4), Sql.Decimal("@Z", w.ZeroScoreDeviationPct, 9, 4), Sql.P("@U", user.UserID), Sql.P("@ID", w.ScoreWeightageID)));
        }

        public void SaveBands(IEnumerable<ScoreBandSetting> rows, IUserContext user)
        {
            SaveAll(rows, "tblScoreBand", user, (db, b) => db.Execute(
                "UPDATE dbo.tblScoreBand SET MinScore = @Min, MaxScore = @Max WHERE ScoreBandID = @ID;",
                Sql.Decimal("@Min", b.MinScore, 9, 4), Sql.Decimal("@Max", b.MaxScore, 9, 4), Sql.P("@ID", b.ScoreBandID)));
        }

        public void SaveSettings(IEnumerable<SystemSetting> rows, IUserContext user)
        {
            SaveAll(rows, "tblSystemSetting", user, (db, s) => db.Execute(
                "UPDATE dbo.tblSystemSetting SET SettingValue = @V, ModifiedBy = @U, ModifiedDate = SYSDATETIME() WHERE SettingKey = @K;",
                Sql.NVarChar("@V", s.SettingValue, 200), Sql.P("@U", user.UserID), Sql.NVarChar("@K", s.SettingKey, 60)));
        }

        public void SaveModules(IEnumerable<ModuleFlag> rows, IUserContext user)
        {
            SaveAll(rows, "tblModule", user, (db, m) => db.Execute(
                "UPDATE dbo.tblModule SET IsEnabled = @E, ModifiedBy = @U, ModifiedDate = SYSDATETIME() WHERE ModuleCode = @K;",
                Sql.P("@E", m.IsEnabled), Sql.P("@U", user.UserID), Sql.NVarChar("@K", m.ModuleCode, 30)));
        }

        private void SaveAll<T>(IEnumerable<T> rows, string table, IUserContext user, Action<IDbExecutor, T> update)
        {
            var list = (rows ?? Enumerable.Empty<T>()).ToList();
            var before = Load();
            _db.InTransaction(db =>
            {
                foreach (var row in list) update(db, row);
                AuditWriter.Write(db, user, AuditActions.Update, table, "config", AuditWriter.ToJson(Pick(before, table)), AuditWriter.ToJson(list));
            });
        }

        private static object Pick(ConfigurationSet c, string table)
        {
            switch (table)
            {
                case "tblToleranceConfig": return c.Tolerances;
                case "tblScoreWeightage": return c.Weights;
                case "tblScoreBand": return c.Bands;
                case "tblSystemSetting": return c.Settings;
                default: return c.Modules;
            }
        }
    }
}
