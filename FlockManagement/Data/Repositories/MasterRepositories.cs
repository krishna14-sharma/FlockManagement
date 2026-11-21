using System.Data;
using FlockManagement.Common.Entities;
using FlockManagement.Data.Infrastructure;
using FlockManagement.Data.Interfaces;

namespace FlockManagement.Data.Repositories
{
    internal static class AuditColumns
    {
        public static void Fill(AuditableEntity e, IDataRecord r)
        {
            e.IsActive = r.Get<bool>("IsActive");
            e.CreatedBy = r.Get<int?>("CreatedBy");
            e.CreatedDate = r.Get<System.DateTime>("CreatedDate");
            e.ModifiedBy = r.Get<int?>("ModifiedBy");
            e.ModifiedDate = r.Get<System.DateTime?>("ModifiedDate");
        }

        public static string For(string alias)
        {
            return alias + ".IsActive, " + alias + ".CreatedBy, " + alias + ".CreatedDate, " + alias + ".ModifiedBy, " + alias + ".ModifiedDate";
        }
    }

    // ------------------------------------------------------------------ FARM
    public class FarmRepository : MasterRepositoryBase<Farm>, IFarmRepository
    {
        public FarmRepository(IDb db) : base(db) { }

        protected override string TableName { get { return "dbo.tblFarm"; } }
        protected override string Alias { get { return "f"; } }
        protected override string KeyColumn { get { return "FarmID"; } }
        protected override string FromClause { get { return "dbo.tblFarm f"; } }
        protected override string[] SearchColumns { get { return new[] { "f.FarmCode", "f.FarmName", "f.Location" }; } }
        protected override string OrderBy { get { return "f.FarmName"; } }
        protected override string LookupTextExpression { get { return "f.FarmName"; } }
        protected override string FarmColumn { get { return "f.FarmID"; } }

        protected override string SelectColumns
        {
            get
            {
                return "f.FarmID, f.FarmCode, f.FarmName, f.Location, f.Address, f.ContactPerson, f.ContactNumber, " +
                       AuditColumns.For("f") +
                       ", (SELECT COUNT(*) FROM dbo.tblHouse h WHERE h.FarmID = f.FarmID AND h.IsActive = 1) AS HouseCount" +
                       ", (SELECT COUNT(*) FROM dbo.tblFlock fl WHERE fl.FarmID = f.FarmID AND fl.Status = N'Active') AS ActiveFlockCount";
            }
        }

        protected override Farm Map(IDataRecord r)
        {
            var e = new Farm
            {
                FarmID = r.Get<int>("FarmID"),
                FarmCode = r.Str("FarmCode"),
                FarmName = r.Str("FarmName"),
                Location = r.Str("Location"),
                Address = r.Str("Address"),
                ContactPerson = r.Str("ContactPerson"),
                ContactNumber = r.Str("ContactNumber"),
                HouseCount = r.Get<int>("HouseCount"),
                ActiveFlockCount = r.Get<int>("ActiveFlockCount")
            };
            AuditColumns.Fill(e, r);
            return e;
        }

        protected override int GetKey(Farm e) { return e.FarmID; }

        protected override int Insert(IDbExecutor db, Farm e, int userId)
        {
            return InsertAndGetId(db,
                "INSERT INTO dbo.tblFarm (FarmCode, FarmName, Location, Address, ContactPerson, ContactNumber, IsActive, CreatedBy) " +
                "VALUES (@FarmCode, @FarmName, @Location, @Address, @ContactPerson, @ContactNumber, @IsActive, @UserID);",
                Sql.NVarChar("@FarmCode", e.FarmCode, 20), Sql.NVarChar("@FarmName", e.FarmName, 100),
                Sql.NVarChar("@Location", e.Location, 100), Sql.NVarChar("@Address", e.Address, 300),
                Sql.NVarChar("@ContactPerson", e.ContactPerson, 100), Sql.NVarChar("@ContactNumber", e.ContactNumber, 30),
                Sql.P("@IsActive", e.IsActive), Sql.P("@UserID", userId));
        }

