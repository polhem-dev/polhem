using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Db.Schema;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Storage;
using Polhem.Hosting.Database;
using Polhem.ObjectCaching;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// The reports <see cref="DatabaseCommandLine"/> writes for the company assignments of <c>db upgrade</c>, the
    /// outcomes of <c>db seed-admin</c> and both runs of <c>db add-company-column</c>, plus its argument checks.
    /// </summary>
    public sealed class DatabaseCommandLineReportTests : IDisposable
    {
        private const string DepartmentTable = "st_department";

        private static readonly string[] s_upgrade = ["db", "upgrade"];
        private static readonly string[] s_upgradeDryRun = ["db", "upgrade", "--dry-run"];
        private static readonly string[] s_helpCommand = ["db", "help"];
        private static readonly string[] s_create = ["db", "create"];
        private static readonly string[] s_upgradeUnknown = ["db", "upgrade", "--bogus"];
        private static readonly string[] s_addCompanyColumn = ["db", "add-company-column"];
        private static readonly string[] s_addCompanyColumnDryRun = ["db", "add-company-column", "--dry-run"];
        private static readonly string[] s_seedFull =
        [
            "db", "seed-admin", "--company", "acme", "--company-name", "Acme Ltd", "--company-database", "company",
            "--user", "boss", "--name", "The Boss", "--currency", "EUR",
        ];

        private readonly string _definePath = Path.Combine(Path.GetTempPath(), $"polhem-cmdline-{Guid.NewGuid():N}");

        public void Dispose()
        {
            try { Directory.Delete(_definePath, recursive: true); } catch (IOException) { /* best effort */ }
        }

        [Fact]
        [DisplayName("TryRun rejects a null service provider or a null argument list")]
        public void TryRun_NullArguments_Throw()
        {
            var services = new ServiceCollection().BuildServiceProvider();

            Assert.Throws<ArgumentNullException>(() => DatabaseCommandLine.TryRun(null!, s_create, TextWriter.Null, _ => null, _ => null));
            Assert.Throws<ArgumentNullException>(() => DatabaseCommandLine.TryRun(services, null!, TextWriter.Null, _ => null, _ => null));
        }

        [Fact]
        [DisplayName("db help prints the usage and succeeds")]
        public void TryRun_HelpCommand_PrintsUsage()
        {
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.Success, Run(CreateServices(new FakeInitializer()), s_helpCommand, output));
            Assert.Contains("Usage:", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A NotSupportedException the initializer throws is reported as a failed command")]
        public void TryRun_InitializerNotSupported_ReturnsFailure()
        {
            var fake = new FakeInitializer { CreateException = new NotSupportedException("repository not supported") };
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.Failure, Run(CreateServices(fake), s_create, output));
            Assert.Contains("Failed: repository not supported", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("db upgrade reports the rows it gave a company")]
        public void TryRun_UpgradeWithAssignments_ReportsGivenRows()
        {
            var fake = new FakeInitializer { UpgradeReport = ReportWithAssignment() };
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.Success, Run(CreateServices(fake), s_upgrade, output));
            Assert.Contains("company.st_department: 3 rows given company 'acme'", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("db upgrade --dry-run reports the rows it would give a company and the planned count")]
        public void TryRun_UpgradeDryRunWithAssignments_ReportsWouldBeGiven()
        {
            var fake = new FakeInitializer { UpgradeReport = ReportWithAssignment() };
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.Success, Run(CreateServices(fake), s_upgradeDryRun, output));
            var text = output.ToString();
            Assert.Contains("3 rows would be given company 'acme'", text, StringComparison.Ordinal);
            Assert.Contains("0 planned", text, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("db upgrade with an unknown option is a usage error that runs nothing")]
        public void TryRun_UpgradeUnknownOption_ReturnsUsageError()
        {
            var fake = new FakeInitializer();
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.UsageError, Run(CreateServices(fake), s_upgradeUnknown, output));
            Assert.Contains("Unknown option '--bogus'", output.ToString(), StringComparison.Ordinal);
            Assert.False(fake.UpgradeCalled);
        }

        [Fact]
        [DisplayName("db seed-admin passes every option and reports existing user, company and access as left unchanged")]
        public void TryRun_SeedAdminAllOptions_ReportsExisting()
        {
            var fake = new FakeInitializer();
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.Success, Run(CreateServices(fake), s_seedFull, output));

            var seed = fake.LastSeed!;
            Assert.Equal("boss", seed.UserId);
            Assert.Equal("The Boss", seed.UserName);
            Assert.Equal("acme", seed.CompanyId);
            Assert.Equal("Acme Ltd", seed.CompanyName);
            Assert.Equal("company", seed.CompanyDatabaseId);
            Assert.Equal("EUR", seed.DefaultCurrency);
            Assert.True(seed.DeploymentAdmin);
            var text = output.ToString();
            Assert.Contains("user boss: exists, left unchanged", text, StringComparison.Ordinal);
            Assert.Contains("company acme (database company): exists, left unchanged", text, StringComparison.Ordinal);
            Assert.Contains("access boss -> acme: exists", text, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("db add-company-column says so and succeeds when every company table schema already has the column")]
        public void TryRun_AddCompanyColumnNothingMissing_SaysSo()
        {
            var access = CreateDefineAccess();
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.Success, Run(CreateServices(access), s_addCompanyColumn, output));
            Assert.Contains("Every company table schema already has the company column.", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("db add-company-column --dry-run lists what it would add and leaves the table schema unchanged")]
        public void TryRun_AddCompanyColumnDryRun_ListsWithoutSaving()
        {
            var access = CreateDefineAccess();
            RemoveCompanyColumn(access, DepartmentTable);
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.Success, Run(CreateServices(access), s_addCompanyColumnDryRun, output));
            var text = output.ToString();
            Assert.Contains($"company.{DepartmentTable}: would add the company column", text, StringComparison.Ordinal);
            Assert.Contains("table schemas would change", text, StringComparison.Ordinal);
            Assert.False(access.GetTableSchema(DbCategoryIds.Company, DepartmentTable).HasCompanyScope());
        }

        private static TableUpgradeReport ReportWithAssignment() => new()
        {
            CompanyAssignments =
            [
                new CompanyRowAssignment { DatabaseId = "company", TableName = DepartmentTable, CompanyId = "acme", Rows = 3 },
            ],
        };

        private CacheDefineAccess CreateDefineAccess()
        {
            Defaults.MaterializeTo(_definePath);
            var paths = new PathOptions { DefinePath = _definePath };
            var storage = new FileDefineStorage(paths);
            return new CacheDefineAccess(storage, paths, new CacheContainerService(storage, paths, $"cmdline-{Guid.NewGuid():N}"), []);
        }

        private static void RemoveCompanyColumn(IDefineAccess access, string tableName)
        {
            var schema = access.GetTableSchema(DbCategoryIds.Company, tableName).Clone();
            schema.Fields!.Remove(SysFields.CompanyId);
            foreach (var index in schema.Indexes!)
                index.IndexFields!.Remove(SysFields.CompanyId);
            access.SaveTableSchema(DbCategoryIds.Company, schema);
        }

        private static int? Run(IServiceProvider services, string[] args, TextWriter output)
            => DatabaseCommandLine.TryRun(services, args, output, _ => "Typed-Secret-123", _ => null);

        private static ServiceProvider CreateServices(IDatabaseInitializer initializer)
            => new ServiceCollection().AddSingleton(initializer).BuildServiceProvider();

        private static ServiceProvider CreateServices(IDefineAccess access)
            => new ServiceCollection().AddSingleton(access).BuildServiceProvider();

        private sealed class FakeInitializer : IDatabaseInitializer
        {
            public TableUpgradeReport UpgradeReport { get; set; } = new();

            public Exception? CreateException { get; set; }

            public bool UpgradeCalled { get; private set; }

            public AdministratorSeed? LastSeed { get; private set; }

            public IReadOnlyList<DatabaseCreateEntry> CreateDatabases()
                => CreateException is null ? [] : throw CreateException;

            public IReadOnlyList<DatabaseCheckEntry> CheckConnections() => [];

            public TableUpgradeReport UpgradeTables(
                UpgradeOptions? options = null, bool dryRun = false, Action<TableUpgradeEntry>? tableCompleted = null,
                IReadOnlyCollection<string>? tableNames = null)
            {
                UpgradeCalled = true;
                return UpgradeReport;
            }

            public AdministratorSeedResult SeedAdministrator(AdministratorSeed seed)
            {
                LastSeed = seed;
                return new AdministratorSeedResult { CompanyDatabaseId = seed.CompanyDatabaseId };
            }
        }
    }
}
