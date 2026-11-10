using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Core;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Db.Schema;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Hosting.Database;
using Polhem.ObjectCaching;
using Polhem.Tests.Shared;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// How <see cref="IDatabaseInitializer.UpgradeTables"/> readies an existing deployment for companies that
    /// share a company database: the checks it runs first, the company it gives rows that carry none, and
    /// the narrowing of <c>st_company.sys_id</c>. Run against SQLite files in a temporary directory.
    /// </summary>
    public sealed class DatabaseInitializerCompanyTests : IClassFixture<SharedDbFixture>, IDisposable
    {
        private const string DepartmentTable = "st_department";
        private readonly string _definePath = Path.Combine(Path.GetTempPath(), $"polhem-init-company-{Guid.NewGuid():N}");
        private readonly CacheDefineAccess _access;
        private readonly DbConnectionManagerService _connectionManager;
        private readonly DbAccessFactory _dbAccessFactory;
        private readonly DatabaseInitializer _initializer;

        public DatabaseInitializerCompanyTests(SharedDbFixture _)
        {
            Defaults.MaterializeTo(_definePath);
            var paths = new PathOptions { DefinePath = _definePath };
            var storage = new FileDefineStorage(paths);
            _access = new CacheDefineAccess(storage, paths, new CacheContainerService(storage, paths, $"init-company-{Guid.NewGuid():N}"), []);
            _connectionManager = new DbConnectionManagerService(new DefineAccessDatabaseSettingsProvider(_access));
            _dbAccessFactory = new DbAccessFactory(_connectionManager);
            _initializer = new DatabaseInitializer(_access, _connectionManager, _dbAccessFactory);
        }

        public void Dispose()
        {
            _connectionManager.Dispose();
            try { Directory.Delete(_definePath, recursive: true); } catch (IOException) { /* best effort */ }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Upgrading stops before any change while a company table schema has no company column, and names the command that adds it")]
        public void UpgradeTables_CompanyTableWithoutColumn_StopsBeforeChanging()
        {
            SaveSqliteItems();
            RemoveCompanyColumn(DepartmentTable);

            var report = _initializer.UpgradeTables();

            var problem = Assert.Single(report.Problems);
            Assert.Contains(DepartmentTable, problem, StringComparison.Ordinal);
            Assert.Contains("db add-company-column", problem, StringComparison.Ordinal);
            Assert.Empty(report.Tables);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("db add-company-column adds the company column and company-led indexes to the table schemas that lack them")]
        public void AddCompanyColumn_MissingColumn_AddsItAndUnblocksTheUpgrade()
        {
            SaveSqliteItems();
            RemoveCompanyColumn(DepartmentTable);
            var services = new ServiceCollection().AddSingleton<IDefineAccess>(_access).BuildServiceProvider();
            var output = new StringWriter();

            var exitCode = DatabaseCommandLine.TryRun(services, ["db", "add-company-column"], output, _ => null, _ => null);

            Assert.Equal(DatabaseCommandLine.Success, exitCode);
            Assert.Contains($"company.{DepartmentTable}: added", output.ToString(), StringComparison.Ordinal);
            Assert.True(_access.GetTableSchema(DbCategoryIds.Company, DepartmentTable).HasCompanyScope());
            Assert.True(_initializer.UpgradeTables().Succeeded);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Upgrading stops before any change while a company id is not in its stored form")]
        public void UpgradeTables_UppercaseCompanyId_Stops()
        {
            SaveSqliteItems();
            Assert.True(_initializer.UpgradeTables().Succeeded);
            InsertCompany("ACME", DbCategoryIds.Company);

            var report = _initializer.UpgradeTables();

            var problem = Assert.Single(report.Problems);
            Assert.Contains("ACME", problem, StringComparison.Ordinal);
            Assert.Empty(report.Tables);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Upgrading gives rows with no company the one company their database belongs to")]
        public void UpgradeTables_OneCompanyPerDatabase_AssignsRows()
        {
            SaveSqliteItems();
            Assert.True(_initializer.UpgradeTables().Succeeded);
            InsertCompany("acme", DbCategoryIds.Company);
            var rowId = InsertDepartmentWithoutCompany();

            var report = _initializer.UpgradeTables();

            Assert.True(report.Succeeded, string.Join("; ", report.Problems));
            var assignment = Assert.Single(report.CompanyAssignments);
            Assert.Equal(DepartmentTable, assignment.TableName);
            Assert.Equal("acme", assignment.CompanyId);
            Assert.Equal(1, assignment.Rows);
            Assert.Equal("acme", StoredCompany(rowId));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A dry run reports the rows it would give a company and leaves them unchanged")]
        public void UpgradeTables_DryRun_ReportsWithoutAssigning()
        {
            SaveSqliteItems();
            Assert.True(_initializer.UpgradeTables().Succeeded);
            InsertCompany("acme", DbCategoryIds.Company);
            var rowId = InsertDepartmentWithoutCompany();

            var report = _initializer.UpgradeTables(dryRun: true);

            Assert.Equal(1, Assert.Single(report.CompanyAssignments).Rows);
            Assert.Equal(string.Empty, StoredCompany(rowId));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Rows with no company in a database several companies share are reported and left alone")]
        public void UpgradeTables_SharedDatabase_ReportsRowsWithoutCompany()
        {
            SaveSqliteItems();
            Assert.True(_initializer.UpgradeTables().Succeeded);
            InsertCompany("acme", DbCategoryIds.Company);
            InsertCompany("globex", DbCategoryIds.Company);
            var rowId = InsertDepartmentWithoutCompany();

            var report = _initializer.UpgradeTables();

            Assert.Contains(report.Problems, p => p.Contains(DepartmentTable, StringComparison.Ordinal)
                && p.Contains("acme", StringComparison.Ordinal) && p.Contains("globex", StringComparison.Ordinal));
            Assert.Empty(report.CompanyAssignments);
            Assert.Equal(string.Empty, StoredCompany(rowId));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A company's database id matches its database item without regard to case, as the connection lookup does")]
        public void UpgradeTables_DatabaseIdInAnotherCase_AssignsRows()
        {
            SaveSqliteItems();
            Assert.True(_initializer.UpgradeTables().Succeeded);
            InsertCompany("acme", "COMPANY");
            var rowId = InsertDepartmentWithoutCompany();

            var report = _initializer.UpgradeTables();

            Assert.True(report.Succeeded, string.Join("; ", report.Problems));
            Assert.Equal("acme", StoredCompany(rowId));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A dry run on a table that has no company column yet counts every row as one the upgrade would give a company")]
        public void UpgradeTables_DryRunBeforeColumnExists_CountsEveryRow()
        {
            SaveSqliteItems();
            Assert.True(_initializer.UpgradeTables().Succeeded);
            var scoped = _access.GetTableSchema(DbCategoryIds.Company, DepartmentTable).Clone();
            RemoveCompanyColumn(DepartmentTable);
            new TableSchemaBuilder(DbCategoryIds.Company, _access, _connectionManager)
                .Execute(DbCategoryIds.Company, DepartmentTable, new UpgradeOptions { AllowColumnNarrowing = true });
            _access.SaveTableSchema(DbCategoryIds.Company, scoped);
            InsertCompany("acme", DbCategoryIds.Company);
            _dbAccessFactory.Create(DbCategoryIds.Company).ExecuteNonQuery(
                $"INSERT INTO {DepartmentTable} (sys_rowid, sys_id, sys_name) VALUES ({{0}}, {{1}}, {{2}})",
                Guid.NewGuid(), "D01", "Sales");

            var report = _initializer.UpgradeTables(dryRun: true);

            Assert.True(report.Succeeded, string.Join("; ", report.Problems));
            Assert.Equal(1, Assert.Single(report.CompanyAssignments).Rows);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Upgrading narrows st_company.sys_id to the company id's width without --allow-narrowing")]
        public void UpgradeTables_WideCompanyIdColumn_NarrowsIt()
        {
            SaveSqliteItems();
            var narrow = _access.GetTableSchema(DbCategoryIds.Common, "st_company").Clone();
            var wide = narrow.Clone();
            wide.Fields![SysFields.Id].Length = 20;
            _access.SaveTableSchema(DbCategoryIds.Common, wide);
            Assert.True(_initializer.UpgradeTables().Succeeded);
            _access.SaveTableSchema(DbCategoryIds.Common, narrow);

            var report = _initializer.UpgradeTables();

            Assert.True(report.Succeeded, string.Join("; ", report.Problems.Concat(report.Tables.Select(t => t.Error ?? string.Empty))));
            Assert.Equal(TableUpgradeOutcome.Upgraded, report.Tables.Single(t => t.TableName == "st_company").Outcome);
        }

        private void RemoveCompanyColumn(string tableName)
        {
            var schema = _access.GetTableSchema(DbCategoryIds.Company, tableName).Clone();
            schema.Fields!.Remove(SysFields.CompanyId);
            foreach (var index in schema.Indexes!)
                index.IndexFields!.Remove(SysFields.CompanyId);
            _access.SaveTableSchema(DbCategoryIds.Company, schema);
        }

        private void InsertCompany(string companyId, string databaseId)
            => _dbAccessFactory.Create(DbCategoryIds.Common).ExecuteNonQuery(
                "INSERT INTO st_company (sys_rowid, sys_id, sys_name, company_database_id, default_currency) " +
                "VALUES ({0}, {1}, {2}, {3}, {4})",
                Guid.NewGuid(), companyId, companyId, databaseId, "USD");

        private Guid InsertDepartmentWithoutCompany()
        {
            var rowId = Guid.NewGuid();
            _dbAccessFactory.Create(DbCategoryIds.Company).ExecuteNonQuery(
                $"INSERT INTO {DepartmentTable} (sys_rowid, sys_id, sys_name) VALUES ({{0}}, {{1}}, {{2}})",
                rowId, "D01", "Sales");
            return rowId;
        }

        private string StoredCompany(Guid rowId)
            => ValueUtilities.CStr(_dbAccessFactory.Create(DbCategoryIds.Company).ExecuteScalar(
                $"SELECT {SysFields.CompanyId} FROM {DepartmentTable} WHERE sys_rowid = {{0}}", rowId) ?? DBNull.Value);

        private void SaveSqliteItems()
        {
            var settings = new DatabaseSettings();
            foreach (var category in new[] { DbCategoryIds.Common, DbCategoryIds.Company, DbCategoryIds.Log })
                settings.Items!.Add(SqliteItem(category));
            _access.SaveDatabaseSettings(settings);
            Assert.All(_initializer.CreateDatabases(), entry => Assert.Equal(DatabaseCreateResult.Created, entry.Result));
        }

        private DatabaseItem SqliteItem(string category) => new()
        {
            Id = category,
            CategoryId = category,
            DatabaseType = DatabaseType.SQLite,
            ConnectionString = $"Data Source={Path.Combine(_definePath, "data", category + ".db")};Pooling=False",
        };
    }
}
