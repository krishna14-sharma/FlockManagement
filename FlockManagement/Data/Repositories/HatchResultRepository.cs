using System;
using System.Data;
using System.Linq;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Helpers;
using FlockManagement.Common.Interfaces;
using FlockManagement.Data.Infrastructure;
using FlockManagement.Data.Interfaces;

namespace FlockManagement.Data.Repositories
{
    /// <summary>Hatch results: one setting of eggs from one flock-week (fertility and hatchability).</summary>
    public class HatchResultRepository : IHatchResultRepository
    {
        private readonly IDb _db;

        public HatchResultRepository(IDb db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        private static HatchResult Map(IDataRecord r)
        {
            return new HatchResult
            {
                HatchResultID = r.Get<int>("HatchResultID"),
                FlockID = r.Get<int>("FlockID"),
                FlockNumber = r.Str("FlockNumber"),
                FarmName = r.Str("FarmName"),
                HouseName = r.Str("HouseName"),
                BirdTypeName = r.Str("BirdTypeName"),
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
                Remarks = r.Str("Remarks"),
                CreatedByName = r.Str("CreatedByName"),
                CreatedDate = r.Get<DateTime>("CreatedDate")
            };
        }

        public PagedResult<HatchResult> Search(HatchResultSearchCriteria c)
        {
            c = c ?? new HatchResultSearchCriteria();
            int page = Math.Max(1, c.PageNo), size = c.PageSize < 1 || c.PageSize > 200 ? 25 : c.PageSize, total = 0;
            var items = _db.QueryProc("dbo.sp_HatchResult_Search", r =>
            {
                total = r.Get<int>("TotalRows");
                return Map(r);
            },
                Sql.P("@FarmID", c.FarmID), Sql.P("@FlockID", c.FlockID), Sql.Date("@FromDate", c.FromDate), Sql.Date("@ToDate", c.ToDate),
                Sql.P("@RestrictFarmID", c.RestrictFarmID), Sql.P("@PageNumber", page), Sql.P("@PageSize", size));
            return new PagedResult<HatchResult>(items, total, page, size);
        }

        public HatchResult Get(int hatchResultId)
        {
            return _db.QuerySingle(
                "SELECT hr.HatchResultID, hr.FlockID, f.FlockNumber, fm.FarmName, h.HouseName, bt.BirdTypeName, hr.WeekNo, hr.SetDate, hr.HatchDate, " +
                "hr.SettableEggs, hr.InfertileEggs, hr.FertileEggs, hr.DeadInShell, hr.HatchedChicks, hr.SaleableChicks, hr.FertilityPct, " +
                "CASE WHEN hr.HatchDate IS NOT NULL THEN hr.HatchabilityPct END AS HatchabilityPct, " +
                "CASE WHEN hr.HatchDate IS NOT NULL THEN hr.HatchOfFertilePct END AS HatchOfFertilePct, hr.Remarks, " +
                "u.FullName AS CreatedByName, hr.CreatedDate " +
                "FROM dbo.tblHatchResult hr INNER JOIN dbo.tblFlock f ON f.FlockID = hr.FlockID INNER JOIN dbo.tblFarm fm ON fm.FarmID = f.FarmID " +
                "INNER JOIN dbo.tblHouse h ON h.HouseID = f.HouseID INNER JOIN dbo.tblBirdType bt ON bt.BirdTypeID = f.BirdTypeID " +
                "LEFT JOIN dbo.tblUsers u ON u.UserID = hr.CreatedBy WHERE hr.HatchResultID = @ID;",
                Map, Sql.P("@ID", hatchResultId));
        }

        public int Save(HatchResult h, IUserContext user)
        {
            var outId = Sql.OutInt("@NewHatchResultID");
            _db.ExecuteProc("dbo.sp_HatchResult_Save",
                Sql.P("@HatchResultID", h.HatchResultID), Sql.P("@FlockID", h.FlockID), Sql.P("@WeekNo", h.WeekNo),
                Sql.Date("@SetDate", h.SetDate), Sql.Date("@HatchDate", h.HatchDate), Sql.P("@SettableEggs", h.SettableEggs),
                Sql.P("@InfertileEggs", h.InfertileEggs), Sql.P("@DeadInShell", h.DeadInShell), Sql.P("@HatchedChicks", h.HatchedChicks),
                Sql.P("@SaleableChicks", h.SaleableChicks), Sql.NVarChar("@Remarks", h.Remarks, 250),
                Sql.P("@UserID", user.UserID), Sql.NVarChar("@UserName", user.UserName, 50), Sql.NVarChar("@IPAddress", user.IPAddress, 50), outId);
            return Sql.ToInt(outId.Value);
        }

        public void Delete(int hatchResultId, IUserContext user)
        {
            _db.ExecuteProc("dbo.sp_HatchResult_Delete", Sql.P("@HatchResultID", hatchResultId),
                Sql.P("@UserID", user.UserID), Sql.NVarChar("@UserName", user.UserName, 50), Sql.NVarChar("@IPAddress", user.IPAddress, 50));
        }
    }
}
