using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;

namespace Polhem.Tests.Shared
{
    /// <summary>
    /// Creates and removes test-owned <c>st_user</c> rows in the common database.
    /// </summary>
    /// <remarks>
    /// WARNING: a test that modifies a user row must **not touch the seed user '001'**. Parallel test processes
    /// share the physical database, so sharing one row always races (this happened: when a flag test and a BO test
    /// changed '001' at the same time, each read the other's value). Each test creates its own row and deletes it.
    /// </remarks>
    public static class TestUsers
    {
        /// <summary>
        /// Creates a user row with a unique account and returns its <c>sys_id</c>.
        /// </summary>
        /// <param name="connectionManager">The connection manager.</param>
        /// <param name="prefix">The account prefix, which identifies the originating test in the database.</param>
        public static string Create(IDbConnectionManager connectionManager, string prefix)
        {
            var dbType = connectionManager.GetConnectionInfo(DbCategoryIds.Common).DatabaseType;
            string tbl = dbType.QuoteIdentifier("st_user");
            string colRowId = dbType.QuoteIdentifier(SysFields.RowId);
            string colId = dbType.QuoteIdentifier(SysFields.Id);
            string colName = dbType.QuoteIdentifier(SysFields.Name);
            string colPwd = dbType.QuoteIdentifier("password");
            string colEmail = dbType.QuoteIdentifier("email");
            string colNote = dbType.QuoteIdentifier("note");
            string colTimeZone = dbType.QuoteIdentifier("time_zone");
            string colCulture = dbType.QuoteIdentifier("culture");

            // The password, email and note columns get a single space rather than an empty string. Oracle treats ''
            // as NULL, which violates the NOT NULL constraint; elsewhere it stays a one-character string.
            // `deployment_admin` and `sys_insert_time` are left out on purpose so the column defaults apply, which
            // is exactly the "a new user is not an administrator by default" behavior under test.
            string sysId = $"{prefix}-{Guid.NewGuid():N}"[..20];
            string sql = $"INSERT INTO {tbl} ({colRowId}, {colId}, {colName}, {colPwd}, {colEmail}, {colNote}, {colTimeZone}, {colCulture}) " +
                         $"VALUES ({{0}}, {{1}}, {{2}}, ' ', ' ', ' ', 'Asia/Taipei', 'zh-TW')";
            new DbAccess(DbCategoryIds.Common, connectionManager)
                .Execute(new DbCommandSpec(DbCommandKind.NonQuery, sql, Guid.NewGuid(), sysId, "測試使用者"));
            return sysId;
        }

        /// <summary>
        /// Removes the given user row. Used for cleanup; a missing row is not an error.
        /// </summary>
        /// <param name="connectionManager">The connection manager.</param>
        /// <param name="sysId">The account of the user to remove.</param>
        public static void Delete(IDbConnectionManager connectionManager, string sysId)
        {
            var dbType = connectionManager.GetConnectionInfo(DbCategoryIds.Common).DatabaseType;
            string sql = $"DELETE FROM {dbType.QuoteIdentifier("st_user")} " +
                         $"WHERE {dbType.QuoteIdentifier(SysFields.Id)} = {{0}}";
            new DbAccess(DbCategoryIds.Common, connectionManager)
                .Execute(new DbCommandSpec(DbCommandKind.NonQuery, sql, sysId));
        }
    }
}