        protected override void Update(IDbExecutor db, Farm e, int userId)
        {
            db.Execute(
                "UPDATE dbo.tblFarm SET FarmCode = @FarmCode, FarmName = @FarmName, Location = @Location, Address = @Address, " +
                "ContactPerson = @ContactPerson, ContactNumber = @ContactNumber, IsActive = @IsActive, ModifiedBy = @UserID, ModifiedDate = SYSDATETIME() " +
                "WHERE FarmID = @FarmID;",
                Sql.NVarChar("@FarmCode", e.FarmCode, 20), Sql.NVarChar("@FarmName", e.FarmName, 100),
                Sql.NVarChar("@Location", e.Location, 100), Sql.NVarChar("@Address", e.Address, 300),
                Sql.NVarChar("@ContactPerson", e.ContactPerson, 100), Sql.NVarChar("@ContactNumber", e.ContactNumber, 30),
                Sql.P("@IsActive", e.IsActive), Sql.P("@UserID", userId), Sql.P("@FarmID", e.FarmID));
        }
    }

    // ------------------------------------------------------------------ HOUSE
    public class HouseRepository : MasterRepositoryBase<House>, IHouseRepository
    {
        public HouseRepository(IDb db) : base(db) { }

        protected override string TableName { get { return "dbo.tblHouse"; } }
        protected override string Alias { get { return "h"; } }
        protected override string KeyColumn { get { return "HouseID"; } }
        protected override string FromClause { get { return "dbo.tblHouse h INNER JOIN dbo.tblFarm f ON f.FarmID = h.FarmID"; } }
        protected override string[] SearchColumns { get { return new[] { "h.HouseCode", "h.HouseName", "f.FarmName" }; } }
        protected override string OrderBy { get { return "f.FarmName, h.HouseCode"; } }
        protected override string LookupTextExpression { get { return "h.HouseName + N' (' + h.HouseCode + N')'"; } }
        protected override string ParentColumn { get { return "h.FarmID"; } }
        protected override string FarmColumn { get { return "h.FarmID"; } }

        protected override string SelectColumns
        {
            get
            {
                return "h.HouseID, h.FarmID, f.FarmName, h.HouseCode, h.HouseName, h.HouseType, h.Capacity, h.AreaSqFt, " +
                       AuditColumns.For("h") +
                       ", ISNULL((SELECT SUM(fl.TotalBirdCount) FROM dbo.tblFlock fl WHERE fl.HouseID = h.HouseID AND fl.Status = N'Active'), 0) AS CurrentBirds";
            }
        }

        protected override House Map(IDataRecord r)
        {
            var e = new House
            {
                HouseID = r.Get<int>("HouseID"),
                FarmID = r.Get<int>("FarmID"),
                FarmName = r.Str("FarmName"),
                HouseCode = r.Str("HouseCode"),
                HouseName = r.Str("HouseName"),
                HouseType = r.Str("HouseType"),
                Capacity = r.Get<int>("Capacity"),
                AreaSqFt = r.Get<decimal?>("AreaSqFt"),
                CurrentBirds = r.Get<int>("CurrentBirds")
            };
            AuditColumns.Fill(e, r);
            return e;
        }

        protected override int GetKey(House e) { return e.HouseID; }

        protected override int Insert(IDbExecutor db, House e, int userId)
        {
            return InsertAndGetId(db,
                "INSERT INTO dbo.tblHouse (FarmID, HouseCode, HouseName, HouseType, Capacity, AreaSqFt, IsActive, CreatedBy) " +
                "VALUES (@FarmID, @HouseCode, @HouseName, @HouseType, @Capacity, @AreaSqFt, @IsActive, @UserID);",
                Sql.P("@FarmID", e.FarmID), Sql.NVarChar("@HouseCode", e.HouseCode, 20), Sql.NVarChar("@HouseName", e.HouseName, 100),
                Sql.NVarChar("@HouseType", e.HouseType, 50), Sql.P("@Capacity", e.Capacity), Sql.Decimal("@AreaSqFt", e.AreaSqFt, 12, 2),
                Sql.P("@IsActive", e.IsActive), Sql.P("@UserID", userId));
        }

        protected override void Update(IDbExecutor db, House e, int userId)
        {
            db.Execute(
                "UPDATE dbo.tblHouse SET FarmID = @FarmID, HouseCode = @HouseCode, HouseName = @HouseName, HouseType = @HouseType, " +
                "Capacity = @Capacity, AreaSqFt = @AreaSqFt, IsActive = @IsActive, ModifiedBy = @UserID, ModifiedDate = SYSDATETIME() WHERE HouseID = @HouseID;",
                Sql.P("@FarmID", e.FarmID), Sql.NVarChar("@HouseCode", e.HouseCode, 20), Sql.NVarChar("@HouseName", e.HouseName, 100),
                Sql.NVarChar("@HouseType", e.HouseType, 50), Sql.P("@Capacity", e.Capacity), Sql.Decimal("@AreaSqFt", e.AreaSqFt, 12, 2),
                Sql.P("@IsActive", e.IsActive), Sql.P("@UserID", userId), Sql.P("@HouseID", e.HouseID));
        }

