using System.ComponentModel;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Repository.System;
using Polhem.Tests.Shared;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// Round-trip tests across the database providers. The seed user '001' ↔ company 'C001' mapping is already
    /// created by <see cref="SharedDatabaseState"/>; the tests cover granted+enabled, not-granted and
    /// granted+disabled.
    /// </summary>
    public class UserCompanyRepositoryTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public UserCompanyRepositoryTests(SharedDbFixture fx) { _fx = fx; }

        private UserCompanyRepository CreateRepo(DatabaseType databaseType)
            => new UserCompanyRepository(
                TestRepositoryContext.Create(
                    _fx.GetRequiredService<IDbConnectionManager>(),
                    router: new ProviderScopedRouter(databaseType)),
                Guid.Empty,
                string.Empty);

        #region HasAccess — Granted + Enabled

        private void RunHasAccessGranted(DatabaseType databaseType)
        {
            var repo = CreateRepo(databaseType);
            Assert.True(repo.HasAccess("001", "C001"));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("HasAccess('001','C001') returns true for the seeded mapping on SQL Server")]
        public void HasAccess_Granted_SqlServer() => RunHasAccessGranted(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("HasAccess('001','C001') returns true for the seeded mapping on PostgreSQL")]
        public void HasAccess_Granted_PostgreSql() => RunHasAccessGranted(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("HasAccess('001','C001') returns true for the seeded mapping on SQLite")]
        public void HasAccess_Granted_Sqlite() => RunHasAccessGranted(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("HasAccess('001','C001') returns true for the seeded mapping on MySQL")]
        public void HasAccess_Granted_MySql() => RunHasAccessGranted(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("HasAccess('001','C001') returns true for the seeded mapping on Oracle")]
        public void HasAccess_Granted_Oracle() => RunHasAccessGranted(DatabaseType.Oracle);

        #endregion

        #region HasAccess — Not Granted (nonexistent company)

        private void RunHasAccessNotGranted(DatabaseType databaseType)
        {
            var repo = CreateRepo(databaseType);
            Assert.False(repo.HasAccess("001", "__nonexistent_company_xyz__"));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("HasAccess returns false for a nonexistent company on SQL Server")]
        public void HasAccess_NotGranted_SqlServer() => RunHasAccessNotGranted(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("HasAccess returns false for a nonexistent company on PostgreSQL")]
        public void HasAccess_NotGranted_PostgreSql() => RunHasAccessNotGranted(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("HasAccess returns false for a nonexistent company on SQLite")]
        public void HasAccess_NotGranted_Sqlite() => RunHasAccessNotGranted(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("HasAccess returns false for a nonexistent company on MySQL")]
        public void HasAccess_NotGranted_MySql() => RunHasAccessNotGranted(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("HasAccess returns false for a nonexistent company on Oracle")]
        public void HasAccess_NotGranted_Oracle() => RunHasAccessNotGranted(DatabaseType.Oracle);

        #endregion

        #region HasAccess — Granted but Company Disabled

        private void RunHasAccessDisabledCompany(DatabaseType dbType)
        {
            // Seed a disabled company and map user '001' to it; HasAccess must be false.
            var dbAccess = _fx.NewDbAccess(TestDbConventions.GetDatabaseId(dbType, DbCategoryIds.Common));
            var companyId = string.Concat("DIS_", Guid.NewGuid().ToString("N").AsSpan(0, 6));
            var companyRowId = Guid.NewGuid();
            var linkRowId = Guid.NewGuid();

            string tblCompany = dbType.QuoteIdentifier("st_company");
            string tblUc = dbType.QuoteIdentifier("st_user_company");
            string tblUser = dbType.QuoteIdentifier("st_user");
            string colRowId = dbType.QuoteIdentifier("sys_rowid");
            string colSysId = dbType.QuoteIdentifier("sys_id");
            string colName = dbType.QuoteIdentifier("sys_name");
            string colDbId = dbType.QuoteIdentifier("company_database_id");
            string colNumFmt = dbType.QuoteIdentifier("number_formats_xml");
            string colDefCur = dbType.QuoteIdentifier("default_currency");
            string colCashRnd = dbType.QuoteIdentifier("cash_rounding_xml");
            string colAllowCur = dbType.QuoteIdentifier("allowed_currencies_xml");
            string colEnabled = dbType.QuoteIdentifier("enabled");
            string colUserRowId = dbType.QuoteIdentifier("user_rowid");
            string colCompanyRowId = dbType.QuoteIdentifier("company_rowid");
            string colInsTime = dbType.QuoteIdentifier("sys_insert_time");

            string nowExpr = dbType == DatabaseType.PostgreSQL || dbType == DatabaseType.SQLite ? "CURRENT_TIMESTAMP"
                           : dbType == DatabaseType.SQLServer ? "GETDATE()"
                           : dbType == DatabaseType.MySQL ? "CURRENT_TIMESTAMP(6)"
                           : "SYSTIMESTAMP";
            string disabledLiteral = dbType == DatabaseType.PostgreSQL ? "FALSE" : "0";

            // number_formats_xml / cash_rounding_xml / allowed_currencies_xml are NOT NULL Text columns;
            // MySQL TEXT columns can't carry a DEFAULT, so they must be supplied explicitly (empty strings)
            // rather than relying on a DB-side default. default_currency gets USD, as every company must.
            var insertCompany = new DbCommandSpec(DbCommandKind.NonQuery,
                $"INSERT INTO {tblCompany} ({colRowId}, {colSysId}, {colName}, {colDbId}, {colNumFmt}, {colDefCur}, {colCashRnd}, {colAllowCur}, {colEnabled}, {colInsTime}) " +
                $"VALUES ({{0}}, {{1}}, {{2}}, {{3}}, {{4}}, {{5}}, {{6}}, {{7}}, {disabledLiteral}, {nowExpr})",
                companyRowId, companyId, "停用公司", "common", string.Empty, "USD", string.Empty, string.Empty);
            dbAccess.Execute(insertCompany);

            var lookupUser = new DbCommandSpec(DbCommandKind.Scalar,
                $"SELECT {colRowId} FROM {tblUser} WHERE {colSysId} = {{0}}", "001");
            var userResult = dbAccess.Execute(lookupUser);
            var userRowId = ToGuid(userResult.Scalar!);

            var insertLink = new DbCommandSpec(DbCommandKind.NonQuery,
                $"INSERT INTO {tblUc} ({colRowId}, {colUserRowId}, {colCompanyRowId}, {colInsTime}) " +
                $"VALUES ({{0}}, {{1}}, {{2}}, {nowExpr})",
                linkRowId, userRowId, companyRowId);
            dbAccess.Execute(insertLink);

            try
            {
                var repo = CreateRepo(dbType);
                Assert.False(repo.HasAccess("001", companyId));
            }
            finally
            {
                var cleanupLink = new DbCommandSpec(DbCommandKind.NonQuery,
                    $"DELETE FROM {tblUc} WHERE {colRowId} = {{0}}", linkRowId);
                dbAccess.Execute(cleanupLink);
                var cleanupCompany = new DbCommandSpec(DbCommandKind.NonQuery,
                    $"DELETE FROM {tblCompany} WHERE {colRowId} = {{0}}", companyRowId);
                dbAccess.Execute(cleanupCompany);
            }
        }

        private static Guid ToGuid(object value)
        {
            if (value is Guid g) return g;
            if (value is byte[] b && b.Length == 16) return new Guid(b);
            if (value is string s && Guid.TryParse(s, out var parsed)) return parsed;
            throw new InvalidOperationException($"Cannot convert {value?.GetType().Name} to Guid.");
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("HasAccess returns false for a granted but disabled company on SQL Server")]
        public void HasAccess_GrantedDisabled_SqlServer() => RunHasAccessDisabledCompany(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("HasAccess returns false for a granted but disabled company on PostgreSQL")]
        public void HasAccess_GrantedDisabled_PostgreSql() => RunHasAccessDisabledCompany(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("HasAccess returns false for a granted but disabled company on SQLite")]
        public void HasAccess_GrantedDisabled_Sqlite() => RunHasAccessDisabledCompany(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("HasAccess returns false for a granted but disabled company on MySQL")]
        public void HasAccess_GrantedDisabled_MySql() => RunHasAccessDisabledCompany(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("HasAccess returns false for a granted but disabled company on Oracle")]
        public void HasAccess_GrantedDisabled_Oracle() => RunHasAccessDisabledCompany(DatabaseType.Oracle);

        #endregion
    }
}
