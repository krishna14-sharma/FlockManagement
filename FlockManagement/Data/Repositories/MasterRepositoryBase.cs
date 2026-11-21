using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using FlockManagement.Common.Constants;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Helpers;
using FlockManagement.Common.Interfaces;
using FlockManagement.Data.Infrastructure;
using FlockManagement.Data.Interfaces;

namespace FlockManagement.Data.Repositories
{
    /// <summary>
    /// Shared implementation for master tables: paged search, get, insert/update with
    /// audit trail in one transaction, soft activate/deactivate and dropdown lookup.
    /// Derived classes supply the SQL fragments and the mapping. No user text is ever
    /// concatenated into SQL - fragments are compile-time constants.
    /// </summary>
    public abstract class MasterRepositoryBase<T> : IMasterRepository<T> where T : class
    {
        protected readonly IDb Db;

        protected MasterRepositoryBase(IDb db)
        {
            if (db == null) throw new ArgumentNullException("db");
            Db = db;
        }

        /// <summary>Physical table, e.g. dbo.tblFarm.</summary>
        protected abstract string TableName { get; }
        /// <summary>Alias used in SelectColumns / FromClause, e.g. "f".</summary>
        protected abstract string Alias { get; }
        protected abstract string KeyColumn { get; }
        protected abstract string SelectColumns { get; }
        protected abstract string FromClause { get; }
        /// <summary>Columns searched with LIKE.</summary>
        protected abstract string[] SearchColumns { get; }
        protected abstract string OrderBy { get; }
        /// <summary>SQL expression for the dropdown text.</summary>
        protected abstract string LookupTextExpression { get; }
        /// <summary>Column holding the parent key (e.g. h.FarmID), or null.</summary>
        protected virtual string ParentColumn { get { return null; } }
        /// <summary>Column used to apply the user's farm restriction, or null.</summary>
        protected virtual string FarmColumn { get { return null; } }

        protected abstract T Map(IDataRecord r);
        protected abstract int GetKey(T entity);
        protected abstract int Insert(IDbExecutor db, T entity, int userId);
        protected abstract void Update(IDbExecutor db, T entity, int userId);

        protected string ShortTableName
        {
            get { return TableName.StartsWith("dbo.", StringComparison.OrdinalIgnoreCase) ? TableName.Substring(4) : TableName; }
        }

        public virtual PagedResult<T> Search(PageRequest request)
        {
            request = (request ?? new PageRequest()).Normalize();

            var where = new List<string>();
            if (SearchColumns.Length > 0)
                where.Add("(@Search IS NULL OR " + string.Join(" OR ", SearchColumns.Select(c => c + " LIKE @Search")) + ")");
            where.Add("(@IsActive IS NULL OR " + Alias + ".IsActive = @IsActive)");
            if (ParentColumn != null) where.Add("(@ParentID IS NULL OR " + ParentColumn + " = @ParentID)");
            if (FarmColumn != null) where.Add("(@RestrictFarmID IS NULL OR " + FarmColumn + " = @RestrictFarmID OR " + FarmColumn + " IS NULL)");

            var sql = "SELECT " + SelectColumns + ", COUNT(*) OVER () AS TotalCount FROM " + FromClause +
                      " WHERE " + string.Join(" AND ", where) +
                      " ORDER BY " + OrderBy +
                      " OFFSET (@PageNo - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;";

            var total = 0;
            var items = Db.Query(sql, r =>
            {
                total = r.Get<int>("TotalCount");
                return Map(r);
            },
                Sql.NVarChar("@Search", Sql.ContainsPattern(request.Search), 210),
                Sql.P("@IsActive", request.IsActive),
                Sql.P("@ParentID", request.ParentID),
                Sql.P("@RestrictFarmID", request.RestrictFarmID),
                Sql.P("@PageNo", request.PageNo),
                Sql.P("@PageSize", request.PageSize));

            return new PagedResult<T>(items, total, request.PageNo, request.PageSize);
        }

        public virtual T GetById(int id)
        {
            return Load(Db, id);
        }

        protected T Load(IDbExecutor db, int id)
        {
            var sql = "SELECT " + SelectColumns + " FROM " + FromClause + " WHERE " + Alias + "." + KeyColumn + " = @Id;";
            return db.QuerySingle(sql, Map, Sql.P("@Id", id));
        }

        public virtual int Save(T entity, IUserContext user)
        {
            if (entity == null) throw new ArgumentNullException("entity");
            if (user == null) throw new ArgumentNullException("user");

            return Db.InTransaction(db =>
            {
                var id = GetKey(entity);
                string oldJson = null;
                string action;

                if (id > 0)
                {
                    var existing = Load(db, id);
                    if (existing == null) throw new BusinessException("The record no longer exists.");
                    oldJson = AuditWriter.ToJson(existing);
                    Update(db, entity, user.UserID);
                    action = AuditActions.Update;
                }
                else
                {
                    id = Insert(db, entity, user.UserID);
                    action = AuditActions.Insert;
                }

                var current = Load(db, id);
                AuditWriter.Write(db, user, action, ShortTableName, id, oldJson, AuditWriter.ToJson(current));
                return id;
            });
        }

        public virtual void SetActive(int id, bool isActive, IUserContext user)
        {
            Db.InTransaction(db =>
            {
                var sql = "UPDATE " + TableName + " SET IsActive = @IsActive, ModifiedBy = @UserID, ModifiedDate = SYSDATETIME() " +
                          "WHERE " + KeyColumn + " = @Id;";
                var rows = db.Execute(sql, Sql.P("@IsActive", isActive), Sql.P("@UserID", user.UserID), Sql.P("@Id", id));
                if (rows == 0) throw new BusinessException("The record no longer exists.");

                AuditWriter.Write(db, user, AuditActions.Status, ShortTableName, id,
                    AuditWriter.ToJson(new { IsActive = !isActive }), AuditWriter.ToJson(new { IsActive = isActive }));
            });
        }

        public virtual IList<LookupItem> GetLookup(int? restrictFarmId = null, int? parentId = null)
        {
            var where = new List<string> { Alias + ".IsActive = 1" };
            if (FarmColumn != null) where.Add("(@RestrictFarmID IS NULL OR " + FarmColumn + " = @RestrictFarmID OR " + FarmColumn + " IS NULL)");
            if (ParentColumn != null) where.Add("(@ParentID IS NULL OR " + ParentColumn + " = @ParentID)");

            var parentSelect = ParentColumn ?? "CAST(NULL AS INT)";
            var sql = "SELECT " + Alias + "." + KeyColumn + " AS Id, " + LookupTextExpression + " AS Text, " + parentSelect + " AS ParentId " +
                      "FROM " + FromClause + " WHERE " + string.Join(" AND ", where) + " ORDER BY Text;";

            return Db.Query(sql, r => new LookupItem(r.Get<int>("Id"), r.Str("Text"), r.Get<int?>("ParentId")),
                Sql.P("@RestrictFarmID", restrictFarmId), Sql.P("@ParentID", parentId));
        }

        /// <summary>Executes an INSERT that ends with SELECT CAST(SCOPE_IDENTITY() AS INT).</summary>
        protected static int InsertAndGetId(IDbExecutor db, string sql, params SqlParameter[] parameters)
        {
            var value = db.Scalar(sql + " SELECT CAST(SCOPE_IDENTITY() AS INT);", parameters);
            return Sql.ToInt(value);
        }
    }
}
