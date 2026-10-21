using System.ComponentModel;
using Polhem.Db;
using Polhem.Db.Manager;
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
    /// The planning and failure paths of <see cref="DatabaseInitializer"/>, run against SQLite files in a
    /// temporary directory so each test starts from nothing.
    /// </summary>
    public sealed class DatabaseInitializerSqliteTests : IClassFixture<SharedDbFixture>, IDisposable
    {
        private readonly string _definePath = Path.Combine(Path.GetTempPath(), $"polhem-init-sqlite-{Guid.NewGuid():N}");
        private readonly CacheDefineAccess _access;
        private readonly DbConnectionManagerService _connectionManager;
        private readonly DatabaseInitializer _initializer;

        public DatabaseInitializerSqliteTests(SharedDbFixture _)
        {
            Defaults.MaterializeTo(_definePath);
            var paths = new PathOptions { DefinePath = _definePath };
            var storage = new FileDefineStorage(paths);
            _access = new CacheDefineAccess(storage, paths, new CacheContainerService(storage, paths, $"init-sqlite-{Guid.NewGuid():N}"), []);
            _connectionManager = new DbConnectionManagerService(new DefineAccessDatabaseSettingsProvider(_access));
            _initializer = new DatabaseInitializer(_access, _connectionManager, new DbAccessFactory(_connectionManager));
        }

        public void Dispose()
        {
            _connectionManager.Dispose();
            try { Directory.Delete(_definePath, recursive: true); } catch (IOException) { /* best effort */ }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A dry run plans every missing table with its statements and creates nothing")]
        public void UpgradeTables_DryRun_PlansWithoutCreating()
        {
            SaveSqliteItems();
            var completed = new List<TableUpgradeEntry>();

            var report = _initializer.UpgradeTables(dryRun: true, tableCompleted: completed.Add);

            Assert.True(report.Succeeded);
            Assert.All(report.Tables, t => Assert.Equal(TableUpgradeOutcome.Planned, t.Outcome));
            Assert.Contains("CREATE TABLE", report.Tables.First(t => t.TableName == "st_user").CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(report.Tables.Count, completed.Count);
            Assert.All(_initializer.UpgradeTables(dryRun: true).Tables, t => Assert.Equal(TableUpgradeOutcome.Planned, t.Outcome));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Naming tables processes only those, and a name no category registers is reported")]
        public void UpgradeTables_TableNames_ProcessesOnlyThose()
        {
            SaveSqliteItems();

            var report = _initializer.UpgradeTables(dryRun: true, tableNames: ["ST_USER", "st_company", "ft_missing"]);

            Assert.Equal(["st_company", "st_user"], report.Tables.Select(t => t.TableName).Order(StringComparer.Ordinal));
            var problem = Assert.Single(report.Problems);
            Assert.Contains("ft_missing", problem, StringComparison.Ordinal);
            Assert.False(report.Succeeded);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A registered table without a TableSchema fails on its own while the other tables are built")]
        public void UpgradeTables_MissingDefinition_FailsOnlyThatTable()
        {
            SaveSqliteItems();
            File.Delete(Path.Combine(_definePath, "TableSchema", "log", "st_log_login.TableSchema.xml"));

            var report = _initializer.UpgradeTables();

            Assert.False(report.Succeeded);
            var failed = Assert.Single(report.Tables, t => t.Outcome == TableUpgradeOutcome.Failed);
            Assert.Equal("st_log_login", failed.TableName);
            Assert.NotNull(failed.Error);
            Assert.Contains(report.Tables, t => t.TableName == "st_user" && t.Outcome == TableUpgradeOutcome.Created);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SeedAdministrator accepts a company database named explicitly")]
        public void SeedAdministrator_ExplicitCompanyDatabase_Seeds()
        {
            SaveSqliteItems();
            Assert.True(_initializer.UpgradeTables().Succeeded);

            var result = _initializer.SeedAdministrator(new AdministratorSeed
            {
                Password = "Long-Enough-1",
                CompanyId = "ACME",
                CompanyDatabaseId = DbCategoryIds.Company,
                DeploymentAdmin = false,
            });

            Assert.True(result.UserCreated);
            Assert.Equal(DbCategoryIds.Company, result.CompanyDatabaseId);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("CreateDatabases reports a malformed connection string and a missing server as failed items and continues")]
        public void CreateDatabases_BrokenItems_ReportsEachAndContinues()
        {
            var settings = new DatabaseSettings();
            settings.Items!.Add(new DatabaseItem { Id = "broken", DatabaseType = DatabaseType.SQLite, ConnectionString = "Data Source" });
            settings.Items.Add(new DatabaseItem { Id = "orphan", DatabaseType = DatabaseType.SQLite, ServerId = "missing" });
            settings.Items.Add(SqliteItem(DbCategoryIds.Common));
            _access.SaveDatabaseSettings(settings);

            var entries = _initializer.CreateDatabases();

            Assert.NotNull(entries.Single(e => e.DatabaseId == "broken").Error);
            Assert.NotNull(entries.Single(e => e.DatabaseId == "orphan").Error);
            Assert.Equal(DatabaseCreateResult.Created, entries.Single(e => e.DatabaseId == DbCategoryIds.Common).Result);
        }

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
