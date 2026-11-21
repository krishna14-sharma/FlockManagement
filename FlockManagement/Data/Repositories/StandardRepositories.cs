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
using Newtonsoft.Json;

namespace FlockManagement.Data.Repositories
{
    public class ParameterRepository : IParameterRepository
    {
        private readonly IDb _db;

        public ParameterRepository(IDb db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        public IList<Parameter> GetAll(bool activeOnly = true)
        {
            return _db.Query(
                "SELECT p.ParameterID, p.ParameterCode, p.ParameterName, p.ParameterGroup, p.UnitID, u.UnitCode, p.DecimalPlaces, p.Direction, " +
                "p.LogicalMin, p.LogicalMax, p.SortOrder FROM dbo.tblParameter p INNER JOIN dbo.tblUnit u ON u.UnitID = p.UnitID " +
                "WHERE (@ActiveOnly = 0 OR p.IsActive = 1) ORDER BY p.SortOrder;",
                Map, Sql.P("@ActiveOnly", activeOnly));
        }

        public IList<LookupItem> GetUnits()
        {
            return _db.Query("SELECT UnitID AS Id, UnitCode + N' - ' + UnitName AS Text FROM dbo.tblUnit WHERE IsActive = 1 ORDER BY UnitCode;",
                r => new LookupItem(r.Get<int>("Id"), r.Str("Text")));
        }

        internal static Parameter Map(IDataRecord r)
        {
            return new Parameter
            {
                ParameterID = r.Get<int>("ParameterID"),
                ParameterCode = r.Str("ParameterCode"),
                ParameterName = r.Str("ParameterName"),
                ParameterGroup = r.Str("ParameterGroup"),
                UnitID = r.Get<int>("UnitID"),
                UnitCode = r.Str("UnitCode"),
                DecimalPlaces = r.Get<int>("DecimalPlaces"),
                Direction = r.Str("Direction"),
                LogicalMin = r.Get<decimal?>("LogicalMin"),
                LogicalMax = r.Get<decimal?>("LogicalMax"),
                SortOrder = r.Get<int>("SortOrder")
            };
        }
    }

    public class StandardRepository : IStandardRepository
    {
        private readonly IDb _db;

