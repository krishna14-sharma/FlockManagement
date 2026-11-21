using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using FlockManagement.Common.Helpers;
using FlockManagement.Common.Interfaces;
using Newtonsoft.Json;

namespace FlockManagement.Data.Infrastructure
{
    /// <summary>Short-hand factory for SqlParameter instances.</summary>
    public static class Sql
    {
        public static SqlParameter P(string name, object value)
        {
            return new SqlParameter(name, value ?? DBNull.Value);
        }

        public static SqlParameter NVarChar(string name, string value, int size)
        {
            return new SqlParameter(name, SqlDbType.NVarChar, size)
            {
                Value = string.IsNullOrEmpty(value) ? (object)DBNull.Value : value
            };
        }

        public static SqlParameter Date(string name, DateTime? value)
        {
            return new SqlParameter(name, SqlDbType.Date) { Value = value.HasValue ? (object)value.Value.Date : DBNull.Value };
        }

        public static SqlParameter Decimal(string name, decimal? value, byte precision = 18, byte scale = 4)
        {
            return new SqlParameter(name, SqlDbType.Decimal) { Precision = precision, Scale = scale, Value = (object)value ?? DBNull.Value };
        }

        public static SqlParameter OutInt(string name)
        {
            return new SqlParameter(name, SqlDbType.Int) { Direction = ParameterDirection.Output };
        }

        public static SqlParameter RowVersion(string name, byte[] value)
        {
            return new SqlParameter(name, SqlDbType.Binary, 8) { Value = (object)value ?? DBNull.Value };
        }

        /// <summary>Builds a LIKE pattern with SQL wildcards in the user text escaped.</summary>
        public static string ContainsPattern(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var escaped = text.Trim().Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
            return "%" + escaped + "%";
        }

        public static int ToInt(object value)
        {
            return value == null || value is DBNull ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
    }

    public static class DataRecordExtensions
    {
        public static T Get<T>(this IDataRecord record, string column)
        {
            var value = record[column];
            if (value == null || value is DBNull) return default(T);

            var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (target.IsInstanceOfType(value)) return (T)value;
            return (T)Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }

        public static string Str(this IDataRecord record, string column)
        {
            var value = record[column];
            return value == null || value is DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public static bool HasColumn(this IDataRecord record, string column)
        {
            for (var i = 0; i < record.FieldCount; i++)
            {
                if (string.Equals(record.GetName(i), column, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }

    /// <summary>Maps SQL Server errors to user-readable business errors.</summary>
    public static class SqlErrorTranslator
    {
        public static Exception Translate(SqlException ex)
        {
            if (ex == null) return null;

            // Business rule raised inside a stored procedure via THROW 50000-50999
            if (ex.Number >= 50000 && ex.Number < 51000)
                return new BusinessException(ex.Message, ex);

            switch (ex.Number)
            {
                case 2627: // unique constraint
                case 2601: // unique index
                    return new BusinessException("A record with the same code / number already exists.", ex);
                case 547:  // FK / CHECK constraint
                    return new BusinessException(ex.Message.IndexOf("CHECK", StringComparison.OrdinalIgnoreCase) >= 0
                        ? "The values entered break a data rule (for example a negative quantity or an invalid date range)."
                        : "The record is linked to other data and cannot be changed this way.", ex);
                case 1205: // deadlock victim
                    return new BusinessException("The database was busy. Please try again.", ex);
                default:
                    return ex;
            }
        }
    }

    /// <summary>Writes audit rows inside the caller's transaction.</summary>
    public static class AuditWriter
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            DateFormatString = "yyyy-MM-ddTHH:mm:ss",
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        public static string ToJson(object value)
        {
            return value == null ? null : JsonConvert.SerializeObject(value, JsonSettings);
        }

        public static void Write(IDbExecutor db, IUserContext user, string action, string table, object recordId,
            string oldJson, string newJson)
        {
            db.ExecuteProc("dbo.sp_AuditLog_Insert",
                Sql.P("@UserID", user != null && user.UserID > 0 ? (object)user.UserID : null),
                Sql.NVarChar("@UserName", user != null ? user.UserName : null, 50),
                Sql.NVarChar("@ActionType", action, 20),
                Sql.NVarChar("@TableName", table, 60),
                Sql.NVarChar("@RecordID", Convert.ToString(recordId, CultureInfo.InvariantCulture), 50),
                Sql.P("@OldValue", oldJson),
                Sql.P("@NewValue", newJson),
                Sql.NVarChar("@IPAddress", user != null ? user.IPAddress : null, 50));
        }
    }
}
