using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FlockManagement.Business.Interfaces;
using FlockManagement.Common.Constants;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Helpers;
using FlockManagement.Common.Interfaces;
using FlockManagement.Data.Interfaces;

namespace FlockManagement.Business.Services
{
    public class AnalysisService : IAnalysisService
    {
        private readonly IAnalysisRepository _analysis;
        private readonly IWeeklyRepository _weekly;
        private readonly ISettingsService _settings;
        private readonly IUserContext _user;

        public AnalysisService(IAnalysisRepository analysis, IWeeklyRepository weekly, ISettingsService settings, IUserContext user)
        {
            _analysis = analysis;
            _weekly = weekly;
            _settings = settings;
            _user = user;
        }

        public WeeklyAnalysis Weekly(int flockWeeklyId)
        {
            var week = _weekly.Get(flockWeeklyId);
            if (week == null || (_user.FarmID.HasValue && week.FarmID != _user.FarmID.Value)) return null;
            return new WeeklyAnalysis
            {
                Week = week,
                Parameters = _analysis.GetWeeklyAnalysis(flockWeeklyId),
                UniformityGrade = UniformityGrade(week.UniformityPercentage),
                UniformityRangePct = _settings.GetDecimal(SettingKeys.UniformityRangePct, 10m)
            };
        }

        public DashboardData Dashboard(DashboardFilter filter)
        {
            filter = filter ?? new DashboardFilter();
            filter.RestrictFarmID = _user.FarmID;
            var rows = _analysis.GetDashboard(filter);
            var withData = rows.Where(r => r.HasData).ToList();

            Func<IEnumerable<decimal?>, decimal?> avg = values =>
            {
                var list = values.Where(v => v.HasValue).Select(v => v.Value).ToList();
                return list.Count == 0 ? (decimal?)null : Math.Round(list.Average(), 2);
            };

            var kpis = new DashboardKpis
            {
                ActiveFlocks = rows.Count,
                FlocksWithData = withData.Count,
                TotalBirds = rows.Sum(r => r.TotalBirdCount),
                AvgBodyWeight = avg(withData.Select(r => r.BodyWeight)),
                AvgBodyWeightVarPct = avg(withData.Select(r => r.BodyWeightVarPct)),
                AvgWeightGain = avg(withData.Select(r => r.WeightGain)),
                AvgFeedIntake = avg(withData.Select(r => r.FeedIntake)),
                AvgMortalityPct = avg(withData.Select(r => r.Mortality)),
                AvgUniformity = avg(withData.Select(r => r.Uniformity)),
                AvgScore = avg(withData.Select(r => r.PerformanceScore)),
                FlocksBelowStandard = withData.Count(r => r.BodyWeightStatus == StatusCodes.Below || (r.BodyWeightStatus == StatusCodes.Critical && r.BodyWeightVarPct < 0)),
                FlocksAboveStandard = withData.Count(r => r.BodyWeightStatus == StatusCodes.Above || (r.BodyWeightStatus == StatusCodes.Critical && r.BodyWeightVarPct > 0)),
                CriticalFlocks = withData.Count(r => r.CriticalCount > 0 || r.OverallStatusCode == StatusCodes.Critical),
                OpenAlerts = rows.Sum(r => r.OpenAlerts)
            };
            return new DashboardData { Kpis = kpis, Flocks = rows };
        }

        public IList<TrendPoint> Trend(int flockId)
        {
            return _analysis.GetTrend(flockId, _user.FarmID);
        }

        public IList<FlockSnapshot> Comparison(IEnumerable<int> flockIds, int? ageWeek)
        {
            return _analysis.GetComparison(flockIds, ageWeek, _user.FarmID);
        }

        public IList<ExceptionRow> Exceptions(int? farmId, int? houseId, int? birdTypeId, bool criticalOnly)
        {
            return _analysis.GetExceptions(farmId, houseId, birdTypeId, criticalOnly, _user.FarmID);
        }