        public StandardRepository(IDb db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        public PagedResult<Standard> Search(StandardSearchCriteria c)
        {
            c = c ?? new StandardSearchCriteria();
            var page = Math.Max(1, c.PageNo);
            var size = c.PageSize < 1 || c.PageSize > 500 ? 50 : c.PageSize;
            var total = 0;
            var items = _db.QueryProc("dbo.sp_Standard_Search", r =>
            {
                total = r.Get<int>("TotalCount");
                return new Standard
                {
                    StandardID = r.Get<int>("StandardID"),
                    BirdTypeID = r.Get<int>("BirdTypeID"),
                    BirdTypeName = r.Str("BirdTypeName"),
                    BreedID = r.Get<int?>("BreedID"),
                    BreedName = r.Str("BreedName"),
                    FeedTypeID = r.Get<int?>("FeedTypeID"),
                    FeedTypeName = r.Str("FeedTypeName"),
                    AgeWeek = r.Get<int>("AgeWeek"),
                    ParameterID = r.Get<int>("ParameterID"),
                    ParameterName = r.Str("ParameterName"),
                    ParameterGroup = r.Str("ParameterGroup"),
                    UnitCode = r.Str("UnitCode"),
                    DecimalPlaces = r.Get<int>("DecimalPlaces"),
                    StandardValue = r.Get<decimal>("StandardValue"),
                    MinValue = r.Get<decimal?>("MinValue"),
                    MaxValue = r.Get<decimal?>("MaxValue"),
                    CriticalMinValue = r.Get<decimal?>("CriticalMinValue"),
                    CriticalMaxValue = r.Get<decimal?>("CriticalMaxValue"),
                    EffectiveFrom = r.Get<DateTime>("EffectiveFrom"),
                    EffectiveTo = r.Get<DateTime?>("EffectiveTo"),
                    IsActive = r.Get<bool>("IsActive")
                };
            },
                Sql.P("@BirdTypeID", c.BirdTypeID), Sql.P("@BreedID", c.BreedID), Sql.P("@ParameterID", c.ParameterID),
                Sql.NVarChar("@ParameterGroup", c.ParameterGroup, 30), Sql.P("@FromWeek", c.FromWeek), Sql.P("@ToWeek", c.ToWeek),
                Sql.P("@ActiveOnly", c.ActiveOnly), Sql.P("@PageNo", page), Sql.P("@PageSize", size));
            return new PagedResult<Standard>(items, total, page, size);
        }

        public IList<StandardGridRow> GetGrid(StandardGridKey key)
        {
            return _db.QueryProc("dbo.sp_Standard_GetGrid", r => new StandardGridRow
            {
                AgeWeek = r.Get<int>("AgeWeek"),
                StandardValue = r.Get<decimal?>("StandardValue"),
                MinValue = r.Get<decimal?>("MinValue"),
                MaxValue = r.Get<decimal?>("MaxValue"),
                CriticalMinValue = r.Get<decimal?>("CriticalMinValue"),
                CriticalMaxValue = r.Get<decimal?>("CriticalMaxValue")
            },
                Sql.P("@BirdTypeID", key.BirdTypeID), Sql.P("@BreedID", key.BreedID), Sql.P("@FeedTypeID", key.FeedTypeID),
                Sql.P("@ParameterID", key.ParameterID), Sql.Date("@EffectiveFrom", key.EffectiveFrom));
        }

        public void SaveGrid(StandardGridKey key, IEnumerable<StandardGridRow> rows, IUserContext user)
        {
            var json = JsonConvert.SerializeObject((rows ?? Enumerable.Empty<StandardGridRow>()).Select(r => new
            {
                week = r.AgeWeek,
                std = r.StandardValue,
                min = r.MinValue,
                max = r.MaxValue,
                cmin = r.CriticalMinValue,
                cmax = r.CriticalMaxValue
            }));
            _db.ExecuteProc("dbo.sp_Standard_SaveGrid",
                Sql.P("@BirdTypeID", key.BirdTypeID), Sql.P("@BreedID", key.BreedID), Sql.P("@FeedTypeID", key.FeedTypeID),
                Sql.P("@ParameterID", key.ParameterID), Sql.Date("@EffectiveFrom", key.EffectiveFrom), Sql.Date("@EffectiveTo", key.EffectiveTo),
                Sql.P("@RowsJson", json), Sql.P("@UserID", user.UserID), Sql.NVarChar("@UserName", user.UserName, 50),
                Sql.NVarChar("@IPAddress", user.IPAddress, 50));
        }

        public void SetActive(int standardId, bool isActive, IUserContext user)
        {
            _db.ExecuteProc("dbo.sp_Standard_SetActive", Sql.P("@StandardID", standardId), Sql.P("@IsActive", isActive),
                Sql.P("@UserID", user.UserID), Sql.NVarChar("@UserName", user.UserName, 50), Sql.NVarChar("@IPAddress", user.IPAddress, 50));
        }

        public IList<ParameterStandard> GetFlockStandards(int flockId, int ageWeek, int? feedTypeId, DateTime? asOf)
        {
            return _db.QueryProc("dbo.sp_GetFlockStandard", r => new ParameterStandard
            {
                ParameterID = r.Get<int>("ParameterID"),
                ParameterCode = r.Str("ParameterCode"),
                ParameterName = r.Str("ParameterName"),
                ParameterGroup = r.Str("ParameterGroup"),
                UnitCode = r.Str("UnitCode"),
                Direction = r.Str("Direction"),
                DecimalPlaces = r.Get<int>("DecimalPlaces"),
                SortOrder = r.Get<int>("SortOrder"),
                LogicalMin = r.Get<decimal?>("LogicalMin"),
                LogicalMax = r.Get<decimal?>("LogicalMax"),
                StandardValue = r.Get<decimal?>("StandardValue"),
                MinValue = r.Get<decimal?>("MinValue"),
                MaxValue = r.Get<decimal?>("MaxValue"),
                CriticalMinValue = r.Get<decimal?>("CriticalMinValue"),
                CriticalMaxValue = r.Get<decimal?>("CriticalMaxValue"),
                WarningPct = r.Get<decimal>("WarningPct"),
                CriticalPct = r.Get<decimal>("CriticalPct")
            },
                Sql.P("@FlockID", flockId), Sql.P("@AgeWeek", ageWeek), Sql.P("@FeedTypeID", feedTypeId), Sql.Date("@AsOf", asOf));
        }

        public IList<DateTime> GetEffectiveDates(int birdTypeId, int parameterId)
        {
            return _db.Query("SELECT DISTINCT EffectiveFrom FROM dbo.tblStandard WHERE BirdTypeID = @B AND ParameterID = @P AND IsActive = 1 ORDER BY EffectiveFrom DESC;",
                r => r.Get<DateTime>("EffectiveFrom"), Sql.P("@B", birdTypeId), Sql.P("@P", parameterId));
        }
    }