        public int GetCurrentBirds(int houseId)
        {
            return Sql.ToInt(Db.Scalar(
                "SELECT ISNULL(SUM(TotalBirdCount), 0) FROM dbo.tblFlock WHERE HouseID = @HouseID AND Status = N'Active';",
                Sql.P("@HouseID", houseId)));
        }
    }

    // ------------------------------------------------------------------ BIRD TYPE
    public class BirdTypeRepository : MasterRepositoryBase<BirdType>, IBirdTypeRepository
    {
        public BirdTypeRepository(IDb db) : base(db) { }

        protected override string TableName { get { return "dbo.tblBirdType"; } }
        protected override string Alias { get { return "b"; } }
        protected override string KeyColumn { get { return "BirdTypeID"; } }
        protected override string FromClause { get { return "dbo.tblBirdType b"; } }
        protected override string[] SearchColumns { get { return new[] { "b.BirdTypeCode", "b.BirdTypeName" }; } }
        protected override string OrderBy { get { return "b.BirdTypeName"; } }
        protected override string LookupTextExpression { get { return "b.BirdTypeName"; } }
        protected override string SelectColumns
        {
            get { return "b.BirdTypeID, b.BirdTypeCode, b.BirdTypeName, b.Description, " + AuditColumns.For("b"); }
        }

        protected override BirdType Map(IDataRecord r)
        {
            var e = new BirdType
            {
                BirdTypeID = r.Get<int>("BirdTypeID"),
                BirdTypeCode = r.Str("BirdTypeCode"),
                BirdTypeName = r.Str("BirdTypeName"),
                Description = r.Str("Description")
            };
            AuditColumns.Fill(e, r);
            return e;
        }

        protected override int GetKey(BirdType e) { return e.BirdTypeID; }

        protected override int Insert(IDbExecutor db, BirdType e, int userId)
        {
            return InsertAndGetId(db,
                "INSERT INTO dbo.tblBirdType (BirdTypeCode, BirdTypeName, Description, IsActive, CreatedBy) VALUES (@Code, @Name, @Description, @IsActive, @UserID);",
                Sql.NVarChar("@Code", e.BirdTypeCode, 20), Sql.NVarChar("@Name", e.BirdTypeName, 60),
                Sql.NVarChar("@Description", e.Description, 250), Sql.P("@IsActive", e.IsActive), Sql.P("@UserID", userId));
        }

        protected override void Update(IDbExecutor db, BirdType e, int userId)
        {
            db.Execute(
                "UPDATE dbo.tblBirdType SET BirdTypeCode = @Code, BirdTypeName = @Name, Description = @Description, IsActive = @IsActive, " +
                "ModifiedBy = @UserID, ModifiedDate = SYSDATETIME() WHERE BirdTypeID = @ID;",
                Sql.NVarChar("@Code", e.BirdTypeCode, 20), Sql.NVarChar("@Name", e.BirdTypeName, 60),
                Sql.NVarChar("@Description", e.Description, 250), Sql.P("@IsActive", e.IsActive), Sql.P("@UserID", userId),
                Sql.P("@ID", e.BirdTypeID));
        }
    }

    // ------------------------------------------------------------------ BREED
    public class BreedRepository : MasterRepositoryBase<Breed>, IBreedRepository
    {
        public BreedRepository(IDb db) : base(db) { }

        protected override string TableName { get { return "dbo.tblBreed"; } }
        protected override string Alias { get { return "b"; } }
        protected override string KeyColumn { get { return "BreedID"; } }
        protected override string FromClause { get { return "dbo.tblBreed b"; } }
        protected override string[] SearchColumns { get { return new[] { "b.BreedCode", "b.BreedName", "b.Supplier" }; } }
        protected override string OrderBy { get { return "b.BreedName"; } }
        protected override string LookupTextExpression { get { return "b.BreedName"; } }
        protected override string SelectColumns
        {
            get { return "b.BreedID, b.BreedCode, b.BreedName, b.Supplier, b.Description, " + AuditColumns.For("b"); }
        }

