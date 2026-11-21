using System;
using System.Collections.Generic;
using System.Data;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Helpers;
using FlockManagement.Common.Interfaces;
using FlockManagement.Data.Infrastructure;
using FlockManagement.Data.Interfaces;

namespace FlockManagement.Data.Repositories
{
    /// <summary>Flock master - all writes go through stored procedures (transaction + audit inside SQL).</summary>
    public class FlockRepository : IFlockRepository
    {
        private readonly IDb _db;

        public FlockRepository(IDb db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        public PagedResult<Flock> Search(FlockSearchCriteria c)
        {
            c = c ?? new FlockSearchCriteria();
            var page = c.PageNo < 1 ? 1 : c.PageNo;
            var size = c.PageSize < 1 || c.PageSize > PageRequest.MaxPageSize ? 25 : c.PageSize;
            var total = 0;

            var items = _db.QueryProc("dbo.sp_Flock_Search", r =>
            {
                total = r.Get<int>("TotalCount");
                return Map(r);
            },
                Sql.NVarChar("@Search", string.IsNullOrWhiteSpace(c.Search) ? null : c.Search.Trim(), 50),
                Sql.P("@FarmID", c.FarmID),
                Sql.P("@HouseID", c.HouseID),
                Sql.P("@BirdTypeID", c.BirdTypeID),
                Sql.P("@LotID", c.LotID),
                Sql.NVarChar("@Status", string.IsNullOrWhiteSpace(c.Status) ? null : c.Status, 20),
                Sql.P("@RestrictFarmID", c.RestrictFarmID),
                Sql.P("@PageNo", page),
                Sql.P("@PageSize", size));

            return new PagedResult<Flock>(items, total, page, size);
        }

        public Flock GetById(int flockId)
        {
            return _db.QueryProcSingle("dbo.sp_Flock_Get", Map, Sql.P("@FlockID", flockId));
        }

        public int Create(Flock f, IUserContext user)
        {
            var outId = Sql.OutInt("@FlockID");
            _db.ExecuteProc("dbo.sp_Flock_Create",
                Sql.NVarChar("@FlockNumber", f.FlockNumber, 30),
                Sql.P("@LotID", f.LotID),
                Sql.P("@BirdTypeID", f.BirdTypeID),
                Sql.P("@BreedID", f.BreedID),
                Sql.P("@FarmID", f.FarmID),
                Sql.P("@HouseID", f.HouseID),
                Sql.Date("@PlacementDate", f.PlacementDate),
                Sql.Date("@HatchDate", f.HatchDate),
                Sql.P("@MaleCount", f.MaleCount),
                Sql.P("@FemaleCount", f.FemaleCount),
                Sql.NVarChar("@Source", f.Source, 100),
                Sql.Decimal("@ChickAvgWeight", f.ChickAvgWeight, 10, 2),
                Sql.NVarChar("@Remarks", f.Remarks, 500),
                Sql.P("@UserID", user.UserID),
                Sql.NVarChar("@UserName", user.UserName, 50),
                Sql.NVarChar("@IPAddress", user.IPAddress, 50),
                outId);
            return Sql.ToInt(outId.Value);
        }

        public void Update(Flock f, IUserContext user)
        {
            _db.ExecuteProc("dbo.sp_Flock_Update",
                Sql.P("@FlockID", f.FlockID),
                Sql.NVarChar("@FlockNumber", f.FlockNumber, 30),
                Sql.P("@LotID", f.LotID),
                Sql.P("@BirdTypeID", f.BirdTypeID),
                Sql.P("@BreedID", f.BreedID),
                Sql.P("@FarmID", f.FarmID),
                Sql.P("@HouseID", f.HouseID),
                Sql.Date("@PlacementDate", f.PlacementDate),
                Sql.Date("@HatchDate", f.HatchDate),
                Sql.P("@MaleCount", f.MaleCount),
                Sql.P("@FemaleCount", f.FemaleCount),
                Sql.NVarChar("@Source", f.Source, 100),
                Sql.NVarChar("@Remarks", f.Remarks, 500),
                Sql.RowVersion("@RowVer", f.RowVer),
                Sql.P("@UserID", user.UserID),
                Sql.NVarChar("@UserName", user.UserName, 50),
                Sql.NVarChar("@IPAddress", user.IPAddress, 50));
        }

        public void SetStatus(int flockId, string status, DateTime? depletionDate, IUserContext user)
        {
            _db.ExecuteProc("dbo.sp_Flock_SetStatus",
                Sql.P("@FlockID", flockId),
                Sql.NVarChar("@Status", status, 20),
                Sql.Date("@DepletionDate", depletionDate),
                Sql.P("@UserID", user.UserID),
                Sql.NVarChar("@UserName", user.UserName, 50),
                Sql.NVarChar("@IPAddress", user.IPAddress, 50));
        }

        public IList<LookupItem> GetActiveLookup(int? farmId, int? houseId, int? restrictFarmId)
        {
            return _db.Query(
                "SELECT f.FlockID AS Id, f.FlockNumber + N' - ' + l.LotNumber AS Text, f.HouseID AS ParentId " +
                "FROM dbo.tblFlock f INNER JOIN dbo.tblLot l ON l.LotID = f.LotID " +
                "WHERE f.Status = N'Active' AND (@FarmID IS NULL OR f.FarmID = @FarmID) AND (@HouseID IS NULL OR f.HouseID = @HouseID) " +
                "AND (@RestrictFarmID IS NULL OR f.FarmID = @RestrictFarmID) ORDER BY f.FlockNumber;",
                r => new LookupItem(r.Get<int>("Id"), r.Str("Text"), r.Get<int?>("ParentId")),
                Sql.P("@FarmID", farmId), Sql.P("@HouseID", houseId), Sql.P("@RestrictFarmID", restrictFarmId));
        }

        private static Flock Map(IDataRecord r)
        {
            return new Flock
            {
                FlockID = r.Get<int>("FlockID"),
                FlockNumber = r.Str("FlockNumber"),
                LotID = r.Get<int>("LotID"),
                LotNumber = r.Str("LotNumber"),
                BirdTypeID = r.Get<int>("BirdTypeID"),
                BirdTypeName = r.Str("BirdTypeName"),
                BreedID = r.Get<int>("BreedID"),
                BreedName = r.Str("BreedName"),
                FarmID = r.Get<int>("FarmID"),
                FarmName = r.Str("FarmName"),
                HouseID = r.Get<int>("HouseID"),
                HouseName = r.Str("HouseName"),
                PlacementDate = r.Get<DateTime>("PlacementDate"),
                HatchDate = r.Get<DateTime>("HatchDate"),
                InitialBirdCount = r.Get<int>("InitialBirdCount"),
                MaleCount = r.Get<int>("MaleCount"),
                FemaleCount = r.Get<int>("FemaleCount"),
                CurrentMaleCount = r.Get<int>("CurrentMaleCount"),
                CurrentFemaleCount = r.Get<int>("CurrentFemaleCount"),
                TotalBirdCount = r.Get<int>("TotalBirdCount"),
                Source = r.Str("Source"),
                CurrentAge = r.Get<int>("CurrentAge"),
                Status = r.Str("Status"),
                DepletionDate = r.Get<DateTime?>("DepletionDate"),
                Remarks = r.Str("Remarks"),
                CreatedBy = r.Get<int?>("CreatedBy"),
                CreatedByName = r.Str("CreatedByName"),
                CreatedDate = r.Get<DateTime>("CreatedDate"),
                ModifiedBy = r.Get<int?>("ModifiedBy"),
                ModifiedByName = r.Str("ModifiedByName"),
                ModifiedDate = r.Get<DateTime?>("ModifiedDate"),
                RowVer = r.Get<byte[]>("RowVer"),
                HasWeeklyData = r.Get<bool>("HasWeeklyData")
            };
        }
    }
}
