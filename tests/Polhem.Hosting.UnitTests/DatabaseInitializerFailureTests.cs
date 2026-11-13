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
    /// How <see cref="DatabaseInitializer"/> reports a database it cannot reach, cannot parse or has no server for:
    /// as a failed entry or a problem of its own, never as an exception that stops the other databases. Run against
    /// SQLite files in a temporary directory.
    /// </summary>
    public sealed class DatabaseInitializerFailureTests : IClassFixture<SharedDbFixture>, IDisposable
    {
        private const string LogTable = "st_log_login";
        private readonly string _definePath = Path.Combine(Path.GetTempPath(), $"polhem-init-failure-{Guid.NewGuid():N}");
        private readonly CacheDefineAccess _access;
        private readonly DbConnectionManagerService _connectionManager;
        private readonly DatabaseInitializer _initializer;

        public DatabaseInitializerFailureTests(SharedDbFixture _)
        {
            Defaults.MaterializeTo(_definePath);
            var paths = new PathOptions { DefinePath = _definePath };
            var storage = new FileDefineStorage(paths);
            _access = new CacheDefineAccess(storage, paths, new CacheContainerService(storage, paths, $"init-failure-{Guid.NewGuid():N}"), []);
            _connectionManager = new DbConnectionManagerService(new DefineAccessDatabaseSettingsProvider(_access));
            _initializer = new DatabaseInitializer(_access, _connectionManager, () => TestRepositoryFactory.Create(_access, _connectionManager));
        }

        public void Dispose()
        {
            _connectionManager.Dispose();
            try { Directory.Delete(_definePath, recursive: true); } catch (IOException) { /* best effort */ }
        }

        [Fact]
        [DisplayName("The constructor rejects a null define access, connection manager or repository factory")]
        public void Constructor_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new DatabaseInitializer(null!, _connectionManager, () => null!));
            Assert.Throws<ArgumentNullException>(() => new DatabaseInitializer(_access, null!, () => null!));
            Assert.Throws<ArgumentNullException>(() => new DatabaseInitializer(_access, _connectionManager, null!));
        }

        [Fact]
        [DisplayName("CreateDatabases and CheckConnections return no entries when the settings have no database items")]
        public void CreateAndCheck_NoItems_ReturnEmpty()
        {
            _access.SaveDatabaseSettings(new DatabaseSettings());

            Assert.Empty(_initializer.CreateDatabases());
            Assert.Empty(_initializer.CheckConnections());
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("CheckConnections reports a malformed connection string and an unopenable file as failed items")]
        public void CheckConnections_MalformedAndUnopenable_ReportsBoth()
        {
            var settings = new DatabaseSettings();
            settings.Items!.Add(new DatabaseItem { Id = "malformed", DatabaseType = DatabaseType.SQLite, ConnectionString = "Data Source" });
            settings.Items.Add(UnopenableItem("unopenable", "unopenable"));
            _access.SaveDatabaseSettings(settings);

            var entries = _initializer.CheckConnections();

            Assert.False(string.IsNullOrEmpty(entries.Single(e => e.DatabaseId == "malformed").Error));
            Assert.False(string.IsNullOrEmpty(entries.Single(e => e.DatabaseId == "unopenable").Error));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("UpgradeTables fails each table of a log database it cannot parse or open and still builds the others")]
        public void UpgradeTables_BrokenLogDatabases_FailTheirTablesOnly()
        {
            var settings = new DatabaseSettings();
            settings.Items!.Add(SqliteItem(DbCategoryIds.Common));
            settings.Items.Add(SqliteItem(DbCategoryIds.Company));
            _access.SaveDatabaseSettings(settings);
            _initializer.CreateDatabases();
            settings = _access.GetDatabaseSettings().Clone();
            settings.Items!.Add(new DatabaseItem
            {
                Id = "log_malformed", CategoryId = DbCategoryIds.Log, DatabaseType = DatabaseType.SQLite, ConnectionString = "Data Source",
            });
            settings.Items.Add(UnopenableItem("log_unopenable", DbCategoryIds.Log));
            _access.SaveDatabaseSettings(settings);

            var report = _initializer.UpgradeTables(tableNames: [LogTable, "st_user"]);

            Assert.Equal(TableUpgradeOutcome.Failed, report.Tables.Single(t => t.DatabaseId == "log_malformed").Outcome);
            var unopenable = report.Tables.Single(t => t.DatabaseId == "log_unopenable");
            Assert.Equal(TableUpgradeOutcome.Failed, unopenable.Outcome);
            Assert.False(string.IsNullOrEmpty(unopenable.Error));
            Assert.Equal(TableUpgradeOutcome.Created, report.Tables.Single(t => t.TableName == "st_user").Outcome);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("UpgradeTables fails the tables of a common database whose server is missing, naming the engine")]
        public void UpgradeTables_CommonWithMissingServer_FailsItsTables()
        {
            var settings = new DatabaseSettings();
            settings.Items!.Add(new DatabaseItem
            {
                Id = DbCategoryIds.Common, CategoryId = DbCategoryIds.Common, DatabaseType = DatabaseType.SQLite, ServerId = "missing",
            });
            settings.Items.Add(SqliteItem(DbCategoryIds.Company));
            _access.SaveDatabaseSettings(settings);

            var report = _initializer.UpgradeTables(tableNames: ["st_user"]);

            var entry = Assert.Single(report.Tables);
            Assert.Equal(TableUpgradeOutcome.Failed, entry.Outcome);
            Assert.Contains(nameof(DatabaseType.SQLite), entry.Error, StringComparison.Ordinal);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("UpgradeTables fails the tables of a common database it cannot open and does not stop at reading its companies")]
        public void UpgradeTables_UnopenableCommon_FailsItsTables()
        {
            var settings = new DatabaseSettings();
            settings.Items!.Add(UnopenableItem(DbCategoryIds.Common, DbCategoryIds.Common));
            settings.Items.Add(SqliteItem(DbCategoryIds.Company));
            _access.SaveDatabaseSettings(settings);

            var report = _initializer.UpgradeTables(tableNames: ["st_user"]);

            Assert.Equal(TableUpgradeOutcome.Failed, Assert.Single(report.Tables).Outcome);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A company whose database has no server is reported as a problem when its rows are given their company")]
        public void UpgradeTables_CompanyDatabaseWithMissingServer_ReportsAssignmentProblem()
        {
            BuildWithCompanyIn("orphan_company");
            var settings = _access.GetDatabaseSettings().Clone();
            settings.Items!.Add(new DatabaseItem
            {
                Id = "orphan_company", CategoryId = DbCategoryIds.Company, DatabaseType = DatabaseType.SQLite, ServerId = "missing",
            });
            _access.SaveDatabaseSettings(settings);

            var report = _initializer.UpgradeTables();

            Assert.Contains(report.Problems, p => p.Contains("orphan_company their company", StringComparison.Ordinal));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A company whose database cannot be opened is reported as a problem when its rows are given their company")]
        public void UpgradeTables_UnopenableCompanyDatabase_ReportsAssignmentProblem()
        {
            BuildWithCompanyIn("dead_company");
            var settings = _access.GetDatabaseSettings().Clone();
            settings.Items!.Add(UnopenableItem("dead_company", DbCategoryIds.Company));
            _access.SaveDatabaseSettings(settings);

            var report = _initializer.UpgradeTables();

            Assert.Contains(report.Problems, p => p.Contains("dead_company their company", StringComparison.Ordinal));
        }

        /// <summary>Builds the common, company and log databases, then adds a company that names <paramref name="databaseId"/>.</summary>
        private void BuildWithCompanyIn(string databaseId)
        {
            var settings = new DatabaseSettings();
            foreach (var category in new[] { DbCategoryIds.Common, DbCategoryIds.Company, DbCategoryIds.Log })
                settings.Items!.Add(SqliteItem(category));
            _access.SaveDatabaseSettings(settings);
            _initializer.CreateDatabases();
            Assert.True(_initializer.UpgradeTables().Succeeded);

            new DbAccessFactory(_connectionManager).Create(DbCategoryIds.Common).ExecuteNonQuery(
                "INSERT INTO st_company (sys_rowid, sys_id, sys_name, company_database_id, default_currency) " +
                "VALUES ({0}, {1}, {2}, {3}, {4})",
                Guid.NewGuid(), "acme", "Acme", databaseId, "USD");
        }

        private DatabaseItem SqliteItem(string category) => new()
        {
            Id = category,
            CategoryId = category,
            DatabaseType = DatabaseType.SQLite,
            ConnectionString = $"Data Source={Path.Combine(_definePath, "data", category + ".db")};Pooling=False",
        };

        /// <summary>A SQLite item whose file is opened read-only in a directory that does not exist, so opening it fails.</summary>
        private DatabaseItem UnopenableItem(string id, string category) => new()
        {
            Id = id,
            CategoryId = category,
            DatabaseType = DatabaseType.SQLite,
            ConnectionString = $"Data Source={Path.Combine(_definePath, "absent", id + ".db")};Mode=ReadOnly;Pooling=False",
        };
    }
}