        protected override Breed Map(IDataRecord r)
        {
            var e = new Breed
            {
                BreedID = r.Get<int>("BreedID"),
                BreedCode = r.Str("BreedCode"),
                BreedName = r.Str("BreedName"),
                Supplier = r.Str("Supplier"),
                Description = r.Str("Description")
            };
            AuditColumns.Fill(e, r);
            return e;
        }

        protected override int GetKey(Breed e) { return e.BreedID; }

        protected override int Insert(IDbExecutor db, Breed e, int userId)
        {
            return InsertAndGetId(db,
                "INSERT INTO dbo.tblBreed (BreedCode, BreedName, Supplier, Description, IsActive, CreatedBy) VALUES (@Code, @Name, @Supplier, @Description, @IsActive, @UserID);",
                Sql.NVarChar("@Code", e.BreedCode, 20), Sql.NVarChar("@Name", e.BreedName, 100), Sql.NVarChar("@Supplier", e.Supplier, 100),
                Sql.NVarChar("@Description", e.Description, 250), Sql.P("@IsActive", e.IsActive), Sql.P("@UserID", userId));
        }

        protected override void Update(IDbExecutor db, Breed e, int userId)
        {
            db.Execute(
                "UPDATE dbo.tblBreed SET BreedCode = @Code, BreedName = @Name, Supplier = @Supplier, Description = @Description, IsActive = @IsActive, " +
                "ModifiedBy = @UserID, ModifiedDate = SYSDATETIME() WHERE BreedID = @ID;",
                Sql.NVarChar("@Code", e.BreedCode, 20), Sql.NVarChar("@Name", e.BreedName, 100), Sql.NVarChar("@Supplier", e.Supplier, 100),
                Sql.NVarChar("@Description", e.Description, 250), Sql.P("@IsActive", e.IsActive), Sql.P("@UserID", userId), Sql.P("@ID", e.BreedID));
        }
    }

    // ------------------------------------------------------------------ LOT
    public class LotRepository : MasterRepositoryBase<Lot>, ILotRepository
    {
        public LotRepository(IDb db) : base(db) { }

        protected override string TableName { get { return "dbo.tblLot"; } }
        protected override string Alias { get { return "l"; } }
        protected override string KeyColumn { get { return "LotID"; } }
        protected override string FromClause { get { return "dbo.tblLot l LEFT JOIN dbo.tblFarm f ON f.FarmID = l.FarmID"; } }
        protected override string[] SearchColumns { get { return new[] { "l.LotNumber", "l.LotName" }; } }
        protected override string OrderBy { get { return "l.LotNumber DESC"; } }
        protected override string LookupTextExpression { get { return "l.LotNumber"; } }
        protected override string ParentColumn { get { return "l.FarmID"; } }
        protected override string FarmColumn { get { return "l.FarmID"; } }
        protected override string SelectColumns
        {
            get { return "l.LotID, l.LotNumber, l.LotName, l.FarmID, f.FarmName, l.Description, " + AuditColumns.For("l"); }
        }

        protected override Lot Map(IDataRecord r)
        {
            var e = new Lot
            {
                LotID = r.Get<int>("LotID"),
                LotNumber = r.Str("LotNumber"),
                LotName = r.Str("LotName"),
                FarmID = r.Get<int?>("FarmID"),
                FarmName = r.Str("FarmName"),
                Description = r.Str("Description")
            };
            AuditColumns.Fill(e, r);
            return e;
        }

        protected override int GetKey(Lot e) { return e.LotID; }

        protected override int Insert(IDbExecutor db, Lot e, int userId)
        {
            return InsertAndGetId(db,
                "INSERT INTO dbo.tblLot (LotNumber, LotName, FarmID, Description, IsActive, CreatedBy) VALUES (@LotNumber, @LotName, @FarmID, @Description, @IsActive, @UserID);",
                Sql.NVarChar("@LotNumber", e.LotNumber, 30), Sql.NVarChar("@LotName", e.LotName, 100), Sql.P("@FarmID", e.FarmID),
                Sql.NVarChar("@Description", e.Description, 250), Sql.P("@IsActive", e.IsActive), Sql.P("@UserID", userId));
        }

