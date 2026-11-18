using System.Data;
using Polhem.Core;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Repository.Abstractions.Form;

namespace Polhem.Repository.Form
{
    /// <summary>
    /// Stores user queries in <c>st_user_query</c> and each user's default in <c>st_user_query_default</c>, both in
    /// the company database of the caller's session.
    /// </summary>
    internal sealed class UserQueryRepository : RepositoryBase, IUserQueryRepository
    {
        private const string QueryTable = "st_user_query";
        private const string DefaultTable = "st_user_query_default";

        private readonly DatabaseType _dbType;

        /// <summary>
        /// Initializes a new <see cref="UserQueryRepository"/>.
        /// </summary>
        /// <param name="ctx">The shared repository context.</param>
        /// <param name="accessToken">The current request's access token, which picks the company database.</param>
        /// <param name="progId">Unused on the framework axis; accepted for signature uniformity.</param>
        public UserQueryRepository(IRepositoryContext ctx, Guid accessToken, string progId)
            : base(ctx, accessToken, progId, DbScope.Company)
        {
            _dbType = ctx.ConnectionManager.GetConnectionInfo(DatabaseId).DatabaseType;
        }

        /// <inheritdoc/>
        public UserQueryList GetQueries(string progId, string userId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(progId);
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);

            var table = CreateQueryTable();
            // A deployment whose definitions predate user queries has no such tables. Asking the schema rather than
            // catching a provider exception keeps a real connection failure from reading as "no queries".
            var schemaProvider = DbDialectRegistry.Get(_dbType).CreateTableSchemaProvider(DatabaseId, Context.ConnectionManager);
            if (schemaProvider.GetTableSchema(QueryTable) == null || schemaProvider.GetTableSchema(DefaultTable) == null)
                return new UserQueryList(table, Guid.Empty);

            string sql = $"SELECT {Q("sys_rowid")}, {Q("sys_name")}, {Q("is_shared")}, {Q("user_id")}, {Q("criteria")} \n" +
                         $"FROM {Q(QueryTable)} \n" +
                         $"WHERE {Q(SysFields.CompanyId)} = {CommandTextVariable.CompanyId} AND {Q("prog_id")} = {{0}} \n" +
                         $"AND ({Q("user_id")} = {{1}} OR {Q("is_shared")} = {{2}}) \n" +
                         $"ORDER BY CASE WHEN {Q("user_id")} = {{3}} THEN 0 ELSE 1 END, {Q("sys_name")}";
            var dbAccess = CreateDbAccess();
            var rows = dbAccess.Execute(new DbCommandSpec(DbCommandKind.DataTable, sql, progId, userId, true, userId)).Table!;
            foreach (DataRow row in rows.Rows)
            {
                table.Rows.Add(
                    ValueUtilities.CGuid(row["sys_rowid"]),
                    ValueUtilities.CStr(row["sys_name"]),
                    ValueUtilities.CBool(row["is_shared"]),
                    string.Equals(ValueUtilities.CStr(row["user_id"]), userId, StringComparison.Ordinal),
                    ValueUtilities.CStr(row["criteria"]));
            }
            table.AcceptChanges();

