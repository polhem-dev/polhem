using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Database;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The CREATE TABLE preview of the TableSchema editor: which dialect each option produces and what a schema the
    /// builder rejects gives. The SQL itself is pinned by the dialect builders' own tests in Polhem.Db.UnitTests.
    /// </summary>
    public class CreateTableScriptBuilderTests
    {
        private static TableSchema DemoSchema()
        {
            var schema = new TableSchema { TableName = "st_demo" };
            schema.Fields!.Add("sys_rowid", "Row ID", FieldDbType.Guid);
            schema.Fields.Add("demo_name", "Name", FieldDbType.String, 50);
            schema.Indexes!.AddPrimaryKey("sys_rowid");
            return schema;
        }

        [Fact]
        [DisplayName("Every offered database produces a statement with the table and its fields, each in its own dialect")]
        public void Build_EveryOption_ProducesItsOwnDialect()
        {
            var schema = DemoSchema();

            var scripts = CreateTableScriptBuilder.Options
                .Select(o => CreateTableScriptBuilder.Build(schema, o.Type))
                .ToList();

            Assert.Equal(Enum.GetValues<DatabaseType>().Length, CreateTableScriptBuilder.Options.Count);
            // Oracle folds unquoted identifiers to upper case, so names are matched without regard to case.
            Assert.All(scripts, script =>
            {
                Assert.Contains("CREATE TABLE", script, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("st_demo", script, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("demo_name", script, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("\r", script, StringComparison.Ordinal);
            });
            Assert.Equal(scripts.Count, scripts.Distinct(StringComparer.Ordinal).Count());
        }

        [Theory]
        [InlineData(DatabaseType.SQLServer, "[st_demo]")]
        [InlineData(DatabaseType.MySQL, "`st_demo`")]
        [DisplayName("Build uses the identifier quoting of the chosen database")]
        public void Build_QuotedDialects_QuoteTheTableName(DatabaseType type, string quotedName)
        {
            Assert.Contains(quotedName, CreateTableScriptBuilder.Build(DemoSchema(), type), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The TableSchema editor rebuilds its CREATE TABLE after an edit and when the database changes")]
        public void TableSchemaEditor_EditAndDatabaseChange_RefreshScript()
        {
            var path = Path.Combine(Path.GetTempPath(), "polhem-create-table-" + Guid.NewGuid().ToString("N") + ".TableSchema.xml");
            XmlCodec.SerializeToFile(DemoSchema(), path);
            try
            {
                using var document = TableSchemaDocumentViewModel.Load(path);
                document.CreateTableDatabase = DatabaseType.SQLServer;

                document.Root.TableName = "st_renamed";
                document.MarkChanged();
                Assert.Contains("[st_renamed]", document.CreateTableScript, StringComparison.Ordinal);

                document.CreateTableDatabase = DatabaseType.MySQL;
                Assert.Contains("`st_renamed`", document.CreateTableScript, StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        [DisplayName("Build returns a SQL comment instead of throwing when the dialect builder rejects the schema")]
        public void Build_RejectedSchema_ReturnsComment()
        {
            var schema = new TableSchema { TableName = "st_bad" };
            schema.Fields!.Add("col", "Col", (FieldDbType)999);

            var script = CreateTableScriptBuilder.Build(schema, DatabaseType.MySQL);

            Assert.StartsWith("-- ", script, StringComparison.Ordinal);
        }
    }
}
