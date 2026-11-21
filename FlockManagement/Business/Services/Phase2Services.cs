using System;
using System.Collections.Generic;
using System.Linq;
using FlockManagement.Business.Interfaces;
using FlockManagement.Common.Constants;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Helpers;
using FlockManagement.Common.Interfaces;
using FlockManagement.Data.Interfaces;

namespace FlockManagement.Business.Services
{
    public class StandardService : IStandardService
    {
        private readonly IStandardRepository _standards;
        private readonly IParameterRepository _parameters;
        private readonly IUserContext _user;

        public StandardService(IStandardRepository standards, IParameterRepository parameters, IUserContext user)
        {
            _standards = standards;
            _parameters = parameters;
            _user = user;
        }

        public PagedResult<Standard> Search(StandardSearchCriteria criteria)
        {
            return _standards.Search(criteria);
        }

        public IList<Parameter> Parameters()
        {
            return _parameters.GetAll();
        }

        public IList<StandardGridRow> GetGrid(StandardGridKey key, int lastWeek)
        {
            var existing = _standards.GetGrid(key).ToDictionary(r => r.AgeWeek);
            lastWeek = Math.Max(lastWeek, existing.Keys.DefaultIfEmpty(0).Max());
            var rows = new List<StandardGridRow>();
            for (var w = 0; w <= Math.Min(120, lastWeek); w++)
            {
                StandardGridRow row;
                rows.Add(existing.TryGetValue(w, out row) ? row : new StandardGridRow { AgeWeek = w });
            }
            return rows;
        }

