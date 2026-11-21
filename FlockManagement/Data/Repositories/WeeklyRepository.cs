using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Helpers;
using FlockManagement.Common.Interfaces;
using FlockManagement.Data.Infrastructure;
using FlockManagement.Data.Interfaces;
using Newtonsoft.Json;

namespace FlockManagement.Data.Repositories
{
    public class WeeklyRepository : IWeeklyRepository
    {
        private readonly IDb _db;

        public WeeklyRepository(IDb db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        public WeeklyContext GetContext(int flockId, int? weekNo)
        {
            return _db.QueryProcSingle("dbo.sp_FlockWeekly_GetContext", r => new WeeklyContext
            {
                FlockID = r.Get<int>("FlockID"),
                FlockNumber = r.Str("FlockNumber"),
                LotNumber = r.Str("LotNumber"),
                BirdTypeID = r.Get<int>("BirdTypeID"),
                BirdTypeName = r.Str("BirdTypeName"),
                BreedID = r.Get<int>("BreedID"),
                BreedName = r.Str("BreedName"),
                FarmID = r.Get<int>("FarmID"),
                FarmName = r.Str("FarmName"),
                HouseID = r.Get<int>("HouseID"),
                HouseName = r.Str("HouseName"),
                HatchDate = r.Get<DateTime>("HatchDate"),
                PlacementDate = r.Get<DateTime>("PlacementDate"),
                Status = r.Str("Status"),
                PlacedMales = r.Get<int>("MaleCount"),
                PlacedFemales = r.Get<int>("FemaleCount"),
                FirstWeek = r.Get<int>("FirstWeek"),
                LastWeek = r.Get<int?>("LastWeek"),
                NextWeek = r.Get<int>("NextWeek"),
                WeekNo = r.Get<int>("WeekNo"),
                WeekStart = r.Get<DateTime>("WeekStart"),
                WeekEnd = r.Get<DateTime>("WeekEnd"),
                FlockWeeklyID = r.Get<int?>("FlockWeeklyID"),
                OpeningMale = r.Get<int>("OpeningMale"),
                OpeningFemale = r.Get<int>("OpeningFemale"),
                ReceivedMale = r.Get<int>("ReceivedMale"),
                ReceivedFemale = r.Get<int>("ReceivedFemale"),
                TransferOutMale = r.Get<int>("TransferOutMale"),
                TransferOutFemale = r.Get<int>("TransferOutFemale"),
                PrevBodyWeight = r.Get<decimal?>("PrevBodyWeight"),
                PrevWeightGain = r.Get<decimal?>("PrevWeightGain"),
                PrevFeedIntake = r.Get<decimal?>("PrevFeedIntake"),
                PrevUniformity = r.Get<decimal?>("PrevUniformity"),
                PrevMortalityPct = r.Get<decimal?>("PrevMortalityPct"),
                PrevScore = r.Get<decimal?>("PrevScore"),
                ChickAvgWeight = r.Get<decimal?>("ChickAvgWeight"),
                SuggestedFeedTypeID = r.Get<int?>("SuggestedFeedTypeID"),
                CanEnter = r.Get<bool>("CanEnter")
            }, Sql.P("@FlockID", flockId), Sql.P("@WeekNo", weekNo));
        }

        public int? FindId(int flockId, int weekNo)
        {
            var v = _db.Scalar("SELECT FlockWeeklyID FROM dbo.tblFlockWeekly WHERE FlockID = @F AND WeekNo = @W;", Sql.P("@F", flockId), Sql.P("@W", weekNo));
            return v == null ? (int?)null : Convert.ToInt32(v);
        }

        public WeeklyRecord Get(int flockWeeklyId)
        {
            var w = _db.QueryProcSingle("dbo.sp_FlockWeekly_Get", Map, Sql.P("@FlockWeeklyID", flockWeeklyId));
            if (w == null) return null;

            var id = Sql.P("@ID", flockWeeklyId);
            w.Samples = _db.Query("SELECT Sex, SampleNo, WeightGrams FROM dbo.tblWeightSample WHERE FlockWeeklyID = @ID ORDER BY Sex, SampleNo;",
                r => new WeightSample { Sex = r.Str("Sex"), SampleNo = r.Get<int>("SampleNo"), Weight = r.Get<decimal>("WeightGrams") }, id);

            w.FeedLines = _db.Query(
                "SELECT fc.FeedTypeID, ft.FeedTypeName, fc.FeedFormulaID, ff.FormulaCode, fc.Sex, fc.QuantityKg, fc.WastageKg, fc.BirdDays, fc.IntakeGPerBirdDay " +
                "FROM dbo.tblFeedConsumption fc INNER JOIN dbo.tblFeedType ft ON ft.FeedTypeID = fc.FeedTypeID " +
                "LEFT JOIN dbo.tblFeedFormula ff ON ff.FeedFormulaID = fc.FeedFormulaID WHERE fc.FlockWeeklyID = @ID ORDER BY fc.FeedConsumptionID;",
                r => new FeedLine
                {
                    FeedTypeID = r.Get<int>("FeedTypeID"),
                    FeedTypeName = r.Str("FeedTypeName"),
                    FeedFormulaID = r.Get<int?>("FeedFormulaID"),
                    FormulaCode = r.Str("FormulaCode"),
                    Sex = r.Str("Sex"),
                    QuantityKg = r.Get<decimal>("QuantityKg"),
                    WastageKg = r.Get<decimal>("WastageKg"),
                    BirdDays = r.Get<int>("BirdDays"),
                    IntakeGPerBirdDay = r.Get<decimal?>("IntakeGPerBirdDay")
                }, Sql.P("@ID", flockWeeklyId));

            w.Nutrients = _db.Query(
                "SELECT na.ParameterID, p.ParameterCode, na.ActualValue FROM dbo.tblNutritionAnalysis na INNER JOIN dbo.tblParameter p ON p.ParameterID = na.ParameterID " +
                "WHERE na.FlockWeeklyID = @ID;",
                r => new NutrientValue { ParameterID = r.Get<int>("ParameterID"), ParameterCode = r.Str("ParameterCode"), Value = r.Get<decimal?>("ActualValue") },
                Sql.P("@ID", flockWeeklyId));

            w.MortalityReasons = _db.Query(
                "SELECT d.MortalityReasonID, r.ReasonName, d.Sex, d.BirdCount FROM dbo.tblMortality m " +
                "INNER JOIN dbo.tblMortalityDetail d ON d.MortalityID = m.MortalityID INNER JOIN dbo.tblMortalityReason r ON r.MortalityReasonID = d.MortalityReasonID " +
                "WHERE m.FlockWeeklyID = @ID ORDER BY d.MortalityDetailID;",
                r => new MortalityReasonLine
                {
                    MortalityReasonID = r.Get<int>("MortalityReasonID"),
                    ReasonName = r.Str("ReasonName"),
                    Sex = r.Str("Sex"),
                    BirdCount = r.Get<int>("BirdCount")
                }, Sql.P("@ID", flockWeeklyId));

            var main = _db.Scalar("SELECT MortalityReasonID FROM dbo.tblMortality WHERE FlockWeeklyID = @ID;", Sql.P("@ID", flockWeeklyId));
            w.MortalityReasonID = main == null ? (int?)null : Convert.ToInt32(main);
            w.Production = GetProduction(flockWeeklyId);
            return w;
        }

        public ProductionData GetProduction(int flockWeeklyId)
        {
            var p = _db.QuerySingle(
                "SELECT w.FlockWeeklyID, w.LightHours, w.LightIntensity, w.TempDay, w.TempNight, e.TotalEggs, e.HatchingEggs, e.FloorEggs, " +
                "e.DirtyEggs, e.BrokenEggs, e.CrackedEggs, e.DoubleYolkEggs, e.AvgEggWeight, e.HenDayProductionPct, e.HenHousedEggsCum, e.HenHousedCount, " +
                "CAST((w.ClosingFemale + w.FemaleMortality + w.FemaleCulling + w.TransferOutFemale - w.ReceivedFemale + w.ClosingFemale) / 2.0 * 7 AS DECIMAL(18,1)) AS HenDays " +
                "FROM dbo.tblFlockWeekly w LEFT JOIN dbo.tblEggProductionWeekly e ON e.FlockWeeklyID = w.FlockWeeklyID WHERE w.FlockWeeklyID = @ID;",
                r => new ProductionData
                {
                    FlockWeeklyID = r.Get<int>("FlockWeeklyID"),
                    LightHours = r.Get<decimal?>("LightHours"),
                    LightIntensity = r.Get<decimal?>("LightIntensity"),
                    TempDay = r.Get<decimal?>("TempDay"),
                    TempNight = r.Get<decimal?>("TempNight"),
                    TotalEggs = r.Get<int?>("TotalEggs"),
                    HatchingEggs = r.Get<int?>("HatchingEggs"),
                    FloorEggs = r.Get<int?>("FloorEggs"),
                    DirtyEggs = r.Get<int?>("DirtyEggs"),
                    BrokenEggs = r.Get<int?>("BrokenEggs"),
                    CrackedEggs = r.Get<int?>("CrackedEggs"),
                    DoubleYolkEggs = r.Get<int?>("DoubleYolkEggs"),
                    AvgEggWeight = r.Get<decimal?>("AvgEggWeight"),
                    HenDayProductionPct = r.Get<decimal?>("HenDayProductionPct"),
                    HenHousedEggsCum = r.Get<decimal?>("HenHousedEggsCum"),
                    HenHousedCount = r.Get<int?>("HenHousedCount"),
                    HenDays = r.Get<decimal?>("HenDays")
                }, Sql.P("@ID", flockWeeklyId));
            if (p == null) return null;
            p.HatchResults = _db.Query(
                "SELECT hr.HatchResultID, hr.FlockID, hr.WeekNo, hr.SetDate, hr.HatchDate, hr.SettableEggs, hr.InfertileEggs, hr.FertileEggs, hr.DeadInShell, " +
                "hr.HatchedChicks, hr.SaleableChicks, hr.FertilityPct, CASE WHEN hr.HatchDate IS NOT NULL THEN hr.HatchabilityPct END AS HatchabilityPct, " +
                "CASE WHEN hr.HatchDate IS NOT NULL THEN hr.HatchOfFertilePct END AS HatchOfFertilePct, hr.Remarks " +
                "FROM dbo.tblHatchResult hr INNER JOIN dbo.tblFlockWeekly w ON w.FlockID = hr.FlockID AND w.WeekNo = hr.WeekNo " +
                "WHERE w.FlockWeeklyID = @ID ORDER BY hr.SetDate;",
                r => new HatchResult
                {
                    HatchResultID = r.Get<int>("HatchResultID"),
                    FlockID = r.Get<int>("FlockID"),
                    WeekNo = r.Get<int>("WeekNo"),
                    SetDate = r.Get<DateTime>("SetDate"),
                    HatchDate = r.Get<DateTime?>("HatchDate"),
                    SettableEggs = r.Get<int>("SettableEggs"),
                    InfertileEggs = r.Get<int>("InfertileEggs"),
                    FertileEggs = r.Get<int>("FertileEggs"),
                    DeadInShell = r.Get<int>("DeadInShell"),
                    HatchedChicks = r.Get<int>("HatchedChicks"),
                    SaleableChicks = r.Get<int?>("SaleableChicks"),
                    FertilityPct = r.Get<decimal?>("FertilityPct"),
                    HatchabilityPct = r.Get<decimal?>("HatchabilityPct"),
                    HatchOfFertilePct = r.Get<decimal?>("HatchOfFertilePct"),
                    Remarks = r.Str("Remarks")
                }, Sql.P("@ID", flockWeeklyId));
            return p;
        }

        public int Save(WeeklyEntryInput i, IUserContext user)
        {
            // main entry + production / environment in one transaction
            return _db.InTransaction(tx =>
            {
                var id = SaveMain(tx, i, user);
                var p = i.Production ?? new ProductionInput();
                tx.ExecuteProc("dbo.sp_FlockWeekly_SaveProduction",
                    Sql.P("@FlockWeeklyID", id), Sql.P("@TotalEggs", p.TotalEggs), Sql.P("@HatchingEggs", p.HatchingEggs),
                    Sql.P("@FloorEggs", p.FloorEggs), Sql.P("@DirtyEggs", p.DirtyEggs), Sql.P("@BrokenEggs", p.BrokenEggs),
                    Sql.P("@CrackedEggs", p.CrackedEggs), Sql.P("@DoubleYolkEggs", p.DoubleYolkEggs),
                    Sql.Decimal("@AvgEggWeight", p.AvgEggWeight, 8, 2), Sql.Decimal("@LightHours", p.LightHours, 5, 2),
                    Sql.Decimal("@LightIntensity", p.LightIntensity, 8, 2), Sql.Decimal("@TempDay", p.TempDay, 5, 2),
                    Sql.Decimal("@TempNight", p.TempNight, 5, 2),
                    Sql.P("@UserID", user.UserID), Sql.NVarChar("@UserName", user.UserName, 50), Sql.NVarChar("@IPAddress", user.IPAddress, 50));
                return id;
            });
        }

        private int SaveMain(IDbExecutor db, WeeklyEntryInput i, IUserContext user)
        {
            var outId = Sql.OutInt("@NewFlockWeeklyID");
            db.ExecuteProc("dbo.sp_FlockWeekly_Save",
                Sql.P("@FlockWeeklyID", i.FlockWeeklyID), Sql.P("@FlockID", i.FlockID), Sql.P("@WeekNo", i.WeekNo), Sql.Date("@EntryDate", i.EntryDate),
                Sql.P("@FeedTypeID", i.FeedTypeID), Sql.P("@FeedFormulaID", i.FeedFormulaID),
                Sql.P("@MaleMortality", i.MaleMortality), Sql.P("@FemaleMortality", i.FemaleMortality),
                Sql.P("@MaleCulling", i.MaleCulling), Sql.P("@FemaleCulling", i.FemaleCulling),
                Sql.Decimal("@ManualBodyWeight", i.ManualBodyWeight, 10, 2), Sql.Decimal("@TotalFeedKg", i.TotalFeedKg, 14, 3),
                Sql.Decimal("@FeedWastageKg", i.FeedWastageKg, 14, 3),
                Sql.P("@NutritionSource", string.IsNullOrEmpty(i.NutritionSource) ? null : i.NutritionSource),
                Sql.NVarChar("@LabSampleRef", i.LabSampleRef, 50), Sql.Date("@AnalysisDate", i.AnalysisDate),
                Sql.P("@MortalityReasonID", i.MortalityReasonID), Sql.NVarChar("@Remarks", i.Remarks, 500),
                Sql.P("@SamplesJson", SamplesJson(i)), Sql.P("@FeedJson", FeedJson(i)), Sql.P("@NutrientsJson", NutrientsJson(i)),
                Sql.P("@MortalityJson", MortalityJson(i)), Sql.RowVersion("@RowVer", i.RowVer),
                Sql.P("@UserID", user.UserID), Sql.NVarChar("@UserName", user.UserName, 50), Sql.NVarChar("@IPAddress", user.IPAddress, 50), outId);
            return Sql.ToInt(outId.Value);
        }

        public void SaveMortality(WeeklyEntryInput i, IUserContext user)
        {
            _db.ExecuteProc("dbo.sp_SaveMortality",
                Sql.P("@FlockWeeklyID", i.FlockWeeklyID), Sql.P("@MaleMortality", i.MaleMortality), Sql.P("@FemaleMortality", i.FemaleMortality),
                Sql.P("@MaleCulling", i.MaleCulling), Sql.P("@FemaleCulling", i.FemaleCulling), Sql.P("@MortalityReasonID", i.MortalityReasonID),
                Sql.P("@MortalityJson", MortalityJson(i)), Sql.NVarChar("@Remarks", i.Remarks, 500),
                Sql.P("@UserID", user.UserID), Sql.NVarChar("@UserName", user.UserName, 50), Sql.NVarChar("@IPAddress", user.IPAddress, 50));
        }

        public void DeleteLast(int flockId, IUserContext user)
        {
            _db.ExecuteProc("dbo.sp_FlockWeekly_DeleteLast", Sql.P("@FlockID", flockId), Sql.P("@UserID", user.UserID),
                Sql.NVarChar("@UserName", user.UserName, 50), Sql.NVarChar("@IPAddress", user.IPAddress, 50));
        }

        public PagedResult<WeeklyListItem> Search(WeeklySearchCriteria c)
        {
            c = c ?? new WeeklySearchCriteria();
            int page = Math.Max(1, c.PageNo), size = c.PageSize < 1 || c.PageSize > 200 ? 25 : c.PageSize, total = 0;
            var items = _db.Query(
                "SELECT w.FlockWeeklyID, w.FlockID, f.FlockNumber, fm.FarmName, h.HouseName, w.WeekNo, w.EntryDate, w.OpeningPopulation, w.TotalMortality, " +
                "w.ClosingPopulation, w.ActualBodyWeight, w.StandardBodyWeight, w.ActualFeedIntake, w.UniformityPercentage, w.MortalityPercentage, " +
                "w.PerformanceScore, w.OverallStatusCode, u.UserName AS CreatedByName, " +
                "CAST(CASE WHEN w.WeekNo = (SELECT MAX(x.WeekNo) FROM dbo.tblFlockWeekly x WHERE x.FlockID = w.FlockID) THEN 1 ELSE 0 END AS BIT) AS IsLatest, " +
                "COUNT(*) OVER () AS TotalCount " +
                "FROM dbo.tblFlockWeekly w INNER JOIN dbo.tblFlock f ON f.FlockID = w.FlockID INNER JOIN dbo.tblFarm fm ON fm.FarmID = f.FarmID " +
                "INNER JOIN dbo.tblHouse h ON h.HouseID = w.HouseID LEFT JOIN dbo.tblUsers u ON u.UserID = w.CreatedBy " +
                "WHERE (@FarmID IS NULL OR f.FarmID = @FarmID) AND (@HouseID IS NULL OR w.HouseID = @HouseID) AND (@FlockID IS NULL OR w.FlockID = @FlockID) " +
                "AND (@FromWeek IS NULL OR w.WeekNo >= @FromWeek) AND (@ToWeek IS NULL OR w.WeekNo <= @ToWeek) " +
                "AND (@FromDate IS NULL OR w.EntryDate >= @FromDate) AND (@ToDate IS NULL OR w.EntryDate <= @ToDate) " +
                "AND (@RestrictFarmID IS NULL OR f.FarmID = @RestrictFarmID) " +
                "ORDER BY w.EntryDate DESC, f.FlockNumber, w.WeekNo DESC OFFSET (@PageNo - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;",
                r =>
                {
                    total = r.Get<int>("TotalCount");
                    return new WeeklyListItem
                    {
                        FlockWeeklyID = r.Get<int>("FlockWeeklyID"),
                        FlockID = r.Get<int>("FlockID"),
                        FlockNumber = r.Str("FlockNumber"),
                        FarmName = r.Str("FarmName"),
                        HouseName = r.Str("HouseName"),
                        WeekNo = r.Get<int>("WeekNo"),
                        EntryDate = r.Get<DateTime>("EntryDate"),
                        OpeningPopulation = r.Get<int>("OpeningPopulation"),
                        TotalMortality = r.Get<int>("TotalMortality"),
                        ClosingPopulation = r.Get<int>("ClosingPopulation"),
                        ActualBodyWeight = r.Get<decimal?>("ActualBodyWeight"),
                        StandardBodyWeight = r.Get<decimal?>("StandardBodyWeight"),
                        ActualFeedIntake = r.Get<decimal?>("ActualFeedIntake"),
                        UniformityPercentage = r.Get<decimal?>("UniformityPercentage"),
                        MortalityPercentage = r.Get<decimal?>("MortalityPercentage"),
                        PerformanceScore = r.Get<decimal?>("PerformanceScore"),
                        OverallStatusCode = r.Str("OverallStatusCode"),
                        IsLatest = r.Get<bool>("IsLatest"),
                        CreatedByName = r.Str("CreatedByName")
                    };
                },
                Sql.P("@FarmID", c.FarmID), Sql.P("@HouseID", c.HouseID), Sql.P("@FlockID", c.FlockID), Sql.P("@FromWeek", c.FromWeek),
                Sql.P("@ToWeek", c.ToWeek), Sql.Date("@FromDate", c.FromDate), Sql.Date("@ToDate", c.ToDate),
                Sql.P("@RestrictFarmID", c.RestrictFarmID), Sql.P("@PageNo", page), Sql.P("@PageSize", size));
            return new PagedResult<WeeklyListItem>(items, total, page, size);
        }

        private static string SamplesJson(WeeklyEntryInput i)
        {
            return JsonConvert.SerializeObject((i.Samples ?? new List<WeightSample>()).Where(s => s.Weight > 0)
                .Select(s => new { sex = string.IsNullOrEmpty(s.Sex) ? "F" : s.Sex, w = s.Weight }));
        }

        private static string FeedJson(WeeklyEntryInput i)
        {
            return JsonConvert.SerializeObject((i.FeedLines ?? new List<FeedLine>()).Where(f => f.FeedTypeID > 0)
                .Select(f => new { feedTypeId = f.FeedTypeID, formulaId = f.FeedFormulaID, sex = string.IsNullOrEmpty(f.Sex) ? "B" : f.Sex, qty = f.QuantityKg, waste = f.WastageKg }));
        }

        private static string NutrientsJson(WeeklyEntryInput i)
        {
            return JsonConvert.SerializeObject((i.Nutrients ?? new List<NutrientValue>()).Where(n => n.Value.HasValue && !string.IsNullOrEmpty(n.ParameterCode))
                .Select(n => new { code = n.ParameterCode, value = n.Value }));
        }

        private static string MortalityJson(WeeklyEntryInput i)
        {
            return JsonConvert.SerializeObject((i.MortalityReasons ?? new List<MortalityReasonLine>()).Where(m => m.BirdCount > 0 && m.MortalityReasonID > 0)
                .Select(m => new { reasonId = m.MortalityReasonID, sex = string.IsNullOrEmpty(m.Sex) ? "F" : m.Sex, count = m.BirdCount }));
        }

        private static WeeklyRecord Map(IDataRecord r)
        {
            return new WeeklyRecord
            {
                FlockWeeklyID = r.Get<int>("FlockWeeklyID"),
                FlockID = r.Get<int>("FlockID"),
                FlockNumber = r.Str("FlockNumber"),
                LotNumber = r.Str("LotNumber"),
                BirdTypeID = r.Get<int>("BirdTypeID"),
                BirdTypeName = r.Str("BirdTypeName"),
                BreedName = r.Str("BreedName"),
                FarmID = r.Get<int>("FarmID"),
                FarmName = r.Str("FarmName"),
                HouseName = r.Str("HouseName"),
                WeekNo = r.Get<int>("WeekNo"),
                AgeInWeeks = r.Get<int>("AgeInWeeks"),
                EntryDate = r.Get<DateTime>("EntryDate"),
                OpeningPopulation = r.Get<int>("OpeningPopulation"),
                OpeningMale = r.Get<int>("OpeningMale"),
                OpeningFemale = r.Get<int>("OpeningFemale"),
                BirdsReceived = r.Get<int>("BirdsReceived"),
                MaleMortality = r.Get<int>("MaleMortality"),
                FemaleMortality = r.Get<int>("FemaleMortality"),
                TotalMortality = r.Get<int>("TotalMortality"),
                MaleCulling = r.Get<int>("MaleCulling"),
                FemaleCulling = r.Get<int>("FemaleCulling"),
                TransferOut = r.Get<int>("TransferOut"),
                ClosingPopulation = r.Get<int>("ClosingPopulation"),
                ClosingMale = r.Get<int>("ClosingMale"),
                ClosingFemale = r.Get<int>("ClosingFemale"),
                MortalityPercentage = r.Get<decimal?>("MortalityPercentage"),
                StandardMortalityPct = r.Get<decimal?>("StandardMortalityPct"),
                FeedTypeID = r.Get<int?>("FeedTypeID"),
                FeedTypeName = r.Str("FeedTypeName"),
                FeedFormulaID = r.Get<int?>("FeedFormulaID"),
                FormulaCode = r.Str("FormulaCode"),
                TotalFeedKg = r.Get<decimal?>("TotalFeedKg"),
                FeedWastageKg = r.Get<decimal?>("FeedWastageKg"),
                StandardFeedIntake = r.Get<decimal?>("StandardFeedIntake"),
                ActualFeedIntake = r.Get<decimal?>("ActualFeedIntake"),
                StandardWeightGain = r.Get<decimal?>("StandardWeightGain"),
                ActualWeightGain = r.Get<decimal?>("ActualWeightGain"),
                StandardBodyWeight = r.Get<decimal?>("StandardBodyWeight"),
                ActualBodyWeight = r.Get<decimal?>("ActualBodyWeight"),
                ManualBodyWeight = r.Get<decimal?>("ManualBodyWeight"),
                MinBodyWeight = r.Get<decimal?>("MinBodyWeight"),
                MaxBodyWeight = r.Get<decimal?>("MaxBodyWeight"),
                StdDevBodyWeight = r.Get<decimal?>("StdDevBodyWeight"),
                CVPercentage = r.Get<decimal?>("CVPercentage"),
                SampleSize = r.Get<int?>("SampleSize"),
                StandardUniformity = r.Get<decimal?>("StandardUniformity"),
                UniformityPercentage = r.Get<decimal?>("UniformityPercentage"),
                PerformanceScore = r.Get<decimal?>("PerformanceScore"),
                OverallStatusCode = r.Str("OverallStatusCode"),
                BandName = r.Str("BandName"),
                BodyWeightScore = r.Get<decimal?>("BodyWeightScore"),
                WeightGainScore = r.Get<decimal?>("WeightGainScore"),
                FeedIntakeScore = r.Get<decimal?>("FeedIntakeScore"),
                UniformityScore = r.Get<decimal?>("UniformityScore"),
                MortalityScore = r.Get<decimal?>("MortalityScore"),
                NutritionScore = r.Get<decimal?>("NutritionScore"),
                NutritionSource = r.Str("NutritionSource"),
                LabSampleRef = r.Str("LabSampleRef"),
                AnalysisDate = r.Get<DateTime?>("AnalysisDate"),
                Remarks = r.Str("Remarks"),
                RowVer = r.Get<byte[]>("RowVer"),
                LastWeek = r.Get<int>("LastWeek"),
                CreatedDate = r.Get<DateTime>("CreatedDate"),
                ModifiedDate = r.Get<DateTime?>("ModifiedDate")
            };
        }
    }
}
