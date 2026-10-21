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
        private static readonly string[] s_help = ["db"];
        private static readonly string[] s_upgrade = ["db", "upgrade"];
        private static readonly string[] s_check = ["db", "check"];
        private static readonly string[] s_seedMissingValue = ["db", "seed-admin", "--company"];
        private static readonly string[] s_createUnknownOption = ["db", "create", "--force=yes"];
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
        [DisplayName("db check reports each database and fails when one could not be opened")]
        public void TryRun_Check_ReportsEachDatabase()
        {
            var (services, fake) = CreateServices();
            fake.CheckEntries =
            [
                new DatabaseCheckEntry { DatabaseId = "common", DatabaseName = "app_common" },
                new DatabaseCheckEntry { DatabaseId = "log", DatabaseName = "app_log", Error = "login failed" },
            ];
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.Failure, Run(services, s_check, output));
            var text = output.ToString();
            Assert.Contains("common (app_common): ok", text, StringComparison.Ordinal);
            Assert.Contains("log (app_log): FAILED: login failed", text, StringComparison.Ordinal);
            Assert.Contains("1 database(s) failed.", text, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("db check succeeds when every database opened, and fails when the settings have none")]
        public void TryRun_CheckAllOpen_Succeeds()
        {
            var (services, fake) = CreateServices();
            fake.CheckEntries = [new DatabaseCheckEntry { DatabaseId = "common" }];
            Assert.Equal(DatabaseCommandLine.Success, Run(services, s_check, new StringWriter()));

            fake.CheckEntries = [];
            Assert.Equal(DatabaseCommandLine.Failure, Run(services, s_check, new StringWriter()));
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
        [DisplayName("db upgrade passes every --table it is given, and none when it is given none")]
        public void TryRun_UpgradeTables_PassesTableNames()
        {
            var (services, fake) = CreateServices();

            Assert.Equal(DatabaseCommandLine.Success,
                Run(services, ["db", "upgrade", "--table", "ft_order", "--dry-run", "--table", "ft_order_detail"], new StringWriter()));
            Assert.Equal(["ft_order", "ft_order_detail"], fake.LastTableNames);
            Assert.True(fake.LastDryRun);

            Run(services, s_upgrade, new StringWriter());
            Assert.Empty(fake.LastTableNames!);
        }

        [Fact]
        [DisplayName("db upgrade --table without a name is a usage error")]
        public void TryRun_UpgradeTableWithoutName_IsUsageError()
        {
            var (services, _) = CreateServices();

            Assert.Equal(DatabaseCommandLine.UsageError, Run(services, ["db", "upgrade", "--table"], new StringWriter()));
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

        [Theory]
        [InlineData("InvalidOperation")]
        [InlineData("IO")]
        [InlineData("UnauthorizedAccess")]
        [InlineData("Db")]
        [DisplayName("Each failure type the initializer can throw is reported with its message and the failure code")]
        public void TryRun_InitializerFailureTypes_ReturnFailure(string kind)
        {
            Exception failure = kind switch
            {
                "InvalidOperation" => new InvalidOperationException("settings could not be decrypted"),
                "IO" => new IOException("definition file unreadable"),
                "UnauthorizedAccess" => new UnauthorizedAccessException("directory not writable"),
                _ => new FakeDbException("server unreachable"),
            };
            var (services, fake) = CreateServices();
            fake.CreateException = failure;
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.Failure, Run(services, s_create, output));
            Assert.Contains(failure.Message, output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("db create with no database items fails and says so")]
        public void TryRun_CreateWithoutItems_ReturnsFailure()
        {
            var (services, _) = CreateServices();
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.Failure, Run(services, s_create, output));
            Assert.Contains("no database items", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("db create reports an engine with nothing to create as skipped")]
        public void TryRun_CreateNotApplicable_ReportsSkipped()
        {
            var (services, fake) = CreateServices();
            fake.CreateEntries = [new DatabaseCreateEntry { DatabaseId = "common", Result = DatabaseCreateResult.NotApplicable }];
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.Success, Run(services, s_create, output));
            Assert.Contains("common: skipped", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("db upgrade writes one line per table, the problems and a summary, and fails when a table failed")]
        public void TryRun_UpgradeWithFailures_ReportsEachLineAndSummary()
        {
            var (services, fake) = CreateServices();
            fake.UpgradeReport = new TableUpgradeReport
            {
                Tables =
                [
                    new TableUpgradeEntry { DatabaseId = "common", TableName = "st_user", Outcome = TableUpgradeOutcome.Created },
                    new TableUpgradeEntry { DatabaseId = "common", TableName = "st_company", Outcome = TableUpgradeOutcome.Upgraded },
                    new TableUpgradeEntry { DatabaseId = "common", TableName = "st_session", Outcome = TableUpgradeOutcome.Unchanged },
                    new TableUpgradeEntry { DatabaseId = "log", TableName = "st_log_login", Outcome = TableUpgradeOutcome.Failed, Error = "no definition" },
                ],
                Problems = ["No database item belongs to the 'company' category."],
            };
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.Failure, Run(services, s_upgrade, output));
            var text = output.ToString();
            Assert.Contains("common.st_user: created", text, StringComparison.Ordinal);
            Assert.Contains("common.st_company: upgraded", text, StringComparison.Ordinal);
            Assert.Contains("common.st_session: unchanged", text, StringComparison.Ordinal);
            Assert.Contains("log.st_log_login: FAILED: no definition", text, StringComparison.Ordinal);
            Assert.Contains("'company' category", text, StringComparison.Ordinal);
            Assert.Contains("1 created, 1 upgraded, 1 unchanged, 1 failed.", text, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("db with no command prints the usage and succeeds")]
        public void TryRun_NoCommand_PrintsUsage()
        {
            var (services, _) = CreateServices();
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.Success, Run(services, s_help, output));
            Assert.Contains("Usage:", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("An option without its value, or an unknown option, is a usage error that names only the option")]
        public void TryRun_BadOptions_ReturnUsageError()
        {
            var (services, _) = CreateServices();
            var output = new StringWriter();

            Assert.Equal(DatabaseCommandLine.UsageError, Run(services, s_seedMissingValue, output));
            Assert.Contains("--company needs a value", output.ToString(), StringComparison.Ordinal);

            output = new StringWriter();
            Assert.Equal(DatabaseCommandLine.UsageError, Run(services, s_createUnknownOption, output));
            Assert.Contains("Unknown option '--force'", output.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("yes", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("db seed-admin with no password source fails as a usage error")]
        public void TryRun_SeedAdminWithoutPasswordSource_ReturnsUsageError()
        {
            var (services, fake) = CreateServices();

            var exitCode = DatabaseCommandLine.TryRun(services, s_seed, new StringWriter(), _ => null, _ => null);

            Assert.Equal(DatabaseCommandLine.UsageError, exitCode);
            Assert.Null(fake.LastSeed);
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

        private sealed class FakeDbException(string message) : System.Data.Common.DbException(message);

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

            public IReadOnlyList<DatabaseCheckEntry> CheckEntries { get; set; } = [];

            public IReadOnlyList<DatabaseCheckEntry> CheckConnections() => CheckEntries;

            public IReadOnlyCollection<string>? LastTableNames { get; private set; }

            public TableUpgradeReport UpgradeTables(
                UpgradeOptions? options = null, bool dryRun = false, Action<TableUpgradeEntry>? tableCompleted = null,
                IReadOnlyCollection<string>? tableNames = null)
            {
                LastOptions = options;
                LastDryRun = dryRun;
                LastTableNames = tableNames;
                foreach (var table in UpgradeReport.Tables)
                    tableCompleted?.Invoke(table);
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
