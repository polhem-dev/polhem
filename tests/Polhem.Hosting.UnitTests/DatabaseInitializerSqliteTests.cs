using System.ComponentModel;
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
            _initializer = new DatabaseInitializer(_access, _connectionManager, () => TestRepositoryFactory.Create(_access, _connectionManager));
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

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("CheckConnections opens each database and reports a broken item without stopping at it")]
        public void CheckConnections_BrokenItem_ReportsItAndContinues()
        {
            SaveSqliteItems();
            var settings = _access.GetDatabaseSettings();
            settings.Items!.Add(new DatabaseItem { Id = "orphan", DatabaseType = DatabaseType.SQLite, ServerId = "missing" });
            _access.SaveDatabaseSettings(settings);

            var entries = _initializer.CheckConnections();

            Assert.NotNull(entries.Single(e => e.DatabaseId == "orphan").Error);
            Assert.All(entries.Where(e => e.DatabaseId != "orphan"), e => Assert.True(e.Succeeded, e.Error));
            Assert.Equal(4, entries.Count);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Upgrading records each table it builds, and a second upgrade changes nothing")]
        public void UpgradeTables_RecordsEachBuiltTable()
        {
            SaveSqliteItems();

            var report = _initializer.UpgradeTables();

            Assert.True(report.Succeeded);
            var records = new TableUpgradeRecordStore(DbCategoryIds.Common, _connectionManager);
            var record = records.GetRecord("st_user");
            Assert.NotNull(record);
            Assert.Equal(TableUpgradeStatus.Succeeded, record.Status);
            Assert.Equal(SchemaFormat.Current, record.FormatVersion);
            Assert.Equal(SchemaFormat.Current, records.GetHighestFormatVersion());
            Assert.All(_initializer.UpgradeTables().Tables, t => Assert.Equal(TableUpgradeOutcome.Unchanged, t.Outcome));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A database upgraded by a newer schema format is reported once and none of its tables is touched")]
        public void UpgradeTables_NewerFormat_ReportedOncePerDatabase()
        {
            SaveSqliteItems();
            Assert.True(_initializer.UpgradeTables().Succeeded);
            Sql(DbCategoryIds.Log, "UPDATE st_table_upgrade SET format_version = format_version + 1");

            foreach (bool dryRun in new[] { true, false })
            {
                var report = _initializer.UpgradeTables(dryRun: dryRun);

                var problem = Assert.Single(report.Problems);
                Assert.Contains($"'{DbCategoryIds.Log}'", problem, StringComparison.Ordinal);
                Assert.Contains($"schema format {SchemaFormat.Current + 1}", problem, StringComparison.Ordinal);
                Assert.DoesNotContain(report.Tables, t => t.DatabaseId == DbCategoryIds.Log);
                Assert.Contains(report.Tables, t => t.DatabaseId == DbCategoryIds.Common);
                Assert.False(report.Succeeded);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A fast compare takes a table its record calls current as unchanged without reading the database, and a full compare finds it missing")]
        public void UpgradeTables_FastCompare_TrustsRecordsUntilFullCompare()
        {
            SaveSqliteItems();
            Assert.True(_initializer.UpgradeTables().Succeeded);
            Sql(DbCategoryIds.Common, "DROP TABLE st_session");

            var fast = _initializer.UpgradeTables();

            Assert.Equal(TableUpgradeOutcome.Unchanged, Outcome(fast, "st_session"));
            Assert.False(TableExists(DbCategoryIds.Common, "st_session"));

            var full = _initializer.UpgradeTables(new UpgradeOptions { CompareMode = UpgradeCompareMode.Full });

            Assert.Equal(TableUpgradeOutcome.Created, Outcome(full, "st_session"));
            Assert.True(TableExists(DbCategoryIds.Common, "st_session"));
        }

        [DbTheory(DatabaseType.SQLite)]
        [InlineData("status = 'Failed'")]
        [InlineData("status = 'Upgrading'")]
        [InlineData("format_version = 0")]
        [InlineData("schema_hash = 'stale'")]
        [DisplayName("A fast compare compares with the database when the record is not a success of this format and definition")]
        public void UpgradeTables_FastCompare_FallsBackToFullCompare(string recordChange)
        {
            SaveSqliteItems();
            Assert.True(_initializer.UpgradeTables().Succeeded);
            Sql(DbCategoryIds.Common, $"UPDATE st_table_upgrade SET {recordChange} WHERE table_name = 'st_session'");
            Sql(DbCategoryIds.Common, "DROP TABLE st_session");

            var report = _initializer.UpgradeTables();

            Assert.Equal(TableUpgradeOutcome.Created, Outcome(report, "st_session"));
            Assert.True(TableExists(DbCategoryIds.Common, "st_session"));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A table found to match its definition gets a record, and a dry run writes none")]
        public void UpgradeTables_UnchangedTable_IsRecordedExceptInADryRun()
        {
            SaveSqliteItems();
            Assert.True(_initializer.UpgradeTables().Succeeded);
            Sql(DbCategoryIds.Common, "DELETE FROM st_table_upgrade WHERE table_name = 'st_session'");
            var records = () => new TableUpgradeRecordStore(DbCategoryIds.Common, _connectionManager);

            Assert.Equal(TableUpgradeOutcome.Unchanged, Outcome(_initializer.UpgradeTables(dryRun: true), "st_session"));
            Assert.Null(records().GetRecord("st_session"));

            Assert.Equal(TableUpgradeOutcome.Unchanged, Outcome(_initializer.UpgradeTables(), "st_session"));
            Assert.Equal(TableUpgradeStatus.Verified, records().GetRecord("st_session")?.Status);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("An upgrade that changes no table leaves no record that refuses an older tool")]
        public void UpgradeTables_NothingChanged_DoesNotRefuseOlderTool()
        {
            SaveSqliteItems();
            Assert.True(_initializer.UpgradeTables().Succeeded);
            // As if every table had been built by a tool of format 0, before the records existed.
            Sql(DbCategoryIds.Common, "DELETE FROM st_table_upgrade");

            Assert.True(_initializer.UpgradeTables().Succeeded);

            var records = new TableUpgradeRecordStore(DbCategoryIds.Common, _connectionManager);
            Assert.Equal(TableUpgradeStatus.Verified, records.GetRecord("st_user")?.Status);
            Assert.Equal(0, records.GetHighestFormatVersion());
            Assert.Null(records.DescribeNewerFormat());
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("The report says which tables their records called unchanged and what state each record was in before the run")]
        public void UpgradeTables_ReportsRecordState()
        {
            SaveSqliteItems();
            var first = _initializer.UpgradeTables();
            Assert.All(first.Tables, t => Assert.Null(t.PreviousStatus));
            Sql(DbCategoryIds.Common, "UPDATE st_table_upgrade SET status = 'Failed' WHERE table_name = 'st_session'");

            var report = _initializer.UpgradeTables();

            var user = report.Tables.Single(t => t.TableName == "st_user");
            Assert.True(user.UnchangedByRecord);
            Assert.Equal(TableUpgradeStatus.Succeeded, user.PreviousStatus);
            var session = report.Tables.Single(t => t.TableName == "st_session");
            Assert.False(session.UnchangedByRecord);
            Assert.Equal(TableUpgradeOutcome.Unchanged, session.Outcome);
            Assert.Equal(TableUpgradeStatus.Failed, session.PreviousStatus);
            Assert.All(_initializer.UpgradeTables(new UpgradeOptions { CompareMode = UpgradeCompareMode.Full }).Tables,
                t => Assert.False(t.UnchangedByRecord));
        }

        private static TableUpgradeOutcome Outcome(TableUpgradeReport report, string tableName)
            => report.Tables.Single(t => t.TableName == tableName).Outcome;

        private void Sql(string databaseId, string sql)
        {
            using var connection = _connectionManager.CreateConnection(databaseId);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        private bool TableExists(string databaseId, string tableName)
            => DbDialectRegistry.Get(DatabaseType.SQLite)
                .CreateTableSchemaProvider(databaseId, _connectionManager)
                .GetTableSchema(tableName) is not null;

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
