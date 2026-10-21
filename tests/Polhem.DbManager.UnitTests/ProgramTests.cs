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

            Assert.Equal(DatabaseCommandLine.Success, Program.Run(["create", "--define", definePath], new StringWriter(), other));
            Assert.Equal(DatabaseCommandLine.Success, Program.Run(["check", "--define", "../Define"], new StringWriter(), other));
            Assert.Equal(DatabaseCommandLine.Success,
                Program.Run(["upgrade", "--table", "st_user", "--define", definePath], new StringWriter(), other));

            Assert.True(TableExists(definePath, DbCategoryIds.Common, "st_user"));
            Assert.False(TableExists(definePath, DbCategoryIds.Common, "st_session"));
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
