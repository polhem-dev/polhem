using System.ComponentModel;
using Microsoft.Data.Sqlite;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Settings;
using Polhem.Hosting.Database;
using Polhem.Tests.Shared;

namespace Polhem.DbManager.UnitTests
{
    /// <summary>
    /// The <c>polhem-db</c> command line: how it finds the definition folder, and the db commands run end to end
    /// against SQLite files in a temporary folder.
    /// </summary>
    /// <remarks>
    /// A test that builds the framework's services runs the tool as a child process, as DefineEditor does: the
    /// framework's definition cache is process-wide, so a second definition folder in the same process would be served
    /// the first one's settings.
    /// </remarks>
    public sealed class ProgramTests : IDisposable
    {
        private readonly string _definePath = Path.Combine(Path.GetTempPath(), $"polhem-dbmanager-{Guid.NewGuid():N}");

        public ProgramTests() => Directory.CreateDirectory(_definePath);

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(_definePath, recursive: true); } catch (IOException) { /* best effort */ }
        }

        private int Run(params string[] args) => Program.Run(args, new StringWriter(), _definePath);

        private static (int ExitCode, string Output) RunTool(string workingDirectory, params string[] args)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo("dotnet")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
            foreach (var arg in args) startInfo.ArgumentList.Add(arg);
            using var process = System.Diagnostics.Process.Start(startInfo)!;
            var error = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(TimeSpan.FromMinutes(2)))
            {
                process.Kill(entireProcessTree: true);
                Assert.Fail($"polhem-db {string.Join(' ', args)} did not finish within two minutes.");
            }
            return (process.ExitCode, output.Result + error.Result);
        }

        private string RunForOutput(params string[] args)
        {
            var output = new StringWriter();
            Program.Run(args, output, _definePath);
            return output.ToString();
        }

        [Fact]
        [DisplayName("No arguments prints the usage and is a usage error; --help prints it and succeeds")]
        public void Run_NoArgumentsOrHelp_PrintsUsage()
        {
            Assert.Equal(DatabaseCommandLine.UsageError, Run());
            Assert.Equal(DatabaseCommandLine.Success, Run("--help"));
            Assert.Contains("polhem-db <command>", RunForOutput("--help"), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("--version prints the version and succeeds")]
        public void Run_Version_PrintsVersion()
        {
            Assert.Equal(DatabaseCommandLine.Success, Run("--version"));
            Assert.NotEmpty(RunForOutput("--version").Trim());
        }

        [Fact]
        [DisplayName("A folder without SystemSettings.xml is a usage error that names the folder")]
        public void Run_FolderWithoutSystemSettings_IsUsageError()
        {
            Assert.Equal(DatabaseCommandLine.UsageError, Run("check"));
            Assert.Contains(_definePath, RunForOutput("check"), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("--define without a value is a usage error")]
        public void Run_DefineWithoutValue_IsUsageError()
        {
            Assert.Equal(DatabaseCommandLine.UsageError, Run("check", "--define"));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("create, check and upgrade run against the folder named by --define, and --table limits the upgrade")]
        public void Run_SqliteFolder_CreatesChecksAndUpgrades()
        {
            var definePath = Path.Combine(_definePath, "Define");
            Defaults.MaterializeTo(definePath);
            SaveSqliteSettings(definePath);
            var other = Path.Combine(_definePath, "elsewhere");
            Directory.CreateDirectory(other);

            Assert.Equal(DatabaseCommandLine.Success, RunTool(other, "create", "--define", definePath).ExitCode);
            Assert.Equal(DatabaseCommandLine.Success, RunTool(other, "check", "--define", "../Define").ExitCode);
            Assert.Equal(DatabaseCommandLine.Success, RunTool(other, "upgrade", "--table", "st_user", "--define", definePath).ExitCode);

            Assert.True(TableExists(definePath, DbCategoryIds.Common, "st_user"));
            Assert.False(TableExists(definePath, DbCategoryIds.Common, "st_session"));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A SQLite item with a relative path fails with an explanation and creates no file")]
        public void Run_RelativeSqlitePath_FailsWithoutCreatingFile()
        {
            var definePath = Path.Combine(_definePath, "Define");
            Defaults.MaterializeTo(definePath);
            var settings = new DatabaseSettings();
            settings.Items!.Add(new DatabaseItem
            {
                Id = DbCategoryIds.Common, CategoryId = DbCategoryIds.Common, DatabaseType = DatabaseType.SQLite,
                ConnectionString = "Data Source=relative.db",
            });
            File.WriteAllText(Path.Combine(definePath, "DatabaseSettings.xml"), Polhem.Core.Serialization.XmlCodec.Serialize(settings));
            var (exitCode, output) = RunTool(_definePath, "check", "--define", definePath);

            Assert.Equal(DatabaseCommandLine.Failure, exitCode);
            Assert.Contains("'relative.db' is relative", output, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(_definePath, "relative.db")));
            Assert.False(File.Exists(Path.Combine(definePath, "relative.db")));
        }

        [Fact]
        [DisplayName("An unset master key variable fails with its name when SystemSettings holds an encrypted key")]
        public void Run_MissingMasterKeyWithEncryptedKey_NamesVariable()
        {
            var definePath = Path.Combine(_definePath, "Define");
            Defaults.MaterializeTo(definePath);
            var variable = $"POLHEM_TEST_UNSET_{Guid.NewGuid():N}";
            var systemSettings = Path.Combine(definePath, "SystemSettings.xml");
            var document = System.Xml.Linq.XDocument.Load(systemSettings);
            var keys = document.Descendants("SecurityKeySettings").Single();
            keys.Element("MasterKeySource")!.Element("Value")!.Value = variable;
            keys.Add(new System.Xml.Linq.XElement("ConfigEncryptionKey", "encrypted-value"));
            document.Save(systemSettings);
            var (exitCode, output) = RunTool(_definePath, "check", "--define", definePath);

            Assert.Equal(DatabaseCommandLine.Failure, exitCode);
            Assert.Contains(variable, output, StringComparison.Ordinal);
        }

        private static string UseKeyFile(string definePath)
        {
            var systemSettings = Path.Combine(definePath, "SystemSettings.xml");
            var document = System.Xml.Linq.XDocument.Load(systemSettings);
            var source = document.Descendants("SecurityKeySettings").Single().Element("MasterKeySource")!;
            source.Element("Type")!.Value = "File";
            source.Element("Value")!.Value = "Master.key";
            document.Save(systemSettings);
            return Path.Combine(definePath, "Master.key");
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("create makes a missing master key file, owner-only, and reports its path without the key")]
        public void Run_CreateWithoutKeyFile_CreatesKeyFile()
        {
            var definePath = Path.Combine(_definePath, "Define");
            Defaults.MaterializeTo(definePath);
            SaveSqliteSettings(definePath);
            var keyFile = UseKeyFile(definePath);

            var (exitCode, output) = RunTool(_definePath, "create", "--define", definePath);

            Assert.Equal(DatabaseCommandLine.Success, exitCode);
            Assert.True(File.Exists(keyFile));
            Assert.Contains($"master key: created {keyFile}", output, StringComparison.Ordinal);
            Assert.DoesNotContain(File.ReadAllText(keyFile).Trim(), output, StringComparison.Ordinal);
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(keyFile));
            }

            var key = File.ReadAllText(keyFile);
            var (again, secondOutput) = RunTool(_definePath, "create", "--define", definePath);
            Assert.Equal(DatabaseCommandLine.Success, again);
            Assert.Equal(key, File.ReadAllText(keyFile));
            Assert.DoesNotContain("master key: created", secondOutput, StringComparison.Ordinal);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Commands other than create fail without the master key file and do not create it")]
        public void Run_CheckWithoutKeyFile_FailsWithoutCreating()
        {
            var definePath = Path.Combine(_definePath, "Define");
            Defaults.MaterializeTo(definePath);
            SaveSqliteSettings(definePath);
            var keyFile = UseKeyFile(definePath);

            var (exitCode, output) = RunTool(_definePath, "check", "--define", definePath);

            Assert.Equal(DatabaseCommandLine.Failure, exitCode);
            Assert.Contains("Master key file not found", output, StringComparison.Ordinal);
            Assert.False(File.Exists(keyFile));
        }

        private static void SaveSqliteSettings(string definePath)
        {
            var settings = new DatabaseSettings();
            foreach (var category in new[] { DbCategoryIds.Common, DbCategoryIds.Company, DbCategoryIds.Log })
            {
                settings.Items!.Add(new DatabaseItem
                {
                    Id = category,
                    CategoryId = category,
                    DatabaseType = DatabaseType.SQLite,
                    ConnectionString = $"Data Source={DatabaseFile(definePath, category)};Pooling=False",
                });
            }
            File.WriteAllText(Path.Combine(definePath, "DatabaseSettings.xml"),
                Polhem.Core.Serialization.XmlCodec.Serialize(settings));
        }

        private static string DatabaseFile(string definePath, string category)
            => Path.Combine(definePath, "data", category + ".db");

        private static bool TableExists(string definePath, string category, string table)
        {
            using var connection = new SqliteConnection($"Data Source={DatabaseFile(definePath, category)};Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
            command.Parameters.AddWithValue("$name", table);
            return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;
        }
    }
}
