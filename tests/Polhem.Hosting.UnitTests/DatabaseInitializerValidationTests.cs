using System.ComponentModel;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Hosting.Database;
using Polhem.ObjectCaching;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// The checks <see cref="DatabaseInitializer"/> makes before it opens any connection. The database
    /// items point at an address that does not resolve, so a test that reached the engine would fail
    /// with a <see cref="System.Data.Common.DbException"/> instead of the expected outcome.
    /// </summary>
    public sealed class DatabaseInitializerValidationTests : IClassFixture<Polhem.Tests.Shared.SharedDbFixture>, IDisposable
    {
        private readonly string _definePath = Path.Combine(Path.GetTempPath(), $"polhem-init-check-{Guid.NewGuid():N}");
        private readonly CacheDefineAccess _access;
        private readonly DbConnectionManagerService _connectionManager;
        private readonly DatabaseInitializer _initializer;

        public DatabaseInitializerValidationTests()
        {
            Defaults.MaterializeTo(_definePath);
            var paths = new PathOptions { DefinePath = _definePath };
            var storage = new FileDefineStorage(paths);
            _access = new CacheDefineAccess(storage, paths, new CacheContainerService(storage, paths, $"init-check-{Guid.NewGuid():N}"), []);
            _connectionManager = new DbConnectionManagerService(new DefineAccessDatabaseSettingsProvider(_access));
            _initializer = new DatabaseInitializer(_access, _connectionManager, new DbAccessFactory(_connectionManager));
        }

        public void Dispose()
        {
            _connectionManager.Dispose();
            try { Directory.Delete(_definePath, recursive: true); } catch (IOException) { /* best effort */ }
        }

        [Theory]
        [InlineData("admin", "short", "ACME", "USD", "")]
        [InlineData("a_user_id_longer_than_twenty", "Long-Enough-1", "ACME", "USD", "")]
        [InlineData("admin", "Long-Enough-1", "", "USD", "")]
        [InlineData("admin", "Long-Enough-1", "ACME", "XXX", "")]
        [InlineData("admin", "Long-Enough-1", "ACME", "USD", "common")]
        [InlineData("admin", "Long-Enough-1", "ACME", "", "")]
        [InlineData("admin", "Long-Enough-1", "ACME", "USD", "nowhere")]
        [DisplayName("SeedAdministrator rejects an invalid seed before connecting")]
        public void SeedAdministrator_InvalidSeed_Throws(
            string userId, string password, string companyId, string currency, string companyDatabaseId)
        {
            SaveItems(("common", "common"), ("company", "company"));
            var seed = new AdministratorSeed
            {
                UserId = userId,
                Password = password,
                CompanyId = companyId,
                DefaultCurrency = currency,
                CompanyDatabaseId = companyDatabaseId,
            };

            var exception = Assert.Throws<ArgumentException>(() => _initializer.SeedAdministrator(seed));
            Assert.DoesNotContain(password, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("SeedAdministrator refuses a deployment with no company database")]
        public void SeedAdministrator_NoCompanyDatabase_Throws()
        {
            SaveItems(("common", "common"));
            var seed = new AdministratorSeed { Password = "Long-Enough-1", CompanyId = "ACME" };

            var exception = Assert.Throws<ArgumentException>(() => _initializer.SeedAdministrator(seed));
            Assert.Contains("No database item", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("SeedAdministrator asks for the company database when the company category has several")]
        public void SeedAdministrator_SeveralCompanyDatabases_RequiresChoice()
        {
            SaveItems(("common", "common"), ("company_a", "company"), ("company_b", "company"));
            var seed = new AdministratorSeed { Password = "Long-Enough-1", CompanyId = "ACME" };

            var exception = Assert.Throws<ArgumentException>(() => _initializer.SeedAdministrator(seed));
            Assert.Contains("company_a, company_b", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("UpgradeTables reports a category with tables that no database item belongs to")]
        public void UpgradeTables_CategoryWithoutDatabase_ReportsProblem()
        {
            SaveItems();

            var report = _initializer.UpgradeTables();

            Assert.False(report.Succeeded);
            Assert.Empty(report.Tables);
            Assert.Contains(report.Problems, p => p.Contains("'log' category", StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("CreateDatabases reports an unsafe database name as a failed item")]
        public void CreateDatabases_UnsafeName_ReportsError()
        {
            var settings = new DatabaseSettings();
            settings.Items!.Add(new DatabaseItem
            {
                Id = "common",
                CategoryId = "common",
                DatabaseType = DatabaseType.SQLServer,
                ConnectionString = "Data Source=invalid.invalid;Initial Catalog={@DbName};User ID=sa",
                DbName = "bad-name",
            });
            _access.SaveDatabaseSettings(settings);

            var entry = Assert.Single(_initializer.CreateDatabases());

            Assert.Null(entry.Result);
            Assert.Contains("bad-name", entry.Error, StringComparison.Ordinal);
        }

        private void SaveItems(params (string Id, string CategoryId)[] items)
        {
            var settings = new DatabaseSettings();
            foreach (var (id, categoryId) in items)
            {
                settings.Items!.Add(new DatabaseItem
                {
                    Id = id,
                    CategoryId = categoryId,
                    DatabaseType = DatabaseType.SQLServer,
                    ConnectionString = $"Data Source=invalid.invalid;Initial Catalog={id};User ID=sa",
                });
            }
            _access.SaveDatabaseSettings(settings);
        }
    }
}
