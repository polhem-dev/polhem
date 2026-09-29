using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Providers;
using Polhem.Db.Providers.MySql;
using Polhem.Db.Providers.Oracle;
using Polhem.Db.Providers.PostgreSql;
using Polhem.Db.Providers.SqlServer;
using Polhem.Db.Providers.Sqlite;
using Polhem.Db.Schema;
using Polhem.Db.Schema.Changes;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Regression: the ALTER path only ever synchronized descriptions for SQL Server and skipped every other dialect.
    /// Fields added by ALTER therefore never got a caption, and because <see cref="TableSchemaDiff.IsEmpty"/> counts
    /// description differences, every later comparison reported a difference and every Plan produced an Alter with
    /// zero stages. Nothing failed, but "is this table up to date" was always answered no.
    /// </summary>
    public class DescriptionSyncCommandBuilderTests : IClassFixture<SharedDbFixture>
    {
        public DescriptionSyncCommandBuilderTests(SharedDbFixture _) { }

        private static TableSchema BuildDefine(string tableName = "st_demo")
        {
            var schema = new TableSchema { TableName = tableName };
            schema.Fields!.Add("id", "Id", FieldDbType.Guid);
            schema.Fields!.Add("name", "Name", FieldDbType.String, 50);
            return schema;
        }

        /// <summary>
        /// Builds a diff that only adds one field, simulating the state at the moment of an in-place upgrade.
        /// </summary>
        private static TableSchemaDiff BuildAddColumnDiff()
        {
            var define = BuildDefine();
            var added = define.Fields!.Add("api_key_id", "API Key", FieldDbType.String, 50);
            var real = BuildDefine();
            var diff = new TableSchemaDiff(define, real);
            diff.ChangeList.Add(new AddFieldChange(added.Clone()));
            return diff;
        }

        private static TableSchemaDiff BuildCaptionDriftDiff()
        {
            var define = BuildDefine();
            var diff = new TableSchemaDiff(define, BuildDefine());
            diff.DescriptionChangeList.Add(new DescriptionChange
            {
                Level = DescriptionLevel.Column,
                FieldName = "name",
                NewValue = "Name",
                IsNew = false,
            });
            return diff;
        }

        // ---------- The plan that adds a field also writes its caption ----------

        [Fact]
        [DisplayName("Oracle ALTER that adds a field also emits COMMENT ON COLUMN for it")]
        public void Oracle_AddedColumn_EmitsColumnComment()
        {
            var statements = new OracleDescriptionSyncCommandBuilder().GetStatements(BuildAddColumnDiff());

            var sql = Assert.Single(statements);
            Assert.Equal("COMMENT ON COLUMN \"ST_DEMO\".\"API_KEY_ID\" IS 'API Key'", sql);
        }

        [Fact]
        [DisplayName("PostgreSQL ALTER that adds a field also emits COMMENT ON COLUMN for it")]
        public void Pg_AddedColumn_EmitsColumnComment()
        {
            var statements = new PgDescriptionSyncCommandBuilder().GetStatements(BuildAddColumnDiff());

            var sql = Assert.Single(statements);
            Assert.Equal("COMMENT ON COLUMN \"st_demo\".\"api_key_id\" IS 'API Key';", sql);
        }

        [Fact]
        [DisplayName("SQL Server ALTER that adds a field adds its description with sp_addextendedproperty")]
        public void SqlServer_AddedColumn_EmitsAddExtendedProperty()
        {
            var statements = new SqlDescriptionSyncCommandBuilder().GetStatements(BuildAddColumnDiff());

            var sql = Assert.Single(statements);
            Assert.Contains("sp_addextendedproperty", sql, StringComparison.Ordinal);
            Assert.Contains("@level2name=N'api_key_id'", sql, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("MySQL ADD COLUMN already includes COMMENT and does not add it again")]
        public void MySql_AddedColumn_EmitsNothing()
        {
            var statements = new MySqlDescriptionSyncCommandBuilder().GetStatements(BuildAddColumnDiff());

            Assert.Empty(statements);
        }

        // ---------- Caption drift alone (no structural change with it) ----------

        [Fact]
        [DisplayName("Oracle field caption drift emits COMMENT ON COLUMN")]
        public void Oracle_CaptionDrift_EmitsColumnComment()
        {
            var statements = new OracleDescriptionSyncCommandBuilder().GetStatements(BuildCaptionDriftDiff());

            Assert.Equal("COMMENT ON COLUMN \"ST_DEMO\".\"NAME\" IS 'Name'", Assert.Single(statements));
        }

        [Fact]
        [DisplayName("MySQL field caption drift restates the full definition (including COMMENT) with MODIFY COLUMN")]
        public void MySql_CaptionDrift_EmitsModifyColumn()
        {
            var statements = new MySqlDescriptionSyncCommandBuilder().GetStatements(BuildCaptionDriftDiff());

            var sql = Assert.Single(statements);
            Assert.StartsWith("ALTER TABLE `st_demo` MODIFY COLUMN `name` ", sql, StringComparison.Ordinal);
            Assert.Contains("COMMENT 'Name'", sql, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("MySQL does not emit another MODIFY COLUMN for a field that already has an AlterFieldChange in the same batch")]
        public void MySql_ColumnAlreadyAltered_SkipsRedundantModify()
        {
            var diff = BuildCaptionDriftDiff();
            var oldField = diff.DefineTable.Fields!["name"].Clone();
            oldField.Length = 30;
            diff.ChangeList.Add(new AlterFieldChange(oldField, diff.DefineTable.Fields!["name"].Clone()));

            var statements = new MySqlDescriptionSyncCommandBuilder().GetStatements(diff);

            Assert.Empty(statements);
        }

        [Fact]
        [DisplayName("MySQL MODIFY COLUMN of an AutoIncrement field keeps AUTO_INCREMENT")]
        public void MySql_AutoIncrementCaptionDrift_KeepsAutoIncrement()
        {
            // Regression: MODIFY replaces the whole field definition, so leaving out AUTO_INCREMENT removes the
            // auto-increment, and every later INSERT fails with "Field 'sys_no' doesn't have a default value".
            var define = new TableSchema { TableName = "st_demo" };
            define.Fields!.Add("sys_no", "Sequence", FieldDbType.AutoIncrement);
            var diff = new TableSchemaDiff(define, define.Clone());
            diff.DescriptionChangeList.Add(new DescriptionChange
            {
                Level = DescriptionLevel.Column,
                FieldName = "sys_no",
                NewValue = "Sequence",
                IsNew = true,
            });

            var sql = Assert.Single(new MySqlDescriptionSyncCommandBuilder().GetStatements(diff));

            Assert.Equal("ALTER TABLE `st_demo` MODIFY COLUMN `sys_no` BIGINT NOT NULL AUTO_INCREMENT COMMENT 'Sequence';", sql);
        }

        [Fact]
        [DisplayName("MySQL table-level DisplayName drift emits ALTER TABLE ... COMMENT")]
        public void MySql_TableDescriptionDrift_EmitsTableComment()
        {
            var diff = new TableSchemaDiff(BuildDefine(), BuildDefine());
            diff.DescriptionChangeList.Add(new DescriptionChange
            {
                Level = DescriptionLevel.Table,
                NewValue = "示範資料表",
                IsNew = true,
            });

            var statements = new MySqlDescriptionSyncCommandBuilder().GetStatements(diff);

            Assert.Equal("ALTER TABLE `st_demo` COMMENT = '示範資料表';", Assert.Single(statements));
        }

        [Fact]
        [DisplayName("No dialect produces a statement when there is no description to write")]
        public void AllDialects_NothingToApply_EmitNoStatements()
        {
            var define = new TableSchema { TableName = "st_demo" };
            define.Fields!.Add("id", string.Empty, FieldDbType.Guid);
            var diff = new TableSchemaDiff(define, define.Clone());

            Assert.Empty(new OracleDescriptionSyncCommandBuilder().GetStatements(diff));
            Assert.Empty(new PgDescriptionSyncCommandBuilder().GetStatements(diff));
            Assert.Empty(new MySqlDescriptionSyncCommandBuilder().GetStatements(diff));
            Assert.Empty(new SqlDescriptionSyncCommandBuilder().GetStatements(diff));
        }

        // ---------- Dialect factory wiring ----------

        [Theory]
        [DisplayName("Every dialect factory that can persist descriptions provides a description sync builder")]
        [InlineData(typeof(SqlDialectFactory))]
        [InlineData(typeof(PgDialectFactory))]
        [InlineData(typeof(MySqlDialectFactory))]
        [InlineData(typeof(OracleDialectFactory))]
        public void DialectFactory_DescriptionCapableDialects_ReturnBuilder(Type factoryType)
        {
            var factory = (IDialectFactory)Activator.CreateInstance(factoryType)!;

            Assert.NotNull(factory.CreateDescriptionSyncCommandBuilder());
        }

        [Fact]
        [DisplayName("SQLite has no COMMENT mechanism and provides no description sync builder")]
        public void DialectFactory_Sqlite_ReturnsNull()
        {
            // Called only through the interface: this is a default interface member that
            // `SqliteDialectFactory` does not override.
            IDialectFactory factory = new SqliteDialectFactory();

            Assert.Null(factory.CreateDescriptionSyncCommandBuilder());
        }

        [Fact]
        [DisplayName("SQLite does not count a caption difference as a diff (otherwise it would never be NoChange)")]
        public void CompareToDiff_Sqlite_DoesNotReportDescriptionDrift()
        {
            var define = BuildDefine();
            // `SqliteTableSchemaProvider` always reads captions back as an empty string.
            var real = new TableSchema { TableName = "st_demo" };
            real.Fields!.Add("id", string.Empty, FieldDbType.Guid);
            real.Fields!.Add("name", string.Empty, FieldDbType.String, 50);

            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLite).CompareToDiff();

            Assert.Empty(diff.DescriptionChanges);
            Assert.True(diff.IsEmpty);
        }
    }
}
