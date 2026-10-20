using System.ComponentModel;
using Polhem.Core;
using Polhem.Core.Security;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Settings;
using Polhem.Hosting.Database;
using Polhem.ObjectCaching;
using Polhem.Tests.Shared;
using Polhem.Definition.Storage;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// Runs the whole initialization against each engine, starting from databases that do not exist.
    /// </summary>
    /// <remarks>
    /// Each run works in databases of its own, named after a fresh suffix, and drops them afterwards.
    /// Oracle is the exception: its "database" is the test schema, which the shared fixture already
    /// built, so the run there checks that nothing changes and seeds rows it removes again.
    /// <see cref="SharedDbFixture"/> is used for the provider and dialect registrations only.
    /// </remarks>
    public class DatabaseInitializerDbTests : IClassFixture<SharedDbFixture>
    {
        private const string Password = "Initial-Pass-1";

        public DatabaseInitializerDbTests(SharedDbFixture _) { }

        private static readonly string[] s_categories = [DbCategoryIds.Common, DbCategoryIds.Company, DbCategoryIds.Log];

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Database initialization on SQL Server creates the databases, builds the tables and seeds the administrator")]
        public void Initialize_SqlServer_BuildsEverything() => RunInitialize(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("Database initialization on PostgreSQL creates the databases, builds the tables and seeds the administrator")]
        public void Initialize_PostgreSql_BuildsEverything() => RunInitialize(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("Database initialization on MySQL creates the databases, builds the tables and seeds the administrator")]
        public void Initialize_MySql_BuildsEverything() => RunInitialize(DatabaseType.MySQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Database initialization on SQLite creates the database files, builds the tables and seeds the administrator")]
        public void Initialize_Sqlite_BuildsEverything() => RunInitialize(DatabaseType.SQLite);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Database initialization on Oracle creates nothing, leaves the existing tables unchanged and seeds the administrator")]
        public void Initialize_Oracle_SeedsIntoExistingSchema() => RunInitialize(DatabaseType.Oracle);

        private static void RunInitialize(DatabaseType databaseType)
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var definePath = Path.Combine(Path.GetTempPath(), $"polhem-init-{suffix}");
            var template = Environment.GetEnvironmentVariable(TestDbConventions.GetConnectionStringEnvVar(databaseType))!;
            var databaseNames = s_categories.ToDictionary(c => c, c => $"polhem_init_{suffix}_{c}");
            var seed = new AdministratorSeed
            {
                UserId = $"admin_{suffix}",
                Password = Password,
                CompanyId = $"co_{suffix}",
                CompanyName = "Initial Company",
                DefaultCurrency = "TWD",
            };

            try
            {
                Defaults.MaterializeTo(definePath);
                if (databaseType == DatabaseType.Oracle)
                    CopyTestTableSchemas(definePath);

                var paths = new PathOptions { DefinePath = definePath };
                var storage = new FileDefineStorage(paths);
                var access = new CacheDefineAccess(
                    storage, paths, new CacheContainerService(storage, paths, $"init-{suffix}"), []);
                access.SaveDatabaseSettings(BuildSettings(databaseType, template, definePath, databaseNames));

                using var connectionManager = new DbConnectionManagerService(new DefineAccessDatabaseSettingsProvider(access));
                var initializer = new DatabaseInitializer(access, connectionManager, new DbAccessFactory(connectionManager));

                AssertCreate(databaseType, initializer);
                AssertUpgrade(databaseType, initializer);
                AssertSeed(databaseType, initializer, connectionManager, seed);
            }
            finally
            {
                CleanUp(databaseType, template, databaseNames.Values, definePath, seed);
            }
        }

        private static void AssertCreate(DatabaseType databaseType, DatabaseInitializer initializer)
        {
            var first = initializer.CreateDatabases();
            Assert.Equal(s_categories.Length, first.Count);
            Assert.All(first, entry => Assert.Null(entry.Error));
            var expected = databaseType == DatabaseType.Oracle ? DatabaseCreateResult.NotApplicable : DatabaseCreateResult.Created;
            Assert.All(first, entry => Assert.Equal(expected, entry.Result));

            var second = initializer.CreateDatabases();
            expected = databaseType == DatabaseType.Oracle ? DatabaseCreateResult.NotApplicable : DatabaseCreateResult.AlreadyExists;
            Assert.All(second, entry => Assert.Equal(expected, entry.Result));
        }

        private static void AssertUpgrade(DatabaseType databaseType, DatabaseInitializer initializer)
        {
            var first = initializer.UpgradeTables();
            Assert.True(first.Succeeded, string.Join(Environment.NewLine,
                first.Problems.Concat(first.Tables.Where(t => t.Error is not null).Select(t => $"{t.TableName}: {t.Error}"))));
            Assert.Contains(first.Tables, t => t.DatabaseId == DbCategoryIds.Common && t.TableName == "st_user");
            Assert.Contains(first.Tables, t => t.DatabaseId == DbCategoryIds.Company && t.TableName == "st_role");
            Assert.Contains(first.Tables, t => t.DatabaseId == DbCategoryIds.Log && t.TableName == "st_log_login");
            if (databaseType != DatabaseType.Oracle)
                Assert.All(first.Tables, t => Assert.Equal(TableUpgradeOutcome.Created, t.Outcome));

            var second = initializer.UpgradeTables();
            Assert.Empty(second.Tables.Where(t => t.Outcome != TableUpgradeOutcome.Unchanged)
                .Select(t => $"{t.DatabaseId}.{t.TableName}: {t.Outcome}"));

            var dryRun = initializer.UpgradeTables(dryRun: true);
            Assert.All(dryRun.Tables, t => Assert.Equal(TableUpgradeOutcome.Unchanged, t.Outcome));
        }

        private static void AssertSeed(
            DatabaseType databaseType, DatabaseInitializer initializer, DbConnectionManagerService connectionManager, AdministratorSeed seed)
        {
            var first = initializer.SeedAdministrator(seed);
            Assert.True(first.UserCreated);
            Assert.True(first.CompanyCreated);
            Assert.True(first.AccessGranted);
            Assert.Equal(DbCategoryIds.Company, first.CompanyDatabaseId);

            var dbAccess = new DbAccess(DbCategoryIds.Common, connectionManager);
            var user = ReadRow(databaseType, dbAccess, "st_user", seed.UserId);
            Assert.True(PasswordHasher.VerifyPassword(Password, ValueUtilities.CStr(user["password"])));
            Assert.True(ValueUtilities.CBool(user["deployment_admin"]));

            var company = ReadRow(databaseType, dbAccess, "st_company", seed.CompanyId);
            Assert.Equal(DbCategoryIds.Company, ValueUtilities.CStr(company["company_database_id"]));
            Assert.Equal("TWD", ValueUtilities.CStr(company["default_currency"]));
            Assert.True(ValueUtilities.CBool(company["enabled"]));

            // A second run with another password changes nothing, the password included.
            var again = initializer.SeedAdministrator(new AdministratorSeed
            {
                UserId = seed.UserId,
                Password = "Another-Pass-2",
                CompanyId = seed.CompanyId,
                DefaultCurrency = "TWD",
            });
            Assert.False(again.UserCreated);
            Assert.False(again.CompanyCreated);
            Assert.False(again.AccessGranted);
            user = ReadRow(databaseType, dbAccess, "st_user", seed.UserId);
            Assert.True(PasswordHasher.VerifyPassword(Password, ValueUtilities.CStr(user["password"])));
        }

        private static System.Data.DataRow ReadRow(DatabaseType databaseType, DbAccess dbAccess, string table, string sysId)
        {
            var sql = $"SELECT * FROM {databaseType.QuoteIdentifier(table)} WHERE {databaseType.QuoteIdentifier("sys_id")} = {{0}}";
            var rows = dbAccess.Execute(new DbCommandSpec(DbCommandKind.DataTable, sql, sysId)).Table!.Rows;
            Assert.Single(rows.Cast<System.Data.DataRow>());
            return rows[0];
        }

        private static DatabaseSettings BuildSettings(
            DatabaseType databaseType, string template, string definePath, Dictionary<string, string> databaseNames)
        {
            var settings = new DatabaseSettings();
            foreach (var category in s_categories)
            {
                var item = new DatabaseItem { Id = category, CategoryId = category, DatabaseType = databaseType };
                switch (databaseType)
                {
                    case DatabaseType.SQLite:
                        item.ConnectionString = $"Data Source={Path.Combine(definePath, "data", category + ".db")};Pooling=False";
                        break;
                    case DatabaseType.Oracle:
                        item.ConnectionString = template;
                        break;
                    default:
                        item.ConnectionString = template;
                        item.DbName = databaseNames[category];
                        break;
                }
                settings.Items!.Add(item);
            }
            return settings;
        }

        /// <summary>
        /// Uses the test suite's table definitions, so the shared Oracle schema already matches them.
        /// </summary>
        private static void CopyTestTableSchemas(string definePath)
        {
            var source = Path.Combine(RepoRoot.Find(), "tests", "Define", "TableSchema");
            foreach (var file in Directory.GetFiles(source, "*.TableSchema.xml", SearchOption.AllDirectories))
            {
                var target = Path.Combine(definePath, "TableSchema", Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
            }
        }

        private static void CleanUp(
            DatabaseType databaseType, string template, IEnumerable<string> databaseNames, string definePath, AdministratorSeed seed)
        {
            if (databaseType == DatabaseType.Oracle)
            {
                DeleteSeedRows(definePath, seed);
            }
            else if (databaseType != DatabaseType.SQLite)
            {
                foreach (var name in databaseNames)
                    TemporaryDatabase.Drop(databaseType, template, name);
            }
            try { Directory.Delete(definePath, recursive: true); } catch (IOException) { /* best effort */ }
        }

        private static void DeleteSeedRows(string definePath, AdministratorSeed seed)
        {
            if (!Directory.Exists(definePath)) return;
            var paths = new PathOptions { DefinePath = definePath };
            var storage = new FileDefineStorage(paths);
            var access = new CacheDefineAccess(storage, paths, new CacheContainerService(storage, paths, $"init-clean-{Guid.NewGuid():N}"), []);
            using var connectionManager = new DbConnectionManagerService(new DefineAccessDatabaseSettingsProvider(access));
            var dbAccess = new DbAccess(DbCategoryIds.Common, connectionManager);
            const DatabaseType oracle = DatabaseType.Oracle;
            string Q(string identifier) => oracle.QuoteIdentifier(identifier);

            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                $"DELETE FROM {Q("st_user_company")} WHERE {Q("user_rowid")} IN " +
                $"(SELECT {Q("sys_rowid")} FROM {Q("st_user")} WHERE {Q("sys_id")} = {{0}})", seed.UserId));
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                $"DELETE FROM {Q("st_user")} WHERE {Q("sys_id")} = {{0}}", seed.UserId));
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                $"DELETE FROM {Q("st_company")} WHERE {Q("sys_id")} = {{0}}", seed.CompanyId));
        }
    }
}
