using System.ComponentModel;
using Polhem.Base.Serialization;
using Polhem.Definition.Database;
using Polhem.Definition.Settings;
using Polhem.LoadTests.Bootstrap;
using Polhem.LoadTests.Configuration;
using Polhem.Tests.Shared;

namespace Polhem.LoadTests.UnitTests
{
    /// <summary>
    /// Tests for <see cref="DefineWorkspace"/>.
    /// </summary>
    public class DefineWorkspaceTests : IDisposable
    {
        private const string ConnectionString = "Server=localhost;Database={@DbName};";

        private readonly string _source = Path.Combine(
            Path.GetTempPath(), "polhem-loadtest-src-" + Guid.NewGuid().ToString("N"));

        public DefineWorkspaceTests() => WriteSourceDefinitions();

        public void Dispose()
        {
            if (Directory.Exists(_source)) { Directory.Delete(_source, recursive: true); }
            GC.SuppressFinalize(this);
        }

        private void WriteSourceDefinitions()
        {
            Directory.CreateDirectory(_source);
            Directory.CreateDirectory(Path.Combine(_source, "FormSchema"));
            File.WriteAllText(Path.Combine(_source, "FormSchema", "marker.txt"), "nested");

            var databaseSettings = new DatabaseSettings();
            databaseSettings.Items!.Add(new DatabaseItem
            {
                Id = "common", CategoryId = "common",
                DatabaseType = DatabaseType.SQLite,
                ConnectionString = "Data Source=demo.db"
            });
            databaseSettings.Items!.Add(new DatabaseItem
            {
                Id = "company", CategoryId = "company",
                DatabaseType = DatabaseType.SQLite,
                ConnectionString = "Data Source=demo.db"
            });
            XmlCodec.SerializeToFile(databaseSettings,
                Path.Combine(_source, "DatabaseSettings.xml"));

            var programSettings = new ProgramSettings();
            programSettings.Items!.Add(new ProgramItem
            {
                ProgId = "Order",
                BusinessObject = "Some.Missing.OrderBO, Some.Missing.Assembly",
                Repository = "Some.Missing.OrderRepository, Some.Missing.Assembly"
            });
            programSettings.Items!.Add(new ProgramItem
            {
                ProgId = "Resolvable",
                BusinessObject = typeof(string).AssemblyQualifiedName!
            });
            programSettings.Items!.Add(new ProgramItem { ProgId = "Plain" });
            XmlCodec.SerializeToFile(programSettings,
                Path.Combine(_source, "ProgramSettings.xml"));

            var systemSettings = new SystemSettings();
            systemSettings.CommonConfiguration.IsDebugMode = true;
            systemSettings.BackendConfiguration.AuditLogOptions.UseBackgroundWriter = false;
            XmlCodec.SerializeToFile(systemSettings,
                Path.Combine(_source, "SystemSettings.xml"));
        }

        private static LoadTestOptions CreateOptions(DatabaseType provider = DatabaseType.SQLServer)
            => new() { Database = new DatabaseOptions { Provider = provider } };

        private static DatabaseSettings ReadDatabaseSettings(DefineWorkspace workspace)
            => XmlCodec.DeserializeFromFile<DatabaseSettings>(
                Path.Combine(workspace.DefinePath, "DatabaseSettings.xml"))!;

        [Fact]
        [DisplayName("CreateFrom copies the whole definition tree, including subfolders")]
        public void CreateFrom_CopiesNestedFiles()
        {
            using var workspace = DefineWorkspace.CreateFrom(_source, CreateOptions(), ConnectionString);

            Assert.True(File.Exists(Path.Combine(workspace.DefinePath, "FormSchema", "marker.txt")));
            Assert.NotEqual(_source, workspace.DefinePath);
        }

        [Fact]
        [DisplayName("CreateFrom rewrites every DatabaseItem to the configured provider")]
        public void CreateFrom_RewritesProvider()
        {
            using var workspace = DefineWorkspace.CreateFrom(
                _source, CreateOptions(DatabaseType.PostgreSQL), ConnectionString);

            var settings = ReadDatabaseSettings(workspace);
            Assert.All(settings.Items!, item =>
                Assert.Equal(DatabaseType.PostgreSQL, item.DatabaseType));
        }

