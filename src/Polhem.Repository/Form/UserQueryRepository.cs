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
        private const string RowIdColumn = "sys_rowid";
        private const string NameColumn = "sys_name";
        private const string SharedColumn = "is_shared";
        private const string UserColumn = "user_id";
        private const string ProgColumn = "prog_id";

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

            string sql = $"SELECT {Q(RowIdColumn)}, {Q(NameColumn)}, {Q(SharedColumn)}, {Q(UserColumn)}, {Q("criteria")}" +
                         $" FROM {Q(QueryTable)}" +
                         $" WHERE {Q(SysFields.CompanyId)} = {CommandTextVariable.CompanyId} AND {Q(ProgColumn)} = {{0}}" +
                         $" AND ({Q(UserColumn)} = {{1}} OR {Q(SharedColumn)} = {{2}})" +
                         $" ORDER BY CASE WHEN {Q(UserColumn)} = {{3}} THEN 0 ELSE 1 END, {Q(NameColumn)}";
            var dbAccess = CreateDbAccess();
            var rows = dbAccess.Execute(new DbCommandSpec(DbCommandKind.DataTable, sql, progId, userId, true, userId)).Table!;
            foreach (DataRow row in rows.Rows)
            {
                table.Rows.Add(
                    ValueUtilities.CGuid(row[RowIdColumn]),
                    ValueUtilities.CStr(row[NameColumn]),
                    ValueUtilities.CBool(row[SharedColumn]),
                    string.Equals(ValueUtilities.CStr(row[UserColumn]), userId, StringComparison.Ordinal),
                    ValueUtilities.CStr(row["criteria"]));
            }
            table.AcceptChanges();

            string defaultSql = $"SELECT {Q("query_rowid")} FROM {Q(DefaultTable)}" +
                                $" WHERE {Q(SysFields.CompanyId)} = {CommandTextVariable.CompanyId} AND {Q(ProgColumn)} = {{0}} AND {Q(UserColumn)} = {{1}}";
            var scalar = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar, defaultSql, progId, userId)).Scalar;
            var defaultRowId = scalar is null or DBNull ? Guid.Empty : ValueUtilities.CGuid(scalar);
            return new UserQueryList(table, defaultRowId);
        }

        /// <inheritdoc/>
        public UserQueryOwner? GetOwner(Guid rowId)
        {
            string sql = $"SELECT {Q(ProgColumn)}, {Q(UserColumn)}, {Q(SharedColumn)} FROM {Q(QueryTable)}" +
                         $" WHERE {Q(SysFields.CompanyId)} = {CommandTextVariable.CompanyId} AND {Q(RowIdColumn)} = {{0}}";
            var table = CreateDbAccess().Execute(new DbCommandSpec(DbCommandKind.DataTable, sql, rowId)).Table!;
            if (table.Rows.Count == 0) return null;
            var row = table.Rows[0];
            return new UserQueryOwner(ValueUtilities.CStr(row[ProgColumn]), ValueUtilities.CStr(row[UserColumn]),
                ValueUtilities.CBool(row[SharedColumn]));
        }

        /// <inheritdoc/>
        public void Insert(Guid rowId, string progId, string userId, string name, bool isShared, string criteria)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(progId);
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(criteria);

            var now = DateTime.UtcNow;
            string sql = $"INSERT INTO {Q(QueryTable)}" +
                         $" ({Q(RowIdColumn)}, {Q(SysFields.CompanyId)}, {Q(ProgColumn)}, {Q(UserColumn)}, {Q(NameColumn)}, " +
                         $"{Q(SharedColumn)}, {Q("criteria")}, {Q("sys_insert_time")}, {Q("sys_update_time")})" +
                         $" VALUES ({{0}}, {CommandTextVariable.CompanyId}, {{1}}, {{2}}, {{3}}, {{4}}, {{5}}, {{6}}, {{7}})";
            CreateDbAccess().Execute(new DbCommandSpec(DbCommandKind.NonQuery, sql,
                rowId, progId, userId, name, isShared, criteria, now, now));
        }

        /// <inheritdoc/>
        public int Update(Guid rowId, string name, bool isShared, string criteria)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(criteria);

            string sql = $"UPDATE {Q(QueryTable)}" +
                         $" SET {Q(NameColumn)} = {{0}}, {Q(SharedColumn)} = {{1}}, {Q("criteria")} = {{2}}, {Q("sys_update_time")} = {{3}}" +
                         $" WHERE {Q(SysFields.CompanyId)} = {CommandTextVariable.CompanyId} AND {Q(RowIdColumn)} = {{4}}";
            return CreateDbAccess().Execute(new DbCommandSpec(DbCommandKind.NonQuery, sql,
                name, isShared, criteria, DateTime.UtcNow, rowId)).RowsAffected;
        }

        /// <inheritdoc/>
        public int Delete(Guid rowId)
        {
            string defaults = $"DELETE FROM {Q(DefaultTable)}" +
                              $" WHERE {Q(SysFields.CompanyId)} = {CommandTextVariable.CompanyId} AND {Q("query_rowid")} = {{0}}";
            string query = $"DELETE FROM {Q(QueryTable)}" +
                           $" WHERE {Q(SysFields.CompanyId)} = {CommandTextVariable.CompanyId} AND {Q(RowIdColumn)} = {{0}}";

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

            string clear = $"DELETE FROM {Q(DefaultTable)}" +
                           $" WHERE {Q(SysFields.CompanyId)} = {CommandTextVariable.CompanyId} AND {Q(ProgColumn)} = {{0}} AND {Q(UserColumn)} = {{1}}";
            string insert = $"INSERT INTO {Q(DefaultTable)}" +
                            $" ({Q(RowIdColumn)}, {Q(SysFields.CompanyId)}, {Q(ProgColumn)}, {Q(UserColumn)}, {Q("query_rowid")}, {Q("sys_insert_time")})" +
                            $" VALUES ({{0}}, {CommandTextVariable.CompanyId}, {{1}}, {{2}}, {{3}}, {{4}})";

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
            table.Columns.Add(RowIdColumn, typeof(Guid));
            table.Columns.Add(NameColumn, typeof(string));
            table.Columns.Add(SharedColumn, typeof(bool));
            table.Columns.Add("is_own", typeof(bool));
            table.Columns.Add("criteria", typeof(string));
            return table;
        }
    }
}