    public class MortalityReasonRepository : MasterRepositoryBase<MortalityReason>, IMortalityReasonRepository
    {
        public MortalityReasonRepository(IDb db) : base(db) { }

        protected override string TableName { get { return "dbo.tblMortalityReason"; } }
        protected override string Alias { get { return "m"; } }
        protected override string KeyColumn { get { return "MortalityReasonID"; } }
        protected override string FromClause { get { return "dbo.tblMortalityReason m"; } }
        protected override string[] SearchColumns { get { return new[] { "m.ReasonCode", "m.ReasonName", "m.Category" }; } }
        protected override string OrderBy { get { return "m.Category, m.ReasonName"; } }
        protected override string LookupTextExpression { get { return "m.ReasonName"; } }
        protected override string SelectColumns
        {
            get { return "m.MortalityReasonID, m.ReasonCode, m.ReasonName, m.Category, " + AuditColumns.For("m"); }
        }

        protected override MortalityReason Map(IDataRecord r)
        {
            var e = new MortalityReason
            {
                MortalityReasonID = r.Get<int>("MortalityReasonID"),
                ReasonCode = r.Str("ReasonCode"),
                ReasonName = r.Str("ReasonName"),
                Category = r.Str("Category")
            };
            AuditColumns.Fill(e, r);
            return e;
        }

        protected override int GetKey(MortalityReason e) { return e.MortalityReasonID; }

        protected override int Insert(IDbExecutor db, MortalityReason e, int userId)
        {
            return InsertAndGetId(db,
                "INSERT INTO dbo.tblMortalityReason (ReasonCode, ReasonName, Category, IsActive, CreatedBy) VALUES (@Code, @Name, @Category, @IsActive, @UserID);",
                Sql.NVarChar("@Code", e.ReasonCode, 20), Sql.NVarChar("@Name", e.ReasonName, 100), Sql.NVarChar("@Category", e.Category, 50),
                Sql.P("@IsActive", e.IsActive), Sql.P("@UserID", userId));
        }

        protected override void Update(IDbExecutor db, MortalityReason e, int userId)
        {
            db.Execute("UPDATE dbo.tblMortalityReason SET ReasonCode = @Code, ReasonName = @Name, Category = @Category, IsActive = @IsActive, " +
                       "ModifiedBy = @UserID, ModifiedDate = SYSDATETIME() WHERE MortalityReasonID = @ID;",
                Sql.NVarChar("@Code", e.ReasonCode, 20), Sql.NVarChar("@Name", e.ReasonName, 100), Sql.NVarChar("@Category", e.Category, 50),
                Sql.P("@IsActive", e.IsActive), Sql.P("@UserID", userId), Sql.P("@ID", e.MortalityReasonID));
        }
    }

    public class FeedFormulaRepository : IFeedFormulaRepository
    {
        private readonly IDb _db;

