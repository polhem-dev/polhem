using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Db.Manager;
using Polhem.Db.Schema;
using Polhem.Hosting.Database;

namespace Polhem.Hosting.UnitTests
{
    public class DatabaseCommandLineTests
    {
        private const string Secret = "Typed-Secret-123";

        private static readonly string[] s_notDb = ["--urls", "http://localhost:5000"];
        private static readonly string[] s_create = ["db", "create"];
        private static readonly string[] s_upgradeDryRun = ["db", "upgrade", "--dry-run", "--allow-narrowing"];
        private static readonly string[] s_seed = ["db", "seed-admin", "--company", "ACME", "--user", "boss", "--no-deployment-admin"];
        private static readonly string[] s_seedNoCompany = ["db", "seed-admin"];
        private static readonly string[] s_seedWithPassword = ["db", "seed-admin", "--company", "ACME", "--password", Secret];
        private static readonly string[] s_unknown = ["db", "drop"];
        private static readonly string[] s_seedWithPasswordAssignment = ["db", "seed-admin", "--company", "ACME", "--password=" + Secret];
        private static readonly string[] s_seedWithBareSecret = ["db", "seed-admin", "--company", "ACME", Secret];

        [Fact]
        [DisplayName("TryRun returns null for arguments that are not a db command")]
        public void TryRun_NotDbCommand_ReturnsNull()
        {
            var (services, _) = CreateServices();
            Assert.Null(Run(services, s_notDb, new StringWriter()));
        }