        protected override void Update(IDbExecutor db, Lot e, int userId)
        {
            db.Execute(
                "UPDATE dbo.tblLot SET LotNumber = @LotNumber, LotName = @LotName, FarmID = @FarmID, Description = @Description, IsActive = @IsActive, " +
                "ModifiedBy = @UserID, ModifiedDate = SYSDATETIME() WHERE LotID = @ID;",
                Sql.NVarChar("@LotNumber", e.LotNumber, 30), Sql.NVarChar("@LotName", e.LotName, 100), Sql.P("@FarmID", e.FarmID),
                Sql.NVarChar("@Description", e.Description, 250), Sql.P("@IsActive", e.IsActive), Sql.P("@UserID", userId), Sql.P("@ID", e.LotID));
        }
    }

    // ------------------------------------------------------------------ FEED TYPE
    public class FeedTypeRepository : MasterRepositoryBase<FeedType>, IFeedTypeRepository
    {
        public FeedTypeRepository(IDb db) : base(db) { }

        protected override string TableName { get { return "dbo.tblFeedType"; } }
        protected override string Alias { get { return "ft"; } }
        protected override string KeyColumn { get { return "FeedTypeID"; } }
        protected override string FromClause { get { return "dbo.tblFeedType ft"; } }
        protected override string[] SearchColumns { get { return new[] { "ft.FeedTypeCode", "ft.FeedTypeName" }; } }
        protected override string OrderBy { get { return "ISNULL(ft.ApplicableFromWeek, 999), ft.FeedTypeName"; } }
        protected override string LookupTextExpression { get { return "ft.FeedTypeName"; } }
        protected override string SelectColumns
        {
            get
            {
                return "ft.FeedTypeID, ft.FeedTypeCode, ft.FeedTypeName, ft.TargetSex, ft.ApplicableFromWeek, ft.ApplicableToWeek, ft.Description, " +
                       AuditColumns.For("ft");
            }
        }

        protected override FeedType Map(IDataRecord r)
        {
            var e = new FeedType
            {
                FeedTypeID = r.Get<int>("FeedTypeID"),
                FeedTypeCode = r.Str("FeedTypeCode"),
                FeedTypeName = r.Str("FeedTypeName"),
                TargetSex = r.Str("TargetSex"),
                ApplicableFromWeek = r.Get<int?>("ApplicableFromWeek"),
                ApplicableToWeek = r.Get<int?>("ApplicableToWeek"),
                Description = r.Str("Description")
            };
            AuditColumns.Fill(e, r);
            return e;
        }

        protected override int GetKey(FeedType e) { return e.FeedTypeID; }

        protected override int Insert(IDbExecutor db, FeedType e, int userId)
        {
            return InsertAndGetId(db,
                "INSERT INTO dbo.tblFeedType (FeedTypeCode, FeedTypeName, TargetSex, ApplicableFromWeek, ApplicableToWeek, Description, IsActive, CreatedBy) " +
                "VALUES (@Code, @Name, @Sex, @From, @To, @Description, @IsActive, @UserID);",
                Sql.NVarChar("@Code", e.FeedTypeCode, 20), Sql.NVarChar("@Name", e.FeedTypeName, 100), Sql.P("@Sex", e.TargetSex ?? "B"),
                Sql.P("@From", e.ApplicableFromWeek), Sql.P("@To", e.ApplicableToWeek), Sql.NVarChar("@Description", e.Description, 250),
                Sql.P("@IsActive", e.IsActive), Sql.P("@UserID", userId));
        }

        protected override void Update(IDbExecutor db, FeedType e, int userId)
        {
            db.Execute(
                "UPDATE dbo.tblFeedType SET FeedTypeCode = @Code, FeedTypeName = @Name, TargetSex = @Sex, ApplicableFromWeek = @From, ApplicableToWeek = @To, " +
                "Description = @Description, IsActive = @IsActive, ModifiedBy = @UserID, ModifiedDate = SYSDATETIME() WHERE FeedTypeID = @ID;",
                Sql.NVarChar("@Code", e.FeedTypeCode, 20), Sql.NVarChar("@Name", e.FeedTypeName, 100), Sql.P("@Sex", e.TargetSex ?? "B"),
                Sql.P("@From", e.ApplicableFromWeek), Sql.P("@To", e.ApplicableToWeek), Sql.NVarChar("@Description", e.Description, 250),
                Sql.P("@IsActive", e.IsActive), Sql.P("@UserID", userId), Sql.P("@ID", e.FeedTypeID));
        }
    }
}
