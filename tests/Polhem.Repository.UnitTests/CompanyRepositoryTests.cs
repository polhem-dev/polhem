using System.ComponentModel;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Repository.System;
using Polhem.Tests.Shared;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// Round-trip tests across the database providers. The seed company 'C001' is created by
    /// <see cref="SharedDatabaseState"/>; the tests cover enabled, nonexistent and disabled.
    /// </summary>
    public class CompanyRepositoryTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public CompanyRepositoryTests(SharedDbFixture fx) { _fx = fx; }

        private CompanyRepository CreateRepo(DatabaseType databaseType)
            => new CompanyRepository(
                TestRepositoryContext.Create(
                    _fx.GetRequiredService<IDbConnectionManager>(),
                    router: new ProviderScopedRouter(databaseType)),
                Guid.Empty,
                string.Empty);

        #region GetById — Enabled (seed company 'C001')

        private void RunGetByIdEnabled(DatabaseType databaseType)
        {
            var repo = CreateRepo(databaseType);
            var result = repo.GetById("C001");
            Assert.NotNull(result);
            Assert.Equal("C001", result.CompanyId);
            Assert.Equal("測試公司", result.CompanyName);
            Assert.False(string.IsNullOrEmpty(result.CompanyDatabaseId));
            // Seed company 'C001' ships no customization. customize_id is AllowNull=false, so the
            // 4 standard dialects default it to '' and Oracle stores it nullable (Oracle equates
            // '' with NULL); either way it reads back as an empty string across all dialects.
            Assert.Equal(string.Empty, result.CustomizeId);
            // Seed ships no number_formats_xml, so the override table reads back empty and every
            // kind falls back to the framework default.
            Assert.Empty(result.NumberFormats);
            // A company must carry a default currency, so the seed writes USD. It ships no
            // cash_rounding_xml / allowed_currencies_xml, so both currency tables read back empty across
            // all dialects (the empty-string columns deserialize to empty collections).
            Assert.Equal("USD", result.DefaultCurrency);
            Assert.Empty(result.CashRounding);
            Assert.Empty(result.AllowedCurrencies);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("GetById 'C001' returns the enabled seed company on SQL Server")]
        public void GetById_Enabled_SqlServer() => RunGetByIdEnabled(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("GetById 'C001' returns the enabled seed company on PostgreSQL")]
        public void GetById_Enabled_PostgreSql() => RunGetByIdEnabled(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("GetById 'C001' returns the enabled seed company on SQLite")]
        public void GetById_Enabled_Sqlite() => RunGetByIdEnabled(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("GetById 'C001' returns the enabled seed company on MySQL")]
        public void GetById_Enabled_MySql() => RunGetByIdEnabled(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("GetById 'C001' returns the enabled seed company on Oracle")]
        public void GetById_Enabled_Oracle() => RunGetByIdEnabled(DatabaseType.Oracle);

        #endregion

        #region GetById — NonExistent

        private void RunGetByIdNotFound(DatabaseType databaseType)
        {
            var repo = CreateRepo(databaseType);
            var result = repo.GetById("__nonexistent_company_xyz__");
            Assert.Null(result);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("GetById returns null for a nonexistent company on SQL Server")]
        public void GetById_NotFound_SqlServer() => RunGetByIdNotFound(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("GetById returns null for a nonexistent company on PostgreSQL")]
        public void GetById_NotFound_PostgreSql() => RunGetByIdNotFound(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("GetById returns null for a nonexistent company on SQLite")]
        public void GetById_NotFound_Sqlite() => RunGetByIdNotFound(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("GetById returns null for a nonexistent company on MySQL")]
        public void GetById_NotFound_MySql() => RunGetByIdNotFound(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("GetById returns null for a nonexistent company on Oracle")]
        public void GetById_NotFound_Oracle() => RunGetByIdNotFound(DatabaseType.Oracle);

        #endregion

        #region GetById — Disabled (filtered at query layer)

        private void RunGetByIdDisabled(DatabaseType dbType)
        {
            // Create a disabled company; the query must return null. This sub-test seeds with raw SQL to avoid
            // depending on an admin API that does not exist yet. The company ID has a random suffix so leftovers in
            // the provider databases do not pollute later runs.
            var dbAccess = _fx.NewDbAccess(TestDbConventions.GetDatabaseId(dbType, DbCategoryIds.Common));
            var companyId = string.Concat("DISABLED_", Guid.NewGuid().ToString("N").AsSpan(0, 8));
            string tbl = dbType.QuoteIdentifier("st_company");
            string colRowId = dbType.QuoteIdentifier("sys_rowid");
            string colId = dbType.QuoteIdentifier("sys_id");
            string colName = dbType.QuoteIdentifier("sys_name");
            string colDbId = dbType.QuoteIdentifier("company_database_id");
            string colNumFmt = dbType.QuoteIdentifier("number_formats_xml");
            string colDefCur = dbType.QuoteIdentifier("default_currency");
            string colCashRnd = dbType.QuoteIdentifier("cash_rounding_xml");
            string colAllowCur = dbType.QuoteIdentifier("allowed_currencies_xml");
            string colEnabled = dbType.QuoteIdentifier("enabled");
            string colInsTime = dbType.QuoteIdentifier("sys_insert_time");
            string nowExpr = dbType == DatabaseType.PostgreSQL || dbType == DatabaseType.SQLite ? "CURRENT_TIMESTAMP"
                           : dbType == DatabaseType.SQLServer ? "GETDATE()"
                           : dbType == DatabaseType.MySQL ? "CURRENT_TIMESTAMP(6)"
                           : "SYSTIMESTAMP";
            string disabledLiteral = dbType == DatabaseType.PostgreSQL ? "FALSE" : "0";

            // number_formats_xml / cash_rounding_xml / allowed_currencies_xml are NOT NULL Text columns;
            // MySQL TEXT columns can't carry a DEFAULT, so they must be supplied explicitly (empty strings)
            // rather than relying on a DB-side default. default_currency gets USD, as every company must.
            var insert = new DbCommandSpec(DbCommandKind.NonQuery,
                $"INSERT INTO {tbl} ({colRowId}, {colId}, {colName}, {colDbId}, {colNumFmt}, {colDefCur}, {colCashRnd}, {colAllowCur}, {colEnabled}, {colInsTime}) " +
                $"VALUES ({{0}}, {{1}}, {{2}}, {{3}}, {{4}}, {{5}}, {{6}}, {{7}}, {disabledLiteral}, {nowExpr})",
                Guid.NewGuid(), companyId, "停用測試公司", "common", string.Empty, "USD", string.Empty, string.Empty);
            dbAccess.Execute(insert);

            try
            {
                var repo = CreateRepo(dbType);
                var result = repo.GetById(companyId);
                Assert.Null(result);
            }
            finally
            {
                var cleanup = new DbCommandSpec(DbCommandKind.NonQuery,
                    $"DELETE FROM {tbl} WHERE {colId} = {{0}}", companyId);
                dbAccess.Execute(cleanup);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("GetById returns null for a disabled company on SQL Server (filtered at the query layer)")]
        public void GetById_Disabled_SqlServer() => RunGetByIdDisabled(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("GetById returns null for a disabled company on PostgreSQL (filtered at the query layer)")]
        public void GetById_Disabled_PostgreSql() => RunGetByIdDisabled(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("GetById returns null for a disabled company on SQLite (filtered at the query layer)")]
        public void GetById_Disabled_Sqlite() => RunGetByIdDisabled(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("GetById returns null for a disabled company on MySQL (filtered at the query layer)")]
        public void GetById_Disabled_MySql() => RunGetByIdDisabled(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("GetById returns null for a disabled company on Oracle (filtered at the query layer)")]
        public void GetById_Disabled_Oracle() => RunGetByIdDisabled(DatabaseType.Oracle);

        #endregion
    }
}