        public FeedFormulaRepository(IDb db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        private const string Select =
            "SELECT f.FeedFormulaID, f.FeedTypeID, ft.FeedTypeName, f.FormulaCode, f.FormulaName, f.EffectiveFrom, f.EffectiveTo, f.Remarks, f.IsActive, " +
            "(SELECT COUNT(*) FROM dbo.tblFeedFormulaDetail d WHERE d.FeedFormulaID = f.FeedFormulaID) AS NutrientCount ";

        public PagedResult<FeedFormula> Search(string search, int? feedTypeId, bool? isActive, int pageNo, int pageSize)
        {
            pageNo = Math.Max(1, pageNo);
            pageSize = pageSize < 1 || pageSize > 200 ? 25 : pageSize;
            var total = 0;
            var items = _db.Query(Select + ", COUNT(*) OVER () AS TotalCount FROM dbo.tblFeedFormula f INNER JOIN dbo.tblFeedType ft ON ft.FeedTypeID = f.FeedTypeID " +
                                  "WHERE (@Search IS NULL OR f.FormulaCode LIKE @Search OR f.FormulaName LIKE @Search) AND (@FeedTypeID IS NULL OR f.FeedTypeID = @FeedTypeID) " +
                                  "AND (@IsActive IS NULL OR f.IsActive = @IsActive) ORDER BY ft.FeedTypeName, f.EffectiveFrom DESC " +
                                  "OFFSET (@PageNo - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;",
                r => { total = r.Get<int>("TotalCount"); return Map(r); },
                Sql.NVarChar("@Search", Sql.ContainsPattern(search), 210), Sql.P("@FeedTypeID", feedTypeId), Sql.P("@IsActive", isActive),
                Sql.P("@PageNo", pageNo), Sql.P("@PageSize", pageSize));
            return new PagedResult<FeedFormula>(items, total, pageNo, pageSize);
        }

        public FeedFormula GetById(int feedFormulaId)
        {
            var f = _db.QuerySingle(Select + "FROM dbo.tblFeedFormula f INNER JOIN dbo.tblFeedType ft ON ft.FeedTypeID = f.FeedTypeID WHERE f.FeedFormulaID = @ID;",
                Map, Sql.P("@ID", feedFormulaId));
            if (f == null) return null;
            f.Details = _db.Query(
                "SELECT p.ParameterID, p.ParameterCode, p.ParameterName, u.UnitCode, p.DecimalPlaces, p.LogicalMin, p.LogicalMax, d.FormulatedValue AS Value " +
                "FROM dbo.tblParameter p INNER JOIN dbo.tblUnit u ON u.UnitID = p.UnitID " +
                "LEFT JOIN dbo.tblFeedFormulaDetail d ON d.ParameterID = p.ParameterID AND d.FeedFormulaID = @ID " +
                "WHERE p.ParameterGroup = N'NUTRITION' AND p.IsActive = 1 ORDER BY p.SortOrder;",
                MapDetail, Sql.P("@ID", feedFormulaId));
            return f;
        }

        public IList<FeedFormulaDetail> GetBlankDetails()
        {
            return _db.Query(
                "SELECT p.ParameterID, p.ParameterCode, p.ParameterName, u.UnitCode, p.DecimalPlaces, p.LogicalMin, p.LogicalMax, CAST(NULL AS DECIMAL(18,4)) AS Value " +
                "FROM dbo.tblParameter p INNER JOIN dbo.tblUnit u ON u.UnitID = p.UnitID WHERE p.ParameterGroup = N'NUTRITION' AND p.IsActive = 1 ORDER BY p.SortOrder;",
                MapDetail);
        }

        public int Save(FeedFormula f, IUserContext user)
        {
            var json = JsonConvert.SerializeObject(f.Details.Where(d => d.Value.HasValue).Select(d => new { parameterId = d.ParameterID, value = d.Value }));
            var outId = Sql.OutInt("@NewFeedFormulaID");
            _db.ExecuteProc("dbo.sp_FeedFormula_Save",
                Sql.P("@FeedFormulaID", f.FeedFormulaID), Sql.P("@FeedTypeID", f.FeedTypeID), Sql.NVarChar("@FormulaCode", f.FormulaCode, 30),
                Sql.NVarChar("@FormulaName", f.FormulaName, 100), Sql.Date("@EffectiveFrom", f.EffectiveFrom), Sql.Date("@EffectiveTo", f.EffectiveTo),
                Sql.NVarChar("@Remarks", f.Remarks, 250), Sql.P("@IsActive", f.IsActive), Sql.P("@DetailsJson", json),
                Sql.P("@UserID", user.UserID), Sql.NVarChar("@UserName", user.UserName, 50), Sql.NVarChar("@IPAddress", user.IPAddress, 50), outId);
            return Sql.ToInt(outId.Value);
        }

        public IList<LookupItem> GetLookup(int? feedTypeId, DateTime? onDate)
        {
            return _db.Query(
                "SELECT FeedFormulaID AS Id, FormulaCode + N' - ' + FormulaName AS Text, FeedTypeID AS ParentId FROM dbo.tblFeedFormula " +
                "WHERE IsActive = 1 AND (@FeedTypeID IS NULL OR FeedTypeID = @FeedTypeID) " +
                "AND (@OnDate IS NULL OR (EffectiveFrom <= @OnDate AND (EffectiveTo IS NULL OR EffectiveTo >= @OnDate))) ORDER BY EffectiveFrom DESC;",
                r => new LookupItem(r.Get<int>("Id"), r.Str("Text"), r.Get<int?>("ParentId")),
                Sql.P("@FeedTypeID", feedTypeId), Sql.Date("@OnDate", onDate));
        }

        private static FeedFormula Map(IDataRecord r)
        {
            return new FeedFormula
            {
                FeedFormulaID = r.Get<int>("FeedFormulaID"),
                FeedTypeID = r.Get<int>("FeedTypeID"),
                FeedTypeName = r.Str("FeedTypeName"),
                FormulaCode = r.Str("FormulaCode"),
                FormulaName = r.Str("FormulaName"),
                EffectiveFrom = r.Get<DateTime>("EffectiveFrom"),
                EffectiveTo = r.Get<DateTime?>("EffectiveTo"),
                Remarks = r.Str("Remarks"),
                IsActive = r.Get<bool>("IsActive"),
                NutrientCount = r.Get<int>("NutrientCount")
            };
        }

        private static FeedFormulaDetail MapDetail(IDataRecord r)
        {
            return new FeedFormulaDetail
            {
                ParameterID = r.Get<int>("ParameterID"),
                ParameterCode = r.Str("ParameterCode"),
                ParameterName = r.Str("ParameterName"),
                UnitCode = r.Str("UnitCode"),
                DecimalPlaces = r.Get<int>("DecimalPlaces"),
                LogicalMin = r.Get<decimal?>("LogicalMin"),
                LogicalMax = r.Get<decimal?>("LogicalMax"),
                Value = r.Get<decimal?>("Value")
            };
        }
    }