        [Fact]
        [DisplayName("db create reports each database and succeeds when none failed")]
        public void TryRun_Create_ReportsEachDatabase()
        {
            var (services, fake) = CreateServices();
            fake.CreateEntries =
            [
                new DatabaseCreateEntry { DatabaseId = "common", DatabaseName = "app_common", Result = DatabaseCreateResult.Created },
                new DatabaseCreateEntry { DatabaseId = "log", DatabaseName = "app_log", Result = DatabaseCreateResult.AlreadyExists },
            ];
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.Success, Run(services, s_create, output));
            Assert.Contains("common (app_common): created", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("log (app_log): exists", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("db create fails when a database item failed")]
        public void TryRun_CreateWithFailure_ReturnsFailure()
        {
            var (services, fake) = CreateServices();
            fake.CreateEntries = [new DatabaseCreateEntry { DatabaseId = "common", Error = "permission denied" }];
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.Failure, Run(services, s_create, output));
            Assert.Contains("FAILED: permission denied", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("db upgrade passes the dry-run and allow-narrowing options and prints planned statements")]
        public void TryRun_UpgradeDryRun_PassesOptions()
        {
            var (services, fake) = CreateServices();
            fake.UpgradeReport = new TableUpgradeReport
            {
                Tables = [new TableUpgradeEntry
                {
                    DatabaseId = "company", CategoryId = "company", TableName = "ft_customer",
                    Outcome = TableUpgradeOutcome.Planned, CommandText = "CREATE TABLE ft_customer (...)",
                }],
            };
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.Success, Run(services, s_upgradeDryRun, output));
            Assert.True(fake.LastDryRun);
            Assert.True(fake.LastOptions?.AllowColumnNarrowing);
            Assert.Contains("CREATE TABLE ft_customer", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("db seed-admin reads the password from the prompt and never writes it to the output")]
        public void TryRun_SeedAdmin_PromptsForPasswordAndHidesIt()
        {
            var (services, fake) = CreateServices();
            var output = new StringWriter();

            var exitCode = DatabaseCommandLine.TryRun(services, s_seed, output, _ => Secret, _ => null);

            Assert.Equal(DatabaseCommandLine.Success, exitCode);
            Assert.Equal(Secret, fake.LastSeed?.Password);
            Assert.Equal("boss", fake.LastSeed?.UserId);
            Assert.Equal("ACME", fake.LastSeed?.CompanyId);
            Assert.False(fake.LastSeed?.DeploymentAdmin);
            Assert.DoesNotContain(Secret, output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("db seed-admin prefers the password environment variable over the prompt")]
        public void TryRun_SeedAdmin_UsesEnvironmentVariable()
        {
            var (services, fake) = CreateServices();

            DatabaseCommandLine.TryRun(services, s_seed, new StringWriter(),
                _ => throw new InvalidOperationException("must not prompt"),
                name => name == DatabaseCommandLine.PasswordVariable ? "From-Env-Pass" : null);

            Assert.Equal("From-Env-Pass", fake.LastSeed?.Password);
        }

        [Fact]
        [DisplayName("db seed-admin refuses two different passwords at the prompt")]
        public void TryRun_SeedAdminMismatchedPasswords_ReturnsUsageError()
        {
            var (services, fake) = CreateServices();
            var answers = new Queue<string>(["First-Pass-1", "Second-Pass-2"]);

            var exitCode = DatabaseCommandLine.TryRun(services, s_seed, new StringWriter(), _ => answers.Dequeue(), _ => null);

            Assert.Equal(DatabaseCommandLine.UsageError, exitCode);
            Assert.Null(fake.LastSeed);
        }

        [Fact]
        [DisplayName("db seed-admin without --company is a usage error")]
        public void TryRun_SeedAdminWithoutCompany_ReturnsUsageError()
        {
            var (services, _) = CreateServices();
            Assert.Equal(DatabaseCommandLine.UsageError, Run(services, s_seedNoCompany, new StringWriter()));
        }

        [Fact]
        [DisplayName("db seed-admin rejects --password without echoing the value")]
        public void TryRun_SeedAdminWithPasswordOption_RejectsWithoutEcho()
        {
            var (services, fake) = CreateServices();
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.UsageError, Run(services, s_seedWithPassword, output));
            Assert.Null(fake.LastSeed);
            Assert.DoesNotContain(Secret, output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("db seed-admin rejects --password=value and a bare argument without echoing the value")]
        public void TryRun_SeedAdminWithInlineSecret_RejectsWithoutEcho()
        {
            var (services, fake) = CreateServices();
            foreach (var args in new[] { s_seedWithPasswordAssignment, s_seedWithBareSecret })
            {
                var output = new StringWriter();
                Assert.Equal(DatabaseCommandLine.UsageError, Run(services, args, output));
                Assert.DoesNotContain(Secret, output.ToString(), StringComparison.Ordinal);
            }
            Assert.Null(fake.LastSeed);
        }

        [Fact]
        [DisplayName("A failure the initializer throws is reported as a failed command rather than escaping")]
        public void TryRun_InitializerThrows_ReturnsFailure()
        {
            var (services, fake) = CreateServices();
            fake.CreateException = new KeyNotFoundException("Database provider not registered: Oracle");
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.Failure, Run(services, s_create, output));
            Assert.Contains("provider not registered", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("An unknown db command prints the usage and returns the usage error code")]
        public void TryRun_UnknownCommand_ReturnsUsageError()
        {
            var (services, _) = CreateServices();
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.UsageError, Run(services, s_unknown, output));
            Assert.Contains("Usage:", output.ToString(), StringComparison.Ordinal);
        }

        private static int? Run(IServiceProvider services, string[] args, TextWriter output)
            => DatabaseCommandLine.TryRun(services, args, output, _ => Secret, _ => null);

        private static (ServiceProvider Services, FakeInitializer Fake) CreateServices()
        {
            var fake = new FakeInitializer();
            var services = new ServiceCollection().AddSingleton<IDatabaseInitializer>(fake).BuildServiceProvider();
            return (services, fake);
        }

        private sealed class FakeInitializer : IDatabaseInitializer
        {
            public IReadOnlyList<DatabaseCreateEntry> CreateEntries { get; set; } = [];
            public TableUpgradeReport UpgradeReport { get; set; } = new();
            public UpgradeOptions? LastOptions { get; private set; }
            public bool LastDryRun { get; private set; }
            public AdministratorSeed? LastSeed { get; private set; }

            public Exception? CreateException { get; set; }

            public IReadOnlyList<DatabaseCreateEntry> CreateDatabases()
                => CreateException is null ? CreateEntries : throw CreateException;

            public TableUpgradeReport UpgradeTables(UpgradeOptions? options = null, bool dryRun = false)
            {
                LastOptions = options;
                LastDryRun = dryRun;
                return UpgradeReport;
            }

            public AdministratorSeedResult SeedAdministrator(AdministratorSeed seed)
            {
                LastSeed = seed;
                return new AdministratorSeedResult { UserCreated = true, CompanyCreated = true, AccessGranted = true, CompanyDatabaseId = "company" };
            }
        }
    }
}