        /// <summary>Excellent / Good / Warning / Critical from the configurable thresholds.</summary>
        public string UniformityGrade(decimal? pct)
        {
            if (!pct.HasValue) return StatusCodes.NoData;
            if (pct >= _settings.GetDecimal("UNIFORMITY_EXCELLENT_MIN", 85m)) return StatusCodes.Excellent;
            if (pct >= _settings.GetDecimal("UNIFORMITY_GOOD_MIN", 80m)) return StatusCodes.Good;
            if (pct >= _settings.GetDecimal("UNIFORMITY_WARNING_MIN", 70m)) return StatusCodes.Warning;
            return StatusCodes.Critical;
        }
    }

    public class AlertService : IAlertService
    {
        private readonly IAlertRepository _alerts;
        private readonly IUserContext _user;

        public AlertService(IAlertRepository alerts, IUserContext user)
        {
            _alerts = alerts;
            _user = user;
        }

        public PagedResult<AlertRecord> Search(AlertSearchCriteria criteria)
        {
            criteria = criteria ?? new AlertSearchCriteria();
            criteria.RestrictFarmID = _user.FarmID;
            return _alerts.Search(criteria);
        }

        public OperationResult Acknowledge(long alertId, string actionTaken, bool close)
        {
            if (string.IsNullOrWhiteSpace(actionTaken) || actionTaken.Trim().Length < 3)
                return OperationResult.Fail("Describe the action taken.", "ActionTaken");
            try
            {
                _alerts.Acknowledge(alertId, actionTaken.Trim(), close, _user);
                return OperationResult.Ok(close ? "Alert closed." : "Alert acknowledged.");
            }
            catch (BusinessException ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }

        public int OpenCount()
        {
            return _alerts.CountOpen(_user.FarmID);
        }
    }

    public class ReportService : IReportService
    {
        private readonly IReportRepository _reports;
        private readonly IUserContext _user;

        public ReportService(IReportRepository reports, IUserContext user)
        {
            _reports = reports;
            _user = user;
        }

        public ReportResult Run(ReportRequest request, IDictionary<string, string> filterDescriptions)
        {
            if (request == null || ReportCatalog.TitleOf(request.ReportType) == null)
                throw new BusinessException("Select a report.");
            if (request.FromWeek.HasValue && request.ToWeek.HasValue && request.FromWeek > request.ToWeek)
                throw new BusinessException("From week cannot be after to week.");
            if (request.FromDate.HasValue && request.ToDate.HasValue && request.FromDate > request.ToDate)
                throw new BusinessException("From date cannot be after to date.");

            request.RestrictFarmID = _user.FarmID;
            var result = new ReportResult
            {
                ReportType = request.ReportType.ToUpperInvariant(),
                Title = ReportCatalog.TitleOf(request.ReportType),
                Table = _reports.Run(request),
                GeneratedBy = _user.UserName
            };
            if (filterDescriptions != null)
                foreach (var f in filterDescriptions.Where(f => !string.IsNullOrEmpty(f.Value)))
                    result.Filters.Add(f.Key + ": " + f.Value);
            return result;
        }

        public ExportFile Export(ReportResult result, string format)
        {
            var doc = new ExportDocument
            {
                Title = result.Title,
                Table = result.Table,
                Footer = "Flock Weight Management - generated " + result.GeneratedAt.ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture) +
                         " by " + (result.GeneratedBy ?? "-")
            };
            doc.SubTitles.Add("Generated " + result.GeneratedAt.ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture) + " by " + (result.GeneratedBy ?? "-") +
                              "  |  " + result.Table.Rows.Count.ToString("N0", CultureInfo.InvariantCulture) + " rows");
            foreach (var f in result.Filters) doc.SubTitles.Add(f);