    public class MovementRepository : IMovementRepository
    {
        private readonly IDb _db;

        public MovementRepository(IDb db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        private const string Where =
            "WHERE (@FlockID IS NULL OR f.FlockID = @FlockID) AND (@FarmID IS NULL OR f.FarmID = @FarmID) " +
            "AND (@RestrictFarmID IS NULL OR f.FarmID = @RestrictFarmID) ";

        private static System.Data.SqlClient.SqlParameter[] Params(MovementSearchCriteria c, int page, int size)
        {
            return new[]
            {
                Sql.P("@FlockID", c.FlockID), Sql.P("@FarmID", c.FarmID), Sql.P("@RestrictFarmID", c.RestrictFarmID),
                Sql.Date("@FromDate", c.FromDate), Sql.Date("@ToDate", c.ToDate), Sql.P("@PageNo", page), Sql.P("@PageSize", size)
            };
        }

        public PagedResult<PlacementRecord> SearchPlacements(MovementSearchCriteria c)
        {
            c = c ?? new MovementSearchCriteria();
            int page = Math.Max(1, c.PageNo), size = c.PageSize < 1 || c.PageSize > 200 ? 25 : c.PageSize, total = 0;
            var items = _db.Query(
                "SELECT p.FlockPlacementID, p.FlockID, f.FlockNumber, p.PlacementDate, p.HouseID, h.HouseName, p.MaleCount, p.FemaleCount, p.Source, " +
                "p.ChickAvgWeight, p.IsInitial, p.Remarks, u.UserName AS CreatedByName, p.CreatedDate, COUNT(*) OVER () AS TotalCount " +
                "FROM dbo.tblFlockPlacement p INNER JOIN dbo.tblFlock f ON f.FlockID = p.FlockID INNER JOIN dbo.tblHouse h ON h.HouseID = p.HouseID " +
                "LEFT JOIN dbo.tblUsers u ON u.UserID = p.CreatedBy " + Where +
                "AND (@FromDate IS NULL OR p.PlacementDate >= @FromDate) AND (@ToDate IS NULL OR p.PlacementDate <= @ToDate) " +
                "ORDER BY p.PlacementDate DESC, p.FlockPlacementID DESC OFFSET (@PageNo - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;",
                r =>
                {
                    total = r.Get<int>("TotalCount");
                    return new PlacementRecord
                    {
                        FlockPlacementID = r.Get<int>("FlockPlacementID"),
                        FlockID = r.Get<int>("FlockID"),
                        FlockNumber = r.Str("FlockNumber"),
                        PlacementDate = r.Get<DateTime>("PlacementDate"),
                        HouseID = r.Get<int>("HouseID"),
                        HouseName = r.Str("HouseName"),
                        MaleCount = r.Get<int>("MaleCount"),
                        FemaleCount = r.Get<int>("FemaleCount"),
                        Source = r.Str("Source"),
                        ChickAvgWeight = r.Get<decimal?>("ChickAvgWeight"),
                        IsInitial = r.Get<bool>("IsInitial"),
                        Remarks = r.Str("Remarks"),
                        CreatedByName = r.Str("CreatedByName"),
                        CreatedDate = r.Get<DateTime>("CreatedDate")
                    };
                }, Params(c, page, size));
            return new PagedResult<PlacementRecord>(items, total, page, size);
        }

        public int AddPlacement(PlacementRecord p, IUserContext user)
        {
            var outId = Sql.OutInt("@NewPlacementID");
            _db.ExecuteProc("dbo.sp_FlockPlacement_Add",
                Sql.P("@FlockID", p.FlockID), Sql.Date("@PlacementDate", p.PlacementDate), Sql.P("@HouseID", p.HouseID > 0 ? (object)p.HouseID : null),
                Sql.P("@MaleCount", p.MaleCount), Sql.P("@FemaleCount", p.FemaleCount), Sql.NVarChar("@Source", p.Source, 100),
                Sql.Decimal("@ChickAvgWeight", p.ChickAvgWeight, 10, 2), Sql.NVarChar("@Remarks", p.Remarks, 500),
                Sql.P("@UserID", user.UserID), Sql.NVarChar("@UserName", user.UserName, 50), Sql.NVarChar("@IPAddress", user.IPAddress, 50), outId);
            return Sql.ToInt(outId.Value);
        }

        public PagedResult<TransferRecord> SearchTransfers(MovementSearchCriteria c)
        {
            c = c ?? new MovementSearchCriteria();
            int page = Math.Max(1, c.PageNo), size = c.PageSize < 1 || c.PageSize > 200 ? 25 : c.PageSize, total = 0;
            var items = _db.Query(
                "SELECT t.FlockTransferID, t.FlockID, f.FlockNumber, t.TransferDate, t.WeekNo, t.TransferType, t.FromHouseID, hf.HouseName AS FromHouseName, " +
                "t.ToHouseID, ht.HouseName AS ToHouseName, t.CounterFlockID, cf.FlockNumber AS CounterFlockNumber, t.MaleCount, t.FemaleCount, t.Reason, " +
                "t.ApprovedBy, u.UserName AS CreatedByName, t.CreatedDate, COUNT(*) OVER () AS TotalCount " +
                "FROM dbo.tblFlockTransfer t INNER JOIN dbo.tblFlock f ON f.FlockID = t.FlockID LEFT JOIN dbo.tblHouse hf ON hf.HouseID = t.FromHouseID " +
                "LEFT JOIN dbo.tblHouse ht ON ht.HouseID = t.ToHouseID LEFT JOIN dbo.tblFlock cf ON cf.FlockID = t.CounterFlockID " +
                "LEFT JOIN dbo.tblUsers u ON u.UserID = t.CreatedBy " + Where +
                "AND (@FromDate IS NULL OR t.TransferDate >= @FromDate) AND (@ToDate IS NULL OR t.TransferDate <= @ToDate) " +
                "ORDER BY t.TransferDate DESC, t.FlockTransferID DESC OFFSET (@PageNo - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;",
                r =>
                {
                    total = r.Get<int>("TotalCount");
                    return new TransferRecord
                    {
                        FlockTransferID = r.Get<int>("FlockTransferID"),
                        FlockID = r.Get<int>("FlockID"),
                        FlockNumber = r.Str("FlockNumber"),
                        TransferDate = r.Get<DateTime>("TransferDate"),
                        WeekNo = r.Get<int?>("WeekNo"),
                        TransferType = r.Str("TransferType"),
                        FromHouseID = r.Get<int?>("FromHouseID"),
                        FromHouseName = r.Str("FromHouseName"),
                        ToHouseID = r.Get<int?>("ToHouseID"),
                        ToHouseName = r.Str("ToHouseName"),
                        CounterFlockID = r.Get<int?>("CounterFlockID"),
                        CounterFlockNumber = r.Str("CounterFlockNumber"),
                        MaleCount = r.Get<int>("MaleCount"),
                        FemaleCount = r.Get<int>("FemaleCount"),
                        Reason = r.Str("Reason"),
                        ApprovedBy = r.Get<int?>("ApprovedBy"),
                        CreatedByName = r.Str("CreatedByName"),
                        CreatedDate = r.Get<DateTime>("CreatedDate")
                    };
                }, Params(c, page, size));
            return new PagedResult<TransferRecord>(items, total, page, size);
        }

        public int SaveTransfer(TransferRecord t, IUserContext user)
        {
            var outId = Sql.OutInt("@NewTransferID");
            _db.ExecuteProc("dbo.sp_FlockTransfer_Save",
                Sql.P("@FlockID", t.FlockID), Sql.Date("@TransferDate", t.TransferDate), Sql.NVarChar("@TransferType", t.TransferType, 10),
                Sql.P("@ToHouseID", t.ToHouseID), Sql.P("@CounterFlockID", t.CounterFlockID), Sql.P("@MaleCount", t.MaleCount),
                Sql.P("@FemaleCount", t.FemaleCount), Sql.NVarChar("@Reason", t.Reason, 250), Sql.P("@ApprovedBy", t.ApprovedBy),
                Sql.P("@UserID", user.UserID), Sql.NVarChar("@UserName", user.UserName, 50), Sql.NVarChar("@IPAddress", user.IPAddress, 50), outId);
            return Sql.ToInt(outId.Value);
        }

        public PagedResult<AdjustmentRecord> SearchAdjustments(MovementSearchCriteria c)
        {
            c = c ?? new MovementSearchCriteria();
            int page = Math.Max(1, c.PageNo), size = c.PageSize < 1 || c.PageSize > 200 ? 25 : c.PageSize, total = 0;
            var items = _db.Query(
                "SELECT a.FlockAdjustmentID, a.FlockID, f.FlockNumber, a.WeekNo, a.AdjustmentDate, a.MaleAdjustment, a.FemaleAdjustment, a.Reason, " +
                "ap.UserName AS ApprovedByName, u.UserName AS CreatedByName, a.CreatedDate, COUNT(*) OVER () AS TotalCount " +
                "FROM dbo.tblFlockAdjustment a INNER JOIN dbo.tblFlock f ON f.FlockID = a.FlockID LEFT JOIN dbo.tblUsers ap ON ap.UserID = a.ApprovedBy " +
                "LEFT JOIN dbo.tblUsers u ON u.UserID = a.CreatedBy " + Where +
                "AND (@FromDate IS NULL OR a.AdjustmentDate >= @FromDate) AND (@ToDate IS NULL OR a.AdjustmentDate <= @ToDate) " +
                "ORDER BY a.AdjustmentDate DESC, a.FlockAdjustmentID DESC OFFSET (@PageNo - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;",
                r =>
                {
                    total = r.Get<int>("TotalCount");
                    return new AdjustmentRecord
                    {
                        FlockAdjustmentID = r.Get<int>("FlockAdjustmentID"),
                        FlockID = r.Get<int>("FlockID"),
                        FlockNumber = r.Str("FlockNumber"),
                        WeekNo = r.Get<int?>("WeekNo"),
                        AdjustmentDate = r.Get<DateTime>("AdjustmentDate"),
                        MaleAdjustment = r.Get<int>("MaleAdjustment"),
                        FemaleAdjustment = r.Get<int>("FemaleAdjustment"),
                        Reason = r.Str("Reason"),
                        ApprovedByName = r.Str("ApprovedByName"),
                        CreatedByName = r.Str("CreatedByName"),
                        CreatedDate = r.Get<DateTime>("CreatedDate")
                    };
                }, Params(c, page, size));
            return new PagedResult<AdjustmentRecord>(items, total, page, size);
        }

        public int SaveAdjustment(AdjustmentRecord a, IUserContext user)
        {
            var outId = Sql.OutInt("@NewAdjustmentID");
            _db.ExecuteProc("dbo.sp_FlockAdjustment_Save",
                Sql.P("@FlockID", a.FlockID), Sql.Date("@AdjustmentDate", a.AdjustmentDate), Sql.P("@MaleAdjustment", a.MaleAdjustment),
                Sql.P("@FemaleAdjustment", a.FemaleAdjustment), Sql.NVarChar("@Reason", a.Reason, 250), Sql.P("@ApprovedBy", (object)null),
                Sql.P("@UserID", user.UserID), Sql.NVarChar("@UserName", user.UserName, 50), Sql.NVarChar("@IPAddress", user.IPAddress, 50), outId);
            return Sql.ToInt(outId.Value);
        }
    }
}
