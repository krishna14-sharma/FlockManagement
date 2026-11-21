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
    public class UserRepository : IUserRepository
    {
        private readonly IDb _db;

        public UserRepository(IDb db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        public UserCredential GetForLogin(string userName)
        {
            return _db.QueryProcSingle("dbo.sp_User_GetForLogin", r => new UserCredential
            {
                UserID = r.Get<int>("UserID"),
                UserName = r.Str("UserName"),
                FullName = r.Str("FullName"),
                Email = r.Str("Email"),
                PasswordHash = r.Str("PasswordHash"),
                RoleID = r.Get<int>("RoleID"),
                RoleName = r.Str("RoleName"),
                FarmID = r.Get<int?>("FarmID"),
                IsActive = r.Get<bool>("IsActive"),
                IsLocked = r.Get<bool>("IsLocked"),
                RoleIsActive = r.Get<bool>("RoleIsActive"),
                FailedLoginCount = r.Get<int>("FailedLoginCount"),
                MustChangePassword = r.Get<bool>("MustChangePassword")
            }, Sql.NVarChar("@UserName", userName, 50));
        }

        public bool RecordLogin(int? userId, string userName, bool success, string ipAddress, int maxFailedAttempts, string remarks)
        {
            var rows = _db.QueryProc("dbo.sp_User_RecordLogin", r => r.Get<bool>("IsLocked"),
                Sql.P("@UserID", userId),
                Sql.NVarChar("@UserName", string.IsNullOrEmpty(userName) ? "(blank)" : (userName.Length > 50 ? userName.Substring(0, 50) : userName), 50),
                Sql.P("@IsSuccess", success),
                Sql.NVarChar("@IPAddress", ipAddress, 50),
                Sql.P("@MaxFailedAttempts", maxFailedAttempts),
                Sql.NVarChar("@Remarks", remarks, 200));
            return rows.Count > 0 && rows[0];
        }

        public string GetPasswordHash(int userId)
        {
            return _db.Scalar("SELECT PasswordHash FROM dbo.tblUsers WHERE UserID = @UserID;", Sql.P("@UserID", userId)) as string;
        }

        public void ChangePassword(int userId, string passwordHash, bool mustChange, IUserContext user)
        {
            _db.ExecuteProc("dbo.sp_User_ChangePassword",
                Sql.P("@UserID", userId),
                Sql.NVarChar("@PasswordHash", passwordHash, 256),
                Sql.P("@ChangedBy", user.UserID),
                Sql.NVarChar("@ChangedByName", user.UserName, 50),
                Sql.NVarChar("@IPAddress", user.IPAddress, 50),
                Sql.P("@MustChange", mustChange));
        }

        public PagedResult<User> Search(string search, int? roleId, int pageNo, int pageSize)
        {
            var total = 0;
            var items = _db.QueryProc("dbo.sp_User_Search", r =>
            {
                total = r.Get<int>("TotalCount");
                return Map(r);
            },
                Sql.NVarChar("@Search", string.IsNullOrWhiteSpace(search) ? null : search.Trim(), 100),
                Sql.P("@RoleID", roleId), Sql.P("@PageNo", pageNo), Sql.P("@PageSize", pageSize));
            return new PagedResult<User>(items, total, pageNo, pageSize);
        }

        public User GetById(int userId)
        {
            return _db.QueryProcSingle("dbo.sp_User_Get", Map, Sql.P("@UserID", userId));
        }

        public int Save(User u, string passwordHash, IUserContext actionUser)
        {
            var outId = Sql.OutInt("@NewUserID");
            _db.ExecuteProc("dbo.sp_User_Save",
                Sql.P("@UserID", u.UserID),
                Sql.NVarChar("@UserName", u.UserName, 50),
                Sql.NVarChar("@FullName", u.FullName, 100),
                Sql.NVarChar("@Email", u.Email, 150),
                Sql.NVarChar("@MobileNumber", u.MobileNumber, 20),
                Sql.P("@RoleID", u.RoleID),
                Sql.P("@FarmID", u.FarmID),
                Sql.P("@IsActive", u.IsActive),
                Sql.P("@IsLocked", u.IsLocked),
                Sql.NVarChar("@PasswordHash", passwordHash, 256),
                Sql.P("@ActionUserID", actionUser.UserID),
                Sql.NVarChar("@ActionUserName", actionUser.UserName, 50),
                Sql.NVarChar("@IPAddress", actionUser.IPAddress, 50),
                outId);
            return Sql.ToInt(outId.Value);
        }

        private static User Map(IDataRecord r)
        {
            return new User
            {
                UserID = r.Get<int>("UserID"),
                UserName = r.Str("UserName"),
                FullName = r.Str("FullName"),
                Email = r.Str("Email"),
                MobileNumber = r.Str("MobileNumber"),
                RoleID = r.Get<int>("RoleID"),
                RoleName = r.Str("RoleName"),
                FarmID = r.Get<int?>("FarmID"),
                FarmName = r.Str("FarmName"),
                IsActive = r.Get<bool>("IsActive"),
                IsLocked = r.Get<bool>("IsLocked"),
                LastLoginDate = r.Get<DateTime?>("LastLoginDate"),
                MustChangePassword = r.Get<bool>("MustChangePassword")
            };
        }
    }

    public class RoleRepository : IRoleRepository
    {
        private readonly IDb _db;

        private const string RoleSelect =
            "SELECT r.RoleID, r.RoleName, r.Description, r.IsSystem, r.IsActive, " +
            "(SELECT COUNT(*) FROM dbo.tblUsers u WHERE u.RoleID = r.RoleID) AS UserCount, " +
            "(SELECT COUNT(*) FROM dbo.tblRolePermissions rp WHERE rp.RoleID = r.RoleID) AS PermissionCount " +
            "FROM dbo.tblRoles r ";

        public RoleRepository(IDb db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        public IList<Role> GetAll()
        {
            return _db.Query(RoleSelect + "ORDER BY r.RoleID;", Map);
        }

        public Role GetById(int roleId)
        {
            return _db.QuerySingle(RoleSelect + "WHERE r.RoleID = @RoleID;", Map, Sql.P("@RoleID", roleId));
        }

        public int Save(Role role, IUserContext user)
        {
            return _db.InTransaction(db =>
            {
                string oldJson = null;
                var id = role.RoleID;
                if (id > 0)
                {
                    var existing = db.QuerySingle(RoleSelect + "WHERE r.RoleID = @RoleID;", Map, Sql.P("@RoleID", id));
                    if (existing == null) throw new BusinessException("Role not found.");
                    if (existing.IsSystem && !string.Equals(existing.RoleName, role.RoleName, StringComparison.Ordinal))
                        throw new BusinessException("RoleName", "System roles cannot be renamed.");
                    if (existing.IsSystem && !role.IsActive)
                        throw new BusinessException("IsActive", "System roles cannot be deactivated.");
                    oldJson = AuditWriter.ToJson(existing);

                    db.Execute("UPDATE dbo.tblRoles SET RoleName = @Name, Description = @Description, IsActive = @IsActive, " +
                               "ModifiedBy = @UserID, ModifiedDate = SYSDATETIME() WHERE RoleID = @RoleID;",
                        Sql.NVarChar("@Name", role.RoleName, 50), Sql.NVarChar("@Description", role.Description, 250),
                        Sql.P("@IsActive", role.IsActive), Sql.P("@UserID", user.UserID), Sql.P("@RoleID", id));
                }
                else
                {
                    id = Sql.ToInt(db.Scalar(
                        "INSERT INTO dbo.tblRoles (RoleName, Description, IsSystem, IsActive, CreatedBy) VALUES (@Name, @Description, 0, @IsActive, @UserID); " +
                        "SELECT CAST(SCOPE_IDENTITY() AS INT);",
                        Sql.NVarChar("@Name", role.RoleName, 50), Sql.NVarChar("@Description", role.Description, 250),
                        Sql.P("@IsActive", role.IsActive), Sql.P("@UserID", user.UserID)));
                }

                var current = db.QuerySingle(RoleSelect + "WHERE r.RoleID = @RoleID;", Map, Sql.P("@RoleID", id));
                AuditWriter.Write(db, user, oldJson == null ? AuditActions.Insert : AuditActions.Update, "tblRoles", id, oldJson, AuditWriter.ToJson(current));
                return id;
            });
        }

        public IList<PermissionGrant> GetPermissionMatrix(int roleId)
        {
            return _db.QueryProc("dbo.sp_Permission_GetMatrix", r => new PermissionGrant
            {
                PermissionID = r.Get<int>("PermissionID"),
                PermissionCode = r.Str("PermissionCode"),
                PermissionName = r.Str("PermissionName"),
                MenuGroup = r.Str("MenuGroup"),
                SortOrder = r.Get<int>("SortOrder"),
                IsGranted = r.Get<bool>("IsGranted")
            }, Sql.P("@RoleID", roleId));
        }

        public void SavePermissions(int roleId, IEnumerable<int> permissionIds, IUserContext user)
        {
            var csv = string.Join(",", (permissionIds ?? Enumerable.Empty<int>()).Distinct());
            _db.ExecuteProc("dbo.sp_Role_SavePermissions",
                Sql.P("@RoleID", roleId),
                Sql.P("@PermissionIDs", csv),
                Sql.P("@ActionUserID", user.UserID),
                Sql.NVarChar("@ActionUserName", user.UserName, 50),
                Sql.NVarChar("@IPAddress", user.IPAddress, 50));
        }

        public IList<string> GetPermissionCodes(int roleId)
        {
            return _db.QueryProc("dbo.sp_Permission_GetCodesByRole", r => r.Str("PermissionCode"), Sql.P("@RoleID", roleId));
        }

        public IList<MenuItem> GetMenu(int roleId)
        {
            return _db.QueryProc("dbo.sp_Menu_GetByRole", r => new MenuItem
            {
                PermissionID = r.Get<int>("PermissionID"),
                PermissionCode = r.Str("PermissionCode"),
                MenuGroup = r.Str("MenuGroup"),
                MenuText = r.Str("MenuText"),
                ControllerName = r.Str("ControllerName"),
                ActionName = r.Str("ActionName"),
                IconClass = r.Str("IconClass"),
                SortOrder = r.Get<int>("SortOrder")
            }, Sql.P("@RoleID", roleId));
        }

        public IList<LookupItem> GetLookup()
        {
            return _db.Query("SELECT RoleID AS Id, RoleName AS Text FROM dbo.tblRoles WHERE IsActive = 1 ORDER BY RoleID;",
                r => new LookupItem(r.Get<int>("Id"), r.Str("Text")));
        }

        private static Role Map(IDataRecord r)
        {
            return new Role
            {
                RoleID = r.Get<int>("RoleID"),
                RoleName = r.Str("RoleName"),
                Description = r.Str("Description"),
                IsSystem = r.Get<bool>("IsSystem"),
                IsActive = r.Get<bool>("IsActive"),
                UserCount = r.Get<int>("UserCount"),
                PermissionCount = r.Get<int>("PermissionCount")
            };
        }
    }

    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly IDb _db;

        public AuditLogRepository(IDb db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        public PagedResult<AuditLogEntry> Search(AuditSearchCriteria c)
        {
            c = c ?? new AuditSearchCriteria();
            var page = c.PageNo < 1 ? 1 : c.PageNo;
            var size = c.PageSize < 1 || c.PageSize > PageRequest.MaxPageSize ? 25 : c.PageSize;
            var total = 0;
            var items = _db.QueryProc("dbo.sp_AuditLog_Search", r =>
            {
                total = r.Get<int>("TotalCount");
                return new AuditLogEntry
                {
                    AuditLogID = r.Get<long>("AuditLogID"),
                    UserID = r.Get<int?>("UserID"),
                    UserName = r.Str("UserName"),
                    ActionDate = r.Get<DateTime>("ActionDate"),
                    ActionType = r.Str("ActionType"),
                    TableName = r.Str("TableName"),
                    RecordID = r.Str("RecordID"),
                    OldValue = r.Str("OldValue"),
                    NewValue = r.Str("NewValue"),
                    IPAddress = r.Str("IPAddress")
                };
            },
                Sql.Date("@FromDate", c.FromDate), Sql.Date("@ToDate", c.ToDate),
                Sql.NVarChar("@TableName", string.IsNullOrWhiteSpace(c.TableName) ? null : c.TableName, 60),
                Sql.NVarChar("@UserName", string.IsNullOrWhiteSpace(c.UserName) ? null : c.UserName.Trim(), 50),
                Sql.P("@PageNo", page), Sql.P("@PageSize", size));
            return new PagedResult<AuditLogEntry>(items, total, page, size);
        }

        public IList<string> GetTableNames()
        {
            return _db.Query("SELECT DISTINCT TableName FROM dbo.tblAuditLog ORDER BY TableName;", r => r.Str("TableName"));
        }

        public void Write(IUserContext user, string action, string table, object recordId, string oldJson, string newJson)
        {
            AuditWriter.Write(_db, user, action, table, recordId, oldJson, newJson);
        }
    }

    public class ConfigRepository : IConfigRepository
    {
        private readonly IDb _db;

        public ConfigRepository(IDb db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        public IDictionary<string, string> GetSettings()
        {
            var rows = _db.Query("SELECT SettingKey, SettingValue FROM dbo.tblSystemSetting;",
                r => new KeyValuePair<string, string>(r.Str("SettingKey"), r.Str("SettingValue")));
            return rows.ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
        }

        public IList<StatusDefinition> GetStatuses()
        {
            return _db.Query("SELECT StatusCode, StatusName, StatusGroup, BadgeCssClass, ColorHex, SortOrder FROM dbo.tblStatusConfig ORDER BY SortOrder;",
                r => new StatusDefinition
                {
                    StatusCode = r.Str("StatusCode"),
                    StatusName = r.Str("StatusName"),
                    StatusGroup = r.Str("StatusGroup"),
                    BadgeCssClass = r.Str("BadgeCssClass"),
                    ColorHex = r.Str("ColorHex"),
                    SortOrder = r.Get<int>("SortOrder")
                });
        }

        public DashboardSummary GetDashboardSummary(int? restrictFarmId)
        {
            const string sql =
                "SELECT " +
                " (SELECT COUNT(*) FROM dbo.tblFlock f WHERE f.Status = N'Active' AND (@R IS NULL OR f.FarmID = @R)) AS ActiveFlocks," +
                " (SELECT ISNULL(SUM(f.TotalBirdCount),0) FROM dbo.tblFlock f WHERE f.Status = N'Active' AND (@R IS NULL OR f.FarmID = @R)) AS TotalBirds," +
                " (SELECT ISNULL(SUM(f.CurrentFemaleCount),0) FROM dbo.tblFlock f WHERE f.Status = N'Active' AND (@R IS NULL OR f.FarmID = @R)) AS FemaleBirds," +
                " (SELECT ISNULL(SUM(f.CurrentMaleCount),0) FROM dbo.tblFlock f WHERE f.Status = N'Active' AND (@R IS NULL OR f.FarmID = @R)) AS MaleBirds," +
                " (SELECT COUNT(*) FROM dbo.tblFarm fm WHERE fm.IsActive = 1 AND (@R IS NULL OR fm.FarmID = @R)) AS Farms," +
                " (SELECT COUNT(*) FROM dbo.tblHouse h WHERE h.IsActive = 1 AND (@R IS NULL OR h.FarmID = @R)) AS Houses," +
                " (SELECT ISNULL(SUM(h.Capacity),0) FROM dbo.tblHouse h WHERE h.IsActive = 1 AND (@R IS NULL OR h.FarmID = @R)) AS Capacity," +
                " (SELECT ISNULL(AVG(f.CurrentAge),0) FROM dbo.tblFlock f WHERE f.Status = N'Active' AND (@R IS NULL OR f.FarmID = @R)) AS AvgAge;";

            return _db.QuerySingle(sql, r =>
            {
                var capacity = r.Get<int>("Capacity");
                var birds = r.Get<int>("TotalBirds");
                return new DashboardSummary
                {
                    ActiveFlocks = r.Get<int>("ActiveFlocks"),
                    TotalBirds = birds,
                    FemaleBirds = r.Get<int>("FemaleBirds"),
                    MaleBirds = r.Get<int>("MaleBirds"),
                    Farms = r.Get<int>("Farms"),
                    Houses = r.Get<int>("Houses"),
                    HouseUtilisationPct = capacity > 0 ? Math.Round(birds * 100m / capacity, 1) : 0m,
                    AvgAgeWeeks = r.Get<int>("AvgAge")
                };
            }, Sql.P("@R", restrictFarmId)) ?? new DashboardSummary();
        }
    }
}