            var baseName = result.ReportType.ToLowerInvariant() + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);
            switch ((format ?? string.Empty).ToLowerInvariant())
            {
                case "xlsx":
                case "excel":
                    return new ExportFile
                    {
                        Content = XlsxWriter.Write(doc),
                        ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        FileName = baseName + ".xlsx"
                    };
                case "pdf":
                    return new ExportFile { Content = PdfTableWriter.Write(doc), ContentType = "application/pdf", FileName = baseName + ".pdf" };
                default:
                    return new ExportFile { Content = CsvWriter.Write(doc), ContentType = "text/csv", FileName = baseName + ".csv" };
            }
        }
    }

    public class ConfigurationService : IConfigurationService
    {
        private readonly IConfigurationRepository _config;
        private readonly ISettingsService _settings;
        private readonly IUserContext _user;

        public ConfigurationService(IConfigurationRepository config, ISettingsService settings, IUserContext user)
        {
            _config = config;
            _settings = settings;
            _user = user;
        }

        public ConfigurationSet Load()
        {
            return _config.Load();
        }

        public OperationResult SaveTolerances(IList<ToleranceSetting> rows)
        {
            if (rows == null || rows.Count == 0) return OperationResult.Fail("Nothing to save.");
            var bad = rows.FirstOrDefault(r => r.WarningPct < 0 || r.CriticalPct < r.WarningPct || r.CriticalPct > 1000);
            if (bad != null) return OperationResult.Fail("Tolerances: critical % must be at least the warning %, and both 0 or more (" + bad.ParameterName + ").");
            return Run(() => _config.SaveTolerances(rows, _user), "Tolerances saved. They apply to analysis immediately and to scores when a week is next saved.");
        }

        public OperationResult SaveWeights(IList<ScoreWeight> rows)
        {
            if (rows == null || rows.Count == 0) return OperationResult.Fail("Nothing to save.");
            if (rows.Any(r => r.Weightage < 0 || r.ZeroScoreDeviationPct <= 0)) return OperationResult.Fail("Weights cannot be negative and the zero-score deviation must be above 0.");
            if (rows.Sum(r => r.Weightage) <= 0) return OperationResult.Fail("At least one component needs a weight above 0.");
            return Run(() => _config.SaveWeights(rows, _user), "Score weightage saved. Scores update when each week is next saved or recalculated.");
        }

        public OperationResult SaveBands(IList<ScoreBandSetting> rows)
        {
            if (rows == null || rows.Count == 0) return OperationResult.Fail("Nothing to save.");
            if (rows.Any(r => r.MinScore < 0 || r.MaxScore > 100 || r.MinScore > r.MaxScore))
                return OperationResult.Fail("Each band needs 0 <= minimum <= maximum <= 100.");
            var ordered = rows.OrderBy(r => r.MinScore).ToList();
            for (var i = 1; i < ordered.Count; i++)
                if (ordered[i].MinScore <= ordered[i - 1].MaxScore)
                    return OperationResult.Fail("Score bands overlap: " + ordered[i - 1].BandName + " and " + ordered[i].BandName + ".");
            return Run(() => _config.SaveBands(rows, _user), "Score bands saved.");
        }

        public OperationResult SaveSettings(IList<SystemSetting> rows)
        {
            if (rows == null || rows.Count == 0) return OperationResult.Fail("Nothing to save.");
            var current = _config.Load().Settings.ToDictionary(s => s.SettingKey, StringComparer.OrdinalIgnoreCase);
            foreach (var r in rows)
            {
                SystemSetting def;
                if (!current.TryGetValue(r.SettingKey ?? string.Empty, out def)) return OperationResult.Fail("Unknown setting " + r.SettingKey + ".");
                r.SettingValue = (r.SettingValue ?? string.Empty).Trim();
                decimal d;
                int n;
                if (def.DataType == "INT" && !int.TryParse(r.SettingValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                    return OperationResult.Fail(def.SettingKey + " must be a whole number.");
                if (def.DataType == "DECIMAL" && !decimal.TryParse(r.SettingValue, NumberStyles.Number, CultureInfo.InvariantCulture, out d))
                    return OperationResult.Fail(def.SettingKey + " must be a number.");
                if (r.SettingValue.Length == 0) return OperationResult.Fail(def.SettingKey + " cannot be empty.");
            }
            var result = Run(() => _config.SaveSettings(rows, _user), "Settings saved.");
            _settings.Refresh();
            return result;
        }

        public OperationResult SaveModules(IList<ModuleFlag> rows)
        {
            if (rows == null || rows.Count == 0) return OperationResult.Fail("Nothing to save.");
            if (rows.Any(r => r.ModuleCode == "CORE" && !r.IsEnabled)) return OperationResult.Fail("The core module cannot be disabled.");
            return Run(() => _config.SaveModules(rows, _user), "Modules saved.");
        }

        private static OperationResult Run(Action action, string message)
        {
            try
            {
                action();
                return OperationResult.Ok(message);
            }
            catch (BusinessException ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }
    }
}