        public OperationResult SaveGrid(StandardGridKey key, IList<StandardGridRow> rows)
        {
            if (key == null || key.BirdTypeID <= 0) return OperationResult.Fail("Select a bird type.", "BirdTypeID");
            if (key.ParameterID <= 0) return OperationResult.Fail("Select a parameter.", "ParameterID");
            if (key.EffectiveFrom == default(DateTime)) return OperationResult.Fail("Effective From is required.", "EffectiveFrom");
            if (rows == null || rows.Count == 0) return OperationResult.Fail("No weeks to save.");

            foreach (var r in rows)
            {
                if (!r.StandardValue.HasValue && (r.MinValue.HasValue || r.MaxValue.HasValue || r.CriticalMinValue.HasValue || r.CriticalMaxValue.HasValue))
                    return OperationResult.Fail(string.Format("Week {0}: enter the standard value or clear the limits.", r.AgeWeek));
                if (r.CriticalMinValue.HasValue && r.MinValue.HasValue && r.CriticalMinValue > r.MinValue)
                    return OperationResult.Fail(string.Format("Week {0}: critical minimum must be below the minimum.", r.AgeWeek));
                if (r.CriticalMaxValue.HasValue && r.MaxValue.HasValue && r.CriticalMaxValue < r.MaxValue)
                    return OperationResult.Fail(string.Format("Week {0}: critical maximum must be above the maximum.", r.AgeWeek));
            }
            try
            {
                _standards.SaveGrid(key, rows, _user);
                return OperationResult.Ok(string.Format("Saved {0} week(s) of standards.", rows.Count(r => r.StandardValue.HasValue)));
            }
            catch (BusinessException ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }

        public OperationResult SetActive(int standardId, bool isActive)
        {
            try
            {
                _standards.SetActive(standardId, isActive, _user);
                return OperationResult.Ok(isActive ? "Standard activated." : "Standard deactivated.");
            }
            catch (BusinessException ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }

        public IList<DateTime> EffectiveDates(int birdTypeId, int parameterId)
        {
            return _standards.GetEffectiveDates(birdTypeId, parameterId);
        }

        public IList<ParameterStandard> ForFlock(int flockId, int ageWeek, int? feedTypeId, DateTime? asOf)
        {
            return _standards.GetFlockStandards(flockId, ageWeek, feedTypeId, asOf);
        }
    }

    public class MortalityReasonService : MasterServiceBase<MortalityReason>, IMortalityReasonService
    {
        public MortalityReasonService(IMortalityReasonRepository repository, IUserContext user) : base(repository, user) { }

        protected override void Validate(MortalityReason e)
        {
            e.ReasonCode = Code(e.ReasonCode);
            e.ReasonName = Clean(e.ReasonName);
            e.Category = Clean(e.Category);
            Require(e.ReasonCode, "ReasonCode", "Reason code");
            Require(e.ReasonName, "ReasonName", "Reason");
        }
    }

    public class FeedFormulaService : IFeedFormulaService
    {
        private readonly IFeedFormulaRepository _formulas;
        private readonly IUserContext _user;

        public FeedFormulaService(IFeedFormulaRepository formulas, IUserContext user)
        {
            _formulas = formulas;
            _user = user;
        }

        public PagedResult<FeedFormula> Search(string search, int? feedTypeId, bool? isActive, int pageNo)
        {
            return _formulas.Search(search, feedTypeId, isActive, pageNo, 25);
        }

        public FeedFormula Get(int id)
        {
            return _formulas.GetById(id);
        }

        public FeedFormula New()
        {
            return new FeedFormula { EffectiveFrom = DateTime.Today, Details = _formulas.GetBlankDetails() };
        }

        public OperationResult<int> Save(FeedFormula f)
        {
            if (f == null) return OperationResult<int>.Fail("No data received.");
            f.FormulaCode = string.IsNullOrWhiteSpace(f.FormulaCode) ? null : f.FormulaCode.Trim().ToUpperInvariant();
            f.FormulaName = string.IsNullOrWhiteSpace(f.FormulaName) ? null : f.FormulaName.Trim();
            if (f.FeedTypeID <= 0) return OperationResult<int>.Fail("Feed type is required.", "FeedTypeID");
            if (f.FormulaCode == null) return OperationResult<int>.Fail("Formula code is required.", "FormulaCode");
            if (f.FormulaName == null) return OperationResult<int>.Fail("Formula name is required.", "FormulaName");
            if (f.EffectiveTo.HasValue && f.EffectiveTo < f.EffectiveFrom)
                return OperationResult<int>.Fail("Effective To cannot be before Effective From.", "EffectiveTo");
            foreach (var d in f.Details.Where(d => d.Value.HasValue))
            {
                if (d.Value < 0) return OperationResult<int>.Fail(d.ParameterName + " cannot be negative.");
            }
            try
            {
                var id = _formulas.Save(f, _user);
                return OperationResult<int>.Ok(id, "Feed formula " + f.FormulaCode + " saved.");
            }
            catch (BusinessException ex)
            {
                return OperationResult<int>.Fail(ex.Message, ex.FieldName);
            }
        }

        public IList<LookupItem> Lookup(int? feedTypeId, DateTime? onDate)
        {
            return _formulas.GetLookup(feedTypeId, onDate);
        }
    }

    public class MovementService : IMovementService
    {
        private readonly IMovementRepository _movements;
        private readonly IFlockRepository _flocks;
        private readonly IUserContext _user;

        public MovementService(IMovementRepository movements, IFlockRepository flocks, IUserContext user)
        {
            _movements = movements;
            _flocks = flocks;
            _user = user;
        }

        private MovementSearchCriteria Restrict(MovementSearchCriteria c)
        {
            c = c ?? new MovementSearchCriteria();
            c.RestrictFarmID = _user.FarmID;
            return c;
        }

        private string CheckFlock(int flockId)
        {
            var flock = _flocks.GetById(flockId);
            if (flock == null) return "Flock not found.";
            if (_user.FarmID.HasValue && flock.FarmID != _user.FarmID.Value) return "You can only manage flocks of your assigned farm.";
            return null;
        }

        public PagedResult<PlacementRecord> Placements(MovementSearchCriteria criteria)
        {
            return _movements.SearchPlacements(Restrict(criteria));
        }

        public OperationResult<int> AddPlacement(PlacementRecord p)
        {
            if (p == null) return OperationResult<int>.Fail("No data received.");
            var err = CheckFlock(p.FlockID);
            if (err != null) return OperationResult<int>.Fail(err, "FlockID");
            if (p.MaleCount < 0 || p.FemaleCount < 0) return OperationResult<int>.Fail("Bird counts cannot be negative.", "FemaleCount");
            if (p.MaleCount + p.FemaleCount == 0) return OperationResult<int>.Fail("Enter the number of birds placed.", "FemaleCount");
            if (p.PlacementDate.Date > DateTime.Today) return OperationResult<int>.Fail("Placement date cannot be in the future.", "PlacementDate");
            try
            {
                var id = _movements.AddPlacement(p, _user);
                return OperationResult<int>.Ok(id, string.Format("Placement of {0:N0} birds recorded.", p.MaleCount + p.FemaleCount));
            }
            catch (BusinessException ex)
            {
                return OperationResult<int>.Fail(ex.Message);
            }
        }

        public PagedResult<TransferRecord> Transfers(MovementSearchCriteria criteria)
        {
            return _movements.SearchTransfers(Restrict(criteria));
        }

        public OperationResult<int> SaveTransfer(TransferRecord t)
        {
            if (t == null) return OperationResult<int>.Fail("No data received.");
            var err = CheckFlock(t.FlockID);
            if (err != null) return OperationResult<int>.Fail(err, "FlockID");
            if (t.TransferType != "IN" && t.TransferType != "OUT" && t.TransferType != "HOUSE")
                return OperationResult<int>.Fail("Select the transfer type.", "TransferType");
            if (t.TransferType == "HOUSE" && !t.ToHouseID.HasValue) return OperationResult<int>.Fail("Select the new house.", "ToHouseID");
            if (t.TransferType != "HOUSE" && t.MaleCount + t.FemaleCount <= 0)
                return OperationResult<int>.Fail("Enter the number of birds transferred.", "FemaleCount");
            if (t.CounterFlockID.HasValue)
            {
                var other = CheckFlock(t.CounterFlockID.Value);
                if (other != null) return OperationResult<int>.Fail("Other flock: " + other, "CounterFlockID");
            }
            try
            {
                var id = _movements.SaveTransfer(t, _user);
                return OperationResult<int>.Ok(id, "Transfer recorded and population recalculated.");
            }
            catch (BusinessException ex)
            {
                return OperationResult<int>.Fail(ex.Message);
            }
        }

        public PagedResult<AdjustmentRecord> Adjustments(MovementSearchCriteria criteria)
        {
            return _movements.SearchAdjustments(Restrict(criteria));
        }

        public OperationResult<int> SaveAdjustment(AdjustmentRecord a)
        {
            if (a == null) return OperationResult<int>.Fail("No data received.");
            var err = CheckFlock(a.FlockID);
            if (err != null) return OperationResult<int>.Fail(err, "FlockID");
            if (a.MaleAdjustment == 0 && a.FemaleAdjustment == 0) return OperationResult<int>.Fail("Enter a male or female adjustment.", "FemaleAdjustment");
            if (string.IsNullOrWhiteSpace(a.Reason) || a.Reason.Trim().Length < 5)
                return OperationResult<int>.Fail("Give a reason for the adjustment (at least 5 characters).", "Reason");
            try
            {
                var id = _movements.SaveAdjustment(a, _user);
                return OperationResult<int>.Ok(id, "Adjustment saved and population recalculated. The change is recorded in the audit trail.");
            }
            catch (BusinessException ex)
            {
                return OperationResult<int>.Fail(ex.Message);
            }
        }
    }

    public class WeeklyService : IWeeklyService
    {
        public const int MaxSamples = 2000;

        private readonly IWeeklyRepository _weekly;
        private readonly IStandardRepository _standards;
        private readonly IFlockRepository _flocks;
        private readonly ISettingsService _settings;
        private readonly IUserContext _user;

        public WeeklyService(IWeeklyRepository weekly, IStandardRepository standards, IFlockRepository flocks,
            ISettingsService settings, IUserContext user)
        {
            _weekly = weekly;
            _standards = standards;
            _flocks = flocks;
            _settings = settings;
            _user = user;
        }

        private bool Allowed(int farmId)
        {
            return !_user.FarmID.HasValue || _user.FarmID.Value == farmId;
        }

        public WeeklyEntryData GetEntry(int flockId, int? weekNo, int? feedTypeId)
        {
            var ctx = _weekly.GetContext(flockId, weekNo);
            if (ctx == null || !Allowed(ctx.FarmID)) return null;

            WeeklyRecord existing = null;
            if (ctx.FlockWeeklyID.HasValue) existing = _weekly.Get(ctx.FlockWeeklyID.Value);

            var feed = feedTypeId ?? (existing != null ? existing.FeedTypeID : null) ?? ctx.SuggestedFeedTypeID;
            var asOf = existing != null ? existing.EntryDate : (ctx.WeekEnd > DateTime.Today ? DateTime.Today : ctx.WeekEnd);
            return new WeeklyEntryData
            {
                Context = ctx,
                Existing = existing,
                Standards = _standards.GetFlockStandards(flockId, ctx.WeekNo, feed, asOf),
                UniformityRangePct = _settings.GetDecimal(SettingKeys.UniformityRangePct, 10m)
            };
        }

        public OperationResult<int> Save(WeeklyEntryInput i)
        {
            if (i == null) return OperationResult<int>.Fail("No data received.");
            var flock = _flocks.GetById(i.FlockID);
            if (flock == null) return OperationResult<int>.Fail("Flock not found.");
            if (!Allowed(flock.FarmID)) return OperationResult<int>.Fail("You can only enter data for flocks of your assigned farm.");

            if (i.MaleMortality < 0 || i.FemaleMortality < 0 || i.MaleCulling < 0 || i.FemaleCulling < 0)
                return OperationResult<int>.Fail("Mortality and culling cannot be negative.", "FemaleMortality");
            if (i.ManualBodyWeight.HasValue && (i.ManualBodyWeight <= 0 || i.ManualBodyWeight >= 10000))
                return OperationResult<int>.Fail("Body weight must be between 1 and 9,999 g.", "ManualBodyWeight");
            if (i.Samples.Count > MaxSamples)
                return OperationResult<int>.Fail(string.Format("A maximum of {0:N0} weight samples can be entered per week.", MaxSamples));
            if (i.Samples.Any(s => s.Weight <= 0 || s.Weight >= 10000))
                return OperationResult<int>.Fail("Each sample weight must be between 1 and 9,999 g.");
            if (i.FeedLines.Any(f => f.QuantityKg < 0 || f.WastageKg < 0 || f.WastageKg > f.QuantityKg))
                return OperationResult<int>.Fail("Feed quantity and wastage cannot be negative, and wastage cannot exceed quantity.");
            if (i.EntryDate.Date > DateTime.Today) return OperationResult<int>.Fail("Entry date cannot be in the future.", "EntryDate");
            if (!string.IsNullOrEmpty(i.NutritionSource) && i.NutritionSource != "L" && i.NutritionSource != "F")
                i.NutritionSource = null;
            if (i.NutritionSource == "L" && i.Nutrients.All(n => !n.Value.HasValue))
                return OperationResult<int>.Fail("Lab analysis selected: enter at least one nutrient value, or switch to formula values.");
            var err = ValidateProduction(i.Production);
            if (err != null) return OperationResult<int>.Fail(err.Item2, err.Item1);

            try
            {
                var id = _weekly.Save(i, _user);
                return OperationResult<int>.Ok(id, string.Format("Week {0} of flock {1} saved. Population, score and alerts recalculated.", i.WeekNo, flock.FlockNumber));
            }
            catch (BusinessException ex)
            {
                return OperationResult<int>.Fail(ex.Message, ex.FieldName);
            }
        }

        /// <summary>Checks the Production & Environment tab. Returns (field, message) or null.</summary>
        internal static Tuple<string, string> ValidateProduction(ProductionInput p)
        {
            if (p == null) return null;
            var counts = new[] { p.TotalEggs, p.HatchingEggs, p.FloorEggs, p.DirtyEggs, p.BrokenEggs, p.CrackedEggs, p.DoubleYolkEggs };
            if (counts.Any(c => c.HasValue && c.Value < 0)) return Tuple.Create("Production.TotalEggs", "Egg counts cannot be negative.");
            if (p.HatchingEggs.HasValue && p.HatchingEggs.Value > (p.TotalEggs ?? 0))
                return Tuple.Create("Production.HatchingEggs", "Hatching eggs cannot exceed the total eggs collected.");
            var graded = (p.HatchingEggs ?? 0) + (p.FloorEggs ?? 0) + (p.DirtyEggs ?? 0) + (p.BrokenEggs ?? 0) + (p.CrackedEggs ?? 0) + (p.DoubleYolkEggs ?? 0);
            if (p.TotalEggs.HasValue && graded > p.TotalEggs.Value)
                return Tuple.Create("Production.TotalEggs", "Hatching, floor, dirty, broken, cracked and double-yolk eggs add up to more than the total eggs.");
            if (p.AvgEggWeight.HasValue && (p.AvgEggWeight < 20 || p.AvgEggWeight > 120))
                return Tuple.Create("Production.AvgEggWeight", "Average egg weight must be between 20 and 120 g.");
            if (p.LightHours.HasValue && (p.LightHours < 0 || p.LightHours > 24))
                return Tuple.Create("Production.LightHours", "Light hours must be between 0 and 24.");
            if (p.LightIntensity.HasValue && p.LightIntensity < 0)
                return Tuple.Create("Production.LightIntensity", "Light intensity cannot be negative.");
            if (p.TempDay.HasValue && (p.TempDay < -20 || p.TempDay > 60))
                return Tuple.Create("Production.TempDay", "Day temperature must be between -20 and 60 \u00b0C.");
            if (p.TempNight.HasValue && (p.TempNight < -20 || p.TempNight > 60))
                return Tuple.Create("Production.TempNight", "Night temperature must be between -20 and 60 \u00b0C.");
            return null;
        }

        public OperationResult SaveMortality(WeeklyEntryInput i)
        {
            var existing = _weekly.Get(i.FlockWeeklyID);
            if (existing == null || !Allowed(existing.FarmID)) return OperationResult.Fail("Weekly entry not found.");
            try
            {
                _weekly.SaveMortality(i, _user);
                return OperationResult.Ok("Mortality updated.");
            }
            catch (BusinessException ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }

        public OperationResult DeleteLast(int flockId)
        {
            var flock = _flocks.GetById(flockId);
            if (flock == null || !Allowed(flock.FarmID)) return OperationResult.Fail("Flock not found.");
            try
            {
                _weekly.DeleteLast(flockId, _user);
                return OperationResult.Ok("The latest week was deleted and the population recalculated.");
            }
            catch (BusinessException ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }

        public WeeklyRecord Get(int flockWeeklyId)
        {
            var w = _weekly.Get(flockWeeklyId);
            return w != null && Allowed(w.FarmID) ? w : null;
        }

        public PagedResult<WeeklyListItem> Search(WeeklySearchCriteria criteria)
        {
            criteria = criteria ?? new WeeklySearchCriteria();
            criteria.RestrictFarmID = _user.FarmID;
            return _weekly.Search(criteria);
        }
    }

    public class HatchResultService : IHatchResultService
    {
        private readonly IHatchResultRepository _hatch;
        private readonly IFlockRepository _flocks;
        private readonly IUserContext _user;

        public HatchResultService(IHatchResultRepository hatch, IFlockRepository flocks, IUserContext user)
        {
            _hatch = hatch;
            _flocks = flocks;
            _user = user;
        }

        public PagedResult<HatchResult> Search(HatchResultSearchCriteria c)
        {
            c = c ?? new HatchResultSearchCriteria();
            c.RestrictFarmID = _user.FarmID;
            return _hatch.Search(c);
        }

        public HatchResult Get(int hatchResultId)
        {
            var h = _hatch.Get(hatchResultId);
            if (h == null) return null;
            var flock = _flocks.GetById(h.FlockID);
            if (flock == null || (_user.FarmID.HasValue && flock.FarmID != _user.FarmID.Value)) return null;
            return h;
        }

        public OperationResult<int> Save(HatchResult h)
        {
            if (h == null) return OperationResult<int>.Fail("No data received.");
            var flock = _flocks.GetById(h.FlockID);
            if (flock == null) return OperationResult<int>.Fail("Flock not found.", "FlockID");
            if (_user.FarmID.HasValue && flock.FarmID != _user.FarmID.Value)
                return OperationResult<int>.Fail("You can only record results for flocks of your assigned farm.", "FlockID");
            if (h.HatchResultID > 0 && Get(h.HatchResultID) == null) return OperationResult<int>.Fail("Hatch result not found.");
            if (h.SettableEggs <= 0) return OperationResult<int>.Fail("Enter the number of eggs set.", "SettableEggs");
            if (h.InfertileEggs < 0 || h.DeadInShell < 0 || h.HatchedChicks < 0 || h.SaleableChicks < 0)
                return OperationResult<int>.Fail("Counts cannot be negative.");
            if (h.InfertileEggs > h.SettableEggs) return OperationResult<int>.Fail("Infertile eggs cannot exceed the eggs set.", "InfertileEggs");
            if (h.HatchedChicks + h.DeadInShell > h.SettableEggs - h.InfertileEggs)
                return OperationResult<int>.Fail("Chicks hatched plus dead-in-shell cannot exceed the fertile eggs.", "HatchedChicks");
            if (!h.HatchDate.HasValue && h.HatchedChicks > 0)
                return OperationResult<int>.Fail("Enter the hatch date together with the chicks hatched.", "HatchDate");
            if (h.SaleableChicks > h.HatchedChicks) return OperationResult<int>.Fail("Saleable chicks cannot exceed the chicks hatched.", "SaleableChicks");
            if (h.SetDate.Date > DateTime.Today) return OperationResult<int>.Fail("Set date cannot be in the future.", "SetDate");
            if (h.HatchDate.HasValue && (h.HatchDate.Value.Date > DateTime.Today || h.HatchDate.Value.Date < h.SetDate.Date.AddDays(18)))
                return OperationResult<int>.Fail("Hatch date must be at least 18 days after the set date and not in the future.", "HatchDate");
            try
            {
                var id = _hatch.Save(h, _user);
                return OperationResult<int>.Ok(id, string.Format("Hatch result for flock {0}, week {1} eggs saved. Fertility {2:0.0}%.",
                    flock.FlockNumber, h.WeekNo, (h.SettableEggs - h.InfertileEggs) * 100m / h.SettableEggs));
            }
            catch (BusinessException ex)
            {
                return OperationResult<int>.Fail(ex.Message, ex.FieldName);
            }
        }

        public OperationResult Delete(int hatchResultId)
        {
            if (Get(hatchResultId) == null) return OperationResult.Fail("Hatch result not found.");
            try
            {
                _hatch.Delete(hatchResultId, _user);
                return OperationResult.Ok("Hatch result deleted.");
            }
            catch (BusinessException ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }
    }
}