        [Fact]
        [DisplayName("CreateFrom replaces {@DbName} in the connection string with the prefixed database name")]
        public void CreateFrom_SubstitutesDbNamePlaceholder()
        {
            using var workspace = DefineWorkspace.CreateFrom(_source, CreateOptions(), ConnectionString);

            var settings = ReadDatabaseSettings(workspace);
            Assert.Equal("Server=localhost;Database=loadtest_common;",
                settings.Items!["common"]!.ConnectionString);
            Assert.Equal("Server=localhost;Database=loadtest_company;",
                settings.Items!["company"]!.ConnectionString);
        }

        [Fact]
        [DisplayName("CreateFrom never resolves to the bare CategoryId, which is the unit tests' own database")]
        public void CreateFrom_NeverTargetsTheBareCategoryDatabase()
        {
            using var workspace = DefineWorkspace.CreateFrom(_source, CreateOptions(), ConnectionString);

            var settings = ReadDatabaseSettings(workspace);

            // The test harness creates catalogs named after the bare categories. A run that
            // resolved to one of those would seed rows into the databases the unit tests depend
            // on, and the damage would surface later, elsewhere, as unrelated test failures.
            Assert.All(settings.Items!, item =>
                Assert.DoesNotContain($"Database={item.CategoryId};", item.ConnectionString,
                    StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("CreateFrom uses a custom prefix")]
        public void CreateFrom_HonoursCustomPrefix()
        {
            var options = CreateOptions();
            options.Database.DatabaseNamePrefix = "perf_";

            using var workspace = DefineWorkspace.CreateFrom(_source, options, ConnectionString);

            var settings = ReadDatabaseSettings(workspace);
            Assert.Equal("Server=localhost;Database=perf_common;",
                settings.Items!["common"]!.ConnectionString);
        }

        [Fact]
        [DisplayName("CreateFrom uses a connection string without a placeholder as is")]
        public void CreateFrom_WithoutPlaceholder_UsesStringAsIs()
        {
            const string plain = "Server=localhost;Database=polhem;";

            using var workspace = DefineWorkspace.CreateFrom(_source, CreateOptions(), plain);

            var settings = ReadDatabaseSettings(workspace);
            Assert.All(settings.Items!, item => Assert.Equal(plain, item.ConnectionString));
        }

        [Fact]
        [DisplayName("CreateFrom turns off debug mode and turns the background audit writer back on")]
        public void CreateFrom_OverridesDemoOnlySystemSettings()
        {
            using var workspace = DefineWorkspace.CreateFrom(_source, CreateOptions(), ConnectionString);

            var settings = XmlCodec.DeserializeFromFile<SystemSettings>(
                Path.Combine(workspace.DefinePath, "SystemSettings.xml"))!;

            Assert.False(settings.CommonConfiguration.IsDebugMode);
            Assert.True(settings.BackendConfiguration.AuditLogOptions.UseBackgroundWriter);
        }

        [Fact]
        [DisplayName("CreateFrom leaves the source definition files unchanged")]
        public void CreateFrom_LeavesSourceUntouched()
        {
            using (DefineWorkspace.CreateFrom(_source, CreateOptions(), ConnectionString))
            {
                // The workspace exists here; the assertions below run after it is disposed.
            }

            var settings = XmlCodec.DeserializeFromFile<DatabaseSettings>(
                Path.Combine(_source, "DatabaseSettings.xml"))!;
            Assert.All(settings.Items!, item =>
                Assert.Equal(DatabaseType.SQLite, item.DatabaseType));

            var system = XmlCodec.DeserializeFromFile<SystemSettings>(
                Path.Combine(_source, "SystemSettings.xml"))!;
            Assert.True(system.CommonConfiguration.IsDebugMode);
        }

        [Fact]
        [DisplayName("Dispose deletes the temporary copy")]
        public void Dispose_RemovesTemporaryCopy()
        {
            string path;
            using (var workspace = DefineWorkspace.CreateFrom(_source, CreateOptions(), ConnectionString))
            {
                path = workspace.DefinePath;
                Assert.True(Directory.Exists(path));
            }

            Assert.False(Directory.Exists(path));
        }

        [Fact]
        [DisplayName("CreateFrom throws DirectoryNotFoundException when the source directory does not exist")]
        public void CreateFrom_MissingSource_Throws()
        {
            var missing = Path.Combine(Path.GetTempPath(), "polhem-loadtest-absent-" + Guid.NewGuid());

            Assert.Throws<DirectoryNotFoundException>(
                () => DefineWorkspace.CreateFrom(missing, CreateOptions(), ConnectionString));
        }

        [Fact]
        [DisplayName("CreateFrom clears BO and Repository bindings whose assembly cannot be loaded, falling back to the framework implementation")]
        public void CreateFrom_DropsUnresolvableBindings()
        {
            using var workspace = DefineWorkspace.CreateFrom(_source, CreateOptions(), ConnectionString);

            var settings = XmlCodec.DeserializeFromFile<ProgramSettings>(
                Path.Combine(workspace.DefinePath, "ProgramSettings.xml"))!;

            Assert.Equal(string.Empty, settings.Items!["Order"]!.BusinessObject);
            Assert.Equal(string.Empty, settings.Items!["Order"]!.Repository);
        }

        [Fact]
        [DisplayName("CreateFrom keeps bindings that can be resolved")]
        public void CreateFrom_KeepsResolvableBindings()
        {
            using var workspace = DefineWorkspace.CreateFrom(_source, CreateOptions(), ConnectionString);

            var settings = XmlCodec.DeserializeFromFile<ProgramSettings>(
                Path.Combine(workspace.DefinePath, "ProgramSettings.xml"))!;

            Assert.Equal(typeof(string).AssemblyQualifiedName,
                settings.Items!["Resolvable"]!.BusinessObject);
        }

        [Fact]
        [DisplayName("CreateFrom lists the bindings it clears instead of dropping them silently")]
        public void CreateFrom_ReportsDroppedBindings()
        {
            using var workspace = DefineWorkspace.CreateFrom(_source, CreateOptions(), ConnectionString);

            Assert.Contains("Order.BusinessObject", workspace.DroppedBindings);
            Assert.Contains("Order.Repository", workspace.DroppedBindings);
            Assert.DoesNotContain("Resolvable.BusinessObject", workspace.DroppedBindings);
            Assert.DoesNotContain("Plain.BusinessObject", workspace.DroppedBindings);
        }

        [Fact]
        [DisplayName("CreateFrom does not throw when ProgramSettings.xml is missing")]
        public void CreateFrom_WithoutProgramSettings_DoesNotThrow()
        {
            File.Delete(Path.Combine(_source, "ProgramSettings.xml"));

            using var workspace = DefineWorkspace.CreateFrom(_source, CreateOptions(), ConnectionString);

            Assert.Empty(workspace.DroppedBindings);
        }

        [Fact]
        [DisplayName("CreateFrom copies the real Northwind definitions and rewrites them away from SQLite")]
        public void CreateFrom_RealNorthwindDefinitions_RewritesEveryCategory()
        {
            var source = LocateNorthwindDefine();
            Assert.True(Directory.Exists(source), $"Northwind definitions not found at '{source}'.");

            using var workspace = DefineWorkspace.CreateFrom(
                source, CreateOptions(DatabaseType.SQLServer), ConnectionString);

            var settings = ReadDatabaseSettings(workspace);

            // The shipped set binds common / company / log, all to SQLite. Every one of them must
            // come back pointed at the configured provider: a category left on SQLite would send
            // part of the run to an engine the load test excludes.
            Assert.Equal(3, settings.Items!.Count);
            Assert.All(settings.Items!, item =>
                Assert.Equal(DatabaseType.SQLServer, item.DatabaseType));

            var system = XmlCodec.DeserializeFromFile<SystemSettings>(
                Path.Combine(workspace.DefinePath, "SystemSettings.xml"))!;
            Assert.False(system.CommonConfiguration.IsDebugMode);
            Assert.True(system.BackendConfiguration.AuditLogOptions.UseBackgroundWriter);

            // Order binds to the demo server assembly, which this process does not reference; the
            // binding has to be dropped so the program falls back to the framework's own
            // implementation rather than failing to resolve at call time.
            Assert.Contains("Order.BusinessObject", workspace.DroppedBindings);

            // The definitions the scenarios need must survive the copy.
            Assert.True(File.Exists(Path.Combine(
                workspace.DefinePath, "FormSchema", "Order.FormSchema.xml")));
            Assert.True(File.Exists(Path.Combine(
                workspace.DefinePath, "TableSchema", "company", "ft_order_detail.TableSchema.xml")));
        }

        private static string LocateNorthwindDefine()
            => Path.Combine(RepoRoot.Find(), "apps", "Polhem.Northwind", "Define");

        [Fact]
        [DisplayName("GuardIsolation passes when the connection string contains {@DbName}")]
        public void GuardIsolation_WithPlaceholder_Passes()
        {
            // S2699: verifying "does not throw" needs the exception captured and asserted on —
            // a bare call asserts nothing (tests/CLAUDE.md).
            var exception = Record.Exception(() => DefineWorkspace.GuardIsolation(
                DatabaseType.SQLServer,
                "Data Source=localhost;Initial Catalog={@DbName};",
                "POLHEM_TEST_CONNSTR_SQLSERVER"));

            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("GuardIsolation rejects a connection string without {@DbName}, since the prefix has nothing to apply to")]
        public void GuardIsolation_WithoutPlaceholder_Throws()
        {
            // Oracle's connection string names a service, not a database, so the loadtest_ prefix
            // has nothing to substitute into — every category would resolve to whatever the string
            // already points at, which for the test suite's own variable is the schema the unit
            // tests depend on.
            var ex = Assert.Throws<InvalidOperationException>(() => DefineWorkspace.GuardIsolation(
                DatabaseType.Oracle,
                "Data Source=localhost:1521/FREEPDB1;User Id=testuser;",
                "POLHEM_TEST_CONNSTR_ORACLE"));

            Assert.Contains("POLHEM_LOADTEST_CONNSTR_ORACLE", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(DatabaseType.SQLServer, "POLHEM_LOADTEST_CONNSTR_SQLSERVER")]
        [InlineData(DatabaseType.Oracle, "POLHEM_LOADTEST_CONNSTR_ORACLE")]
        [DisplayName("GetDedicatedConnectionStringVariable returns a name distinct from the test suite's variable")]
        public void GetDedicatedConnectionStringVariable_IsDistinctFromTestSuite(
            DatabaseType provider, string expected)
        {
            Assert.Equal(expected, DefineWorkspace.GetDedicatedConnectionStringVariable(provider));
            Assert.NotEqual(
                DefineWorkspace.GetConnectionStringVariable(provider),
                DefineWorkspace.GetDedicatedConnectionStringVariable(provider));
        }

        [Theory]
        [InlineData(DatabaseType.SQLServer, "POLHEM_TEST_CONNSTR_SQLSERVER")]
        [InlineData(DatabaseType.PostgreSQL, "POLHEM_TEST_CONNSTR_POSTGRESQL")]
        [InlineData(DatabaseType.MySQL, "POLHEM_TEST_CONNSTR_MYSQL")]
        [InlineData(DatabaseType.Oracle, "POLHEM_TEST_CONNSTR_ORACLE")]
        [DisplayName("GetConnectionStringVariable follows the test.sh naming convention")]
        public void GetConnectionStringVariable_MatchesTestHarnessConvention(
            DatabaseType provider, string expected)
        {
            Assert.Equal(expected, DefineWorkspace.GetConnectionStringVariable(provider));
        }
    }
}
