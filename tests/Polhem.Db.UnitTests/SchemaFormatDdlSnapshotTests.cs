using System.ComponentModel;
using System.Text;
using Polhem.Db.Manager;
using Polhem.Db.Providers;
using Polhem.Db.Providers.MySql;
using Polhem.Db.Providers.Oracle;
using Polhem.Db.Providers.PostgreSql;
using Polhem.Db.Providers.Sqlite;
using Polhem.Db.Providers.SqlServer;
using Polhem.Db.Schema;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Fails when the DDL a dialect emits for an upgrade changes while <see cref="SchemaFormat.Current"/> stays the same.
    /// </summary>
    /// <remarks>
    /// A tool of an older format reads a table a newer DDL wrote as a difference and changes it back, and a
    /// <see cref="UpgradeCompareMode.Fast"/> compare takes a table recorded under the same format as unchanged; both
    /// are safe only when the format is raised with the change. The test plans the definition of
    /// <see cref="UpgradeIdempotencyGateTests"/> on each dialect through the create, add-column and rebuild paths, and
    /// compares the statements with the snapshots under <c>DdlSnapshots/format-N</c>, where N is the current format.
    /// It does not see a change in what a provider reads back or what the comparer detects; those raise the format by
    /// the database rule, not by this test.
    /// </remarks>
    public class SchemaFormatDdlSnapshotTests
    {
        private const string UpdateVariable = "POLHEM_UPDATE_DDL_SNAPSHOTS";

        public static TheoryData<string> Dialects => [.. s_dialects.Keys.Order(StringComparer.Ordinal)];

        private static readonly Dictionary<string, (DatabaseType Type, IDialectFactory Factory)> s_dialects =
            new Dictionary<string, (DatabaseType, IDialectFactory)>(StringComparer.Ordinal)
            {
                ["mysql"] = (DatabaseType.MySQL, new MySqlDialectFactory()),
                ["oracle"] = (DatabaseType.Oracle, new OracleDialectFactory()),
                ["postgresql"] = (DatabaseType.PostgreSQL, new PgDialectFactory()),
                ["sqlite"] = (DatabaseType.SQLite, new SqliteDialectFactory()),
                ["sqlserver"] = (DatabaseType.SQLServer, new SqlDialectFactory()),
            };

        [Theory]
        [MemberData(nameof(Dialects))]
        [DisplayName("The upgrade DDL of a dialect matches the snapshot of the current schema format")]
        public void UpgradeDdl_MatchesSnapshotOfCurrentFormat(string dialect)
        {
            string actual = Render(dialect);
            string directory = FormatDirectory(SchemaFormat.Current);
            string path = Path.Combine(directory, dialect + ".sql");

            if (!File.Exists(path))
            {
                Assert.True(Updating,
                    $"No DDL snapshot of {dialect} for schema format {SchemaFormat.Current} ({path}). " +
                    $"Write the snapshots by running this test with {UpdateVariable}=1, and commit them.");
                WriteSnapshot(directory, path, actual);
                return;
            }

            string expected = File.ReadAllText(path).ReplaceLineEndings("\n");
            Assert.True(expected == actual,
                $"The upgrade DDL of {dialect} changed, but {nameof(SchemaFormat)}.{nameof(SchemaFormat.Current)} is " +
                $"still {SchemaFormat.Current}. A tool of the old format would change the new shape back, and a fast " +
                $"compare would take tables upgraded by the old DDL as current. Raise {nameof(SchemaFormat)}." +
                $"{nameof(SchemaFormat.Current)} in src/Polhem.Db/Schema/SchemaFormat.cs to {SchemaFormat.Current + 1}, " +
                $"then run this test with {UpdateVariable}=1 to write the new snapshots. A snapshot is never rewritten " +
                $"under the same format. First difference: {FirstDifference(expected, actual)}");
        }

        private static bool Updating
            => string.Equals(Environment.GetEnvironmentVariable(UpdateVariable), "1", StringComparison.Ordinal);

        private static string Render(string dialect)
        {
            var (type, factory) = s_dialects[dialect];
            var orchestrator = new TableUpgradeOrchestrator(factory, new NoConnectionManager());
            var target = UpgradeIdempotencyGateTests.BuildDefinition();
            var text = new StringBuilder();
            Append(text, "create", orchestrator, new TableSchemaComparer(target, null, type));
            Append(text, "add columns", orchestrator,
                new TableSchemaComparer(target, AsRead(UpgradeIdempotencyGateTests.BuildAddColumnsStart()), type));
            Append(text, "rebuild", orchestrator,
                new TableSchemaComparer(target, AsRead(UpgradeIdempotencyGateTests.BuildRebuildStart()), type));
            // Trailing spaces are dropped, so an editor that strips them from the snapshot does not fail the test.
            var lines = text.ToString().ReplaceLineEndings("\n").Split('\n').Select(line => line.TrimEnd());
            return string.Join('\n', lines);
        }

        // A provider reads index names back with the table name in place of the `{0}` of the definition.
        private static TableSchema AsRead(TableSchema schema)
        {
            foreach (var index in schema.Indexes!)
                index.Name = Polhem.Core.StringUtilities.Format(index.Name, schema.TableName);
            return schema;
        }

        private static void Append(StringBuilder text, string path, TableUpgradeOrchestrator orchestrator, TableSchemaComparer comparer)
        {
            var plan = orchestrator.Plan(comparer.CompareToDiff());
            text.Append("-- path: ").Append(path).Append(", mode: ").Append(plan.Mode).Append('\n');
            foreach (var stage in plan.Stages)
            {
                text.Append("-- stage: ").Append(stage.Kind).Append('\n');
                foreach (var statement in stage.Statements)
                    text.Append(statement.TrimEnd()).Append("\n\n");
            }
        }

        private static string FirstDifference(string expected, string actual)
        {
            var expectedLines = expected.Split('\n');
            var actualLines = actual.Split('\n');
            for (int i = 0; i < Math.Max(expectedLines.Length, actualLines.Length); i++)
            {
                string e = i < expectedLines.Length ? expectedLines[i] : "(end)";
                string a = i < actualLines.Length ? actualLines[i] : "(end)";
                if (!string.Equals(e, a, StringComparison.Ordinal))
                    return $"line {i + 1}, snapshot '{e}', now '{a}'.";
            }
            return "none.";
        }

        private static void WriteSnapshot(string directory, string path, string text)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, text);
            // The snapshots of an earlier format guard nothing once the format is raised.
            foreach (var old in Directory.GetDirectories(SnapshotRoot, "format-*"))
            {
                if (!string.Equals(Path.GetFullPath(old), Path.GetFullPath(directory), StringComparison.Ordinal))
                    Directory.Delete(old, recursive: true);
            }
        }

        // The snapshots are read from the source tree, not the build output, so that writing them updates the files
        // that are committed.
        private static string FormatDirectory(int format) => Path.Combine(SnapshotRoot, $"format-{format}");

        private static string SnapshotRoot { get; } =
            Path.Combine(RepoRoot.Find(), "tests", "Polhem.Db.UnitTests", "DdlSnapshots");

        // Planning reads no database; the orchestrator only needs a connection manager to be constructed.
        private sealed class NoConnectionManager : IDbConnectionManager
        {
            public DbConnectionInfo GetConnectionInfo(string databaseId) => throw new NotSupportedException();
            public System.Data.Common.DbConnection CreateConnection(string databaseId) => throw new NotSupportedException();
            public bool Remove(string databaseId) => false;
            public void Clear() { }
            public bool Contains(string databaseId) => false;
            public int Count => 0;
        }
    }
}