            string defaultSql = $"SELECT {Q("query_rowid")} FROM {Q(DefaultTable)} \n" +
                                $"WHERE {Q(SysFields.CompanyId)} = {CommandTextVariable.CompanyId} AND {Q("prog_id")} = {{0}} AND {Q("user_id")} = {{1}}";
            var scalar = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar, defaultSql, progId, userId)).Scalar;
            var defaultRowId = scalar is null or DBNull ? Guid.Empty : ValueUtilities.CGuid(scalar);
            return new UserQueryList(table, defaultRowId);
        }

        /// <inheritdoc/>
        public UserQueryOwner? GetOwner(Guid rowId)
        {
            string sql = $"SELECT {Q("prog_id")}, {Q("user_id")}, {Q("is_shared")} FROM {Q(QueryTable)} \n" +
                         $"WHERE {Q(SysFields.CompanyId)} = {CommandTextVariable.CompanyId} AND {Q("sys_rowid")} = {{0}}";
            var table = CreateDbAccess().Execute(new DbCommandSpec(DbCommandKind.DataTable, sql, rowId)).Table!;
            if (table.Rows.Count == 0) return null;
            var row = table.Rows[0];
            return new UserQueryOwner(ValueUtilities.CStr(row["prog_id"]), ValueUtilities.CStr(row["user_id"]),
                ValueUtilities.CBool(row["is_shared"]));
        }

        /// <inheritdoc/>
        public void Insert(Guid rowId, string progId, string userId, string name, bool isShared, string criteria)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(progId);
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(criteria);

            var now = DateTime.UtcNow;
            string sql = $"INSERT INTO {Q(QueryTable)} \n" +
                         $"({Q("sys_rowid")}, {Q(SysFields.CompanyId)}, {Q("prog_id")}, {Q("user_id")}, {Q("sys_name")}, " +
                         $"{Q("is_shared")}, {Q("criteria")}, {Q("sys_insert_time")}, {Q("sys_update_time")}) \n" +
                         $"VALUES ({{0}}, {CommandTextVariable.CompanyId}, {{1}}, {{2}}, {{3}}, {{4}}, {{5}}, {{6}}, {{7}})";
            CreateDbAccess().Execute(new DbCommandSpec(DbCommandKind.NonQuery, sql,
                rowId, progId, userId, name, isShared, criteria, now, now));
        }

        /// <inheritdoc/>
        public int Update(Guid rowId, string name, bool isShared, string criteria)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(criteria);

            string sql = $"UPDATE {Q(QueryTable)} \n" +
                         $"SET {Q("sys_name")} = {{0}}, {Q("is_shared")} = {{1}}, {Q("criteria")} = {{2}}, {Q("sys_update_time")} = {{3}} \n" +
                         $"WHERE {Q(SysFields.CompanyId)} = {CommandTextVariable.CompanyId} AND {Q("sys_rowid")} = {{4}}";
            return CreateDbAccess().Execute(new DbCommandSpec(DbCommandKind.NonQuery, sql,
                name, isShared, criteria, DateTime.UtcNow, rowId)).RowsAffected;
        }

        /// <inheritdoc/>
        public int Delete(Guid rowId)
        {
            string defaults = $"DELETE FROM {Q(DefaultTable)} \n" +
                              $"WHERE {Q(SysFields.CompanyId)} = {CommandTextVariable.CompanyId} AND {Q("query_rowid")} = {{0}}";
            string query = $"DELETE FROM {Q(QueryTable)} \n" +
                           $"WHERE {Q(SysFields.CompanyId)} = {CommandTextVariable.CompanyId} AND {Q("sys_rowid")} = {{0}}";

            using var connection = Context.ConnectionManager.CreateConnection(DatabaseId);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            var dbAccess = new DbAccess(connection, _dbType).BindCompany(CompanyId);
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, defaults, rowId), transaction);
            int deleted = dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, query, rowId), transaction).RowsAffected;
            transaction.Commit();
            return deleted;
        }

        /// <inheritdoc/>
        public void SetDefault(string progId, string userId, Guid rowId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(progId);
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);

            string clear = $"DELETE FROM {Q(DefaultTable)} \n" +
                           $"WHERE {Q(SysFields.CompanyId)} = {CommandTextVariable.CompanyId} AND {Q("prog_id")} = {{0}} AND {Q("user_id")} = {{1}}";
            string insert = $"INSERT INTO {Q(DefaultTable)} \n" +
                            $"({Q("sys_rowid")}, {Q(SysFields.CompanyId)}, {Q("prog_id")}, {Q("user_id")}, {Q("query_rowid")}, {Q("sys_insert_time")}) \n" +
                            $"VALUES ({{0}}, {CommandTextVariable.CompanyId}, {{1}}, {{2}}, {{3}}, {{4}})";

            using var connection = Context.ConnectionManager.CreateConnection(DatabaseId);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            var dbAccess = new DbAccess(connection, _dbType).BindCompany(CompanyId);
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, clear, progId, userId), transaction);
            if (rowId != Guid.Empty)
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, insert,
                    Guid.NewGuid(), progId, userId, rowId, DateTime.UtcNow), transaction);
            }
            transaction.Commit();
        }

        private string Q(string identifier) => _dbType.QuoteIdentifier(identifier);

        private static DataTable CreateQueryTable()
        {
            var table = new DataTable("UserQuery");
            table.Columns.Add("sys_rowid", typeof(Guid));
            table.Columns.Add("sys_name", typeof(string));
            table.Columns.Add("is_shared", typeof(bool));
            table.Columns.Add("is_own", typeof(bool));
            table.Columns.Add("criteria", typeof(string));
            return table;
        }
    }
}
