using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace FlockManagement.Data.Infrastructure
{
    /// <summary>Executes commands either on a fresh connection or inside an open transaction.</summary>
    public interface IDbExecutor
    {
        IList<T> Query<T>(string sql, Func<IDataRecord, T> map, params SqlParameter[] parameters);
        IList<T> QueryProc<T>(string procedure, Func<IDataRecord, T> map, params SqlParameter[] parameters);
        T QuerySingle<T>(string sql, Func<IDataRecord, T> map, params SqlParameter[] parameters) where T : class;
        T QueryProcSingle<T>(string procedure, Func<IDataRecord, T> map, params SqlParameter[] parameters) where T : class;
        int Execute(string sql, params SqlParameter[] parameters);
        int ExecuteProc(string procedure, params SqlParameter[] parameters);
        object Scalar(string sql, params SqlParameter[] parameters);
    }

    public interface IDb : IDbExecutor
    {
        T InTransaction<T>(Func<IDbExecutor, T> work);
        void InTransaction(Action<IDbExecutor> work);
        /// <summary>Runs a procedure and returns its first result set as a DataTable (reports).</summary>
        DataTable QueryTableProc(string procedure, params SqlParameter[] parameters);
    }

    /// <summary>ADO.NET access to SQL Server. All SQL is parameterised.</summary>
    public sealed class SqlDb : IDb
    {
        private readonly string _connectionString;
        private readonly int _commandTimeout;

        public SqlDb(string connectionString, int commandTimeoutSeconds = 60)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string is required.", "connectionString");
            _connectionString = connectionString;
            _commandTimeout = commandTimeoutSeconds;
        }

        public IList<T> Query<T>(string sql, Func<IDataRecord, T> map, params SqlParameter[] parameters)
        {
            return WithConnection(c => CommandRunner.Query(c, null, CommandType.Text, sql, map, parameters, _commandTimeout));
        }

        public IList<T> QueryProc<T>(string procedure, Func<IDataRecord, T> map, params SqlParameter[] parameters)
        {
            return WithConnection(c => CommandRunner.Query(c, null, CommandType.StoredProcedure, procedure, map, parameters, _commandTimeout));
        }

        public T QuerySingle<T>(string sql, Func<IDataRecord, T> map, params SqlParameter[] parameters) where T : class
        {
            var list = Query(sql, map, parameters);
            return list.Count > 0 ? list[0] : null;
        }

        public T QueryProcSingle<T>(string procedure, Func<IDataRecord, T> map, params SqlParameter[] parameters) where T : class
        {
            var list = QueryProc(procedure, map, parameters);
            return list.Count > 0 ? list[0] : null;
        }

        public int Execute(string sql, params SqlParameter[] parameters)
        {
            return WithConnection(c => CommandRunner.Execute(c, null, CommandType.Text, sql, parameters, _commandTimeout));
        }

        public int ExecuteProc(string procedure, params SqlParameter[] parameters)
        {
            return WithConnection(c => CommandRunner.Execute(c, null, CommandType.StoredProcedure, procedure, parameters, _commandTimeout));
        }

        public object Scalar(string sql, params SqlParameter[] parameters)
        {
            return WithConnection(c => CommandRunner.Scalar(c, null, CommandType.Text, sql, parameters, _commandTimeout));
        }

        public T InTransaction<T>(Func<IDbExecutor, T> work)
        {
            if (work == null) throw new ArgumentNullException("work");
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var tx = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        var result = work(new TransactionExecutor(connection, tx, _commandTimeout));
                        tx.Commit();
                        return result;
                    }
                    catch
                    {
                        SafeRollback(tx);
                        throw;
                    }
                }
            }
        }

        public void InTransaction(Action<IDbExecutor> work)
        {
            if (work == null) throw new ArgumentNullException("work");
            InTransaction<bool>(x => { work(x); return true; });
        }

        public DataTable QueryTableProc(string procedure, params SqlParameter[] parameters)
        {
            return WithConnection(c =>
            {
                using (var cmd = new SqlCommand(procedure, c) { CommandType = CommandType.StoredProcedure, CommandTimeout = _commandTimeout })
                {
                    if (parameters != null)
                    {
                        foreach (var p in parameters)
                        {
                            if (p == null) continue;
                            if (p.Value == null) p.Value = DBNull.Value;
                            cmd.Parameters.Add(p);
                        }
                    }
                    try
                    {
                        var table = new DataTable { Locale = System.Globalization.CultureInfo.InvariantCulture };
                        using (var reader = cmd.ExecuteReader())
                        {
                            table.Load(reader);
                        }
                        return table;
                    }
                    catch (SqlException ex)
                    {
                        throw SqlErrorTranslator.Translate(ex);
                    }
                }
            });
        }

        private TResult WithConnection<TResult>(Func<SqlConnection, TResult> action)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                return action(connection);
            }
        }

        private static void SafeRollback(SqlTransaction tx)
        {
            try
            {
                if (tx.Connection != null) tx.Rollback();
            }
            catch (InvalidOperationException)
            {
                // Transaction already rolled back by SQL Server (XACT_ABORT) - nothing to do.
            }
            catch (SqlException)
            {
                // Same as above for a doomed transaction.
            }
        }

        /// <summary>Executor bound to an open connection + transaction.</summary>
        private sealed class TransactionExecutor : IDbExecutor
        {
            private readonly SqlConnection _c;
            private readonly SqlTransaction _t;
            private readonly int _timeout;

            public TransactionExecutor(SqlConnection c, SqlTransaction t, int timeout)
            {
                _c = c;
                _t = t;
                _timeout = timeout;
            }

            public IList<T> Query<T>(string sql, Func<IDataRecord, T> map, params SqlParameter[] parameters)
            {
                return CommandRunner.Query(_c, _t, CommandType.Text, sql, map, parameters, _timeout);
            }

            public IList<T> QueryProc<T>(string procedure, Func<IDataRecord, T> map, params SqlParameter[] parameters)
            {
                return CommandRunner.Query(_c, _t, CommandType.StoredProcedure, procedure, map, parameters, _timeout);
            }

            public T QuerySingle<T>(string sql, Func<IDataRecord, T> map, params SqlParameter[] parameters) where T : class
            {
                var list = Query(sql, map, parameters);
                return list.Count > 0 ? list[0] : null;
            }

            public T QueryProcSingle<T>(string procedure, Func<IDataRecord, T> map, params SqlParameter[] parameters) where T : class
            {
                var list = QueryProc(procedure, map, parameters);
                return list.Count > 0 ? list[0] : null;
            }

            public int Execute(string sql, params SqlParameter[] parameters)
            {
                return CommandRunner.Execute(_c, _t, CommandType.Text, sql, parameters, _timeout);
            }

            public int ExecuteProc(string procedure, params SqlParameter[] parameters)
            {
                return CommandRunner.Execute(_c, _t, CommandType.StoredProcedure, procedure, parameters, _timeout);
            }

            public object Scalar(string sql, params SqlParameter[] parameters)
            {
                return CommandRunner.Scalar(_c, _t, CommandType.Text, sql, parameters, _timeout);
            }
        }

        private static class CommandRunner
        {
            public static IList<T> Query<T>(SqlConnection c, SqlTransaction t, CommandType type, string text,
                Func<IDataRecord, T> map, SqlParameter[] parameters, int timeout)
            {
                var list = new List<T>();
                using (var cmd = Build(c, t, type, text, parameters, timeout))
                {
                    try
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read()) list.Add(map(reader));
                        }
                    }
                    catch (SqlException ex)
                    {
                        throw SqlErrorTranslator.Translate(ex);
                    }
                }
                return list;
            }

            public static int Execute(SqlConnection c, SqlTransaction t, CommandType type, string text,
                SqlParameter[] parameters, int timeout)
            {
                using (var cmd = Build(c, t, type, text, parameters, timeout))
                {
                    try
                    {
                        return cmd.ExecuteNonQuery();
                    }
                    catch (SqlException ex)
                    {
                        throw SqlErrorTranslator.Translate(ex);
                    }
                }
            }

            public static object Scalar(SqlConnection c, SqlTransaction t, CommandType type, string text,
                SqlParameter[] parameters, int timeout)
            {
                using (var cmd = Build(c, t, type, text, parameters, timeout))
                {
                    try
                    {
                        var value = cmd.ExecuteScalar();
                        return value is DBNull ? null : value;
                    }
                    catch (SqlException ex)
                    {
                        throw SqlErrorTranslator.Translate(ex);
                    }
                }
            }

            private static SqlCommand Build(SqlConnection c, SqlTransaction t, CommandType type, string text,
                SqlParameter[] parameters, int timeout)
            {
                var cmd = new SqlCommand(text, c, t) { CommandType = type, CommandTimeout = timeout };
                if (parameters != null)
                {
                    foreach (var p in parameters)
                    {
                        if (p == null) continue;
                        if (p.Value == null) p.Value = DBNull.Value;
                        cmd.Parameters.Add(p);
                    }
                }
                return cmd;
            }
        }
    }
}
