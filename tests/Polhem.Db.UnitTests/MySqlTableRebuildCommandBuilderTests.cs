using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.MySql;
using Polhem.Db.Schema;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Pure-syntax tests for the MySQL rebuild command builder. Verifies the 6-step
    /// rebuild script (drop tmp / create tmp / copy data / drop old / rename / recreate
    /// secondary indexes) emits the expected fragments with backtick quoting and the
    /// MySQL CREATE TABLE table suffix.
    /// </summary>
    /// <remarks>
    /// The rebuild builder is internal; tests resolve it via
    /// <see cref="MySqlDialectFactory.CreateTableRebuildCommandBuilder"/> rather than
    /// instantiating it directly.
    /// </remarks>
    public class MySqlTableRebuildCommandBuilderTests : IClassFixture<SharedDbFixture>
    {
        public MySqlTableRebuildCommandBuilderTests(SharedDbFixture _) { }

        private static TableSchema BuildDefineSchema()
        {
            var schema = new TableSchema { TableName = "st_demo" };
            schema.Fields!.Add("id", "Id", FieldDbType.Guid);
            schema.Fields!.Add("name", "Name", FieldDbType.String, 50);
            schema.Fields!.Add("age", "Age", FieldDbType.Integer);
            schema.Indexes!.AddPrimaryKey("id");
            schema.Indexes!.Add("ix_{0}_name", "name", false);
            return schema;
        }

        private static TableSchema BuildRealSchema(bool withExtraLegacyField = false)
        {
            var schema = new TableSchema { TableName = "st_demo" };
            schema.Fields!.Add("id", "Id", FieldDbType.Guid);
            schema.Fields!.Add("name", "Name", FieldDbType.String, 50);
            if (withExtraLegacyField)
                schema.Fields!.Add("legacy_col", "Legacy", FieldDbType.String, 10);
            var pk = new DbTableIndex { Name = "pk_st_demo", PrimaryKey = true, Unique = true };
            pk.IndexFields!.Add("id");
            schema.Indexes!.Add(pk);
            return schema;
        }

        private static string BuildSql(TableSchema define, TableSchema? real)
        {
            var diff = new TableSchemaComparer(define, real, DatabaseType.MySQL).CompareToDiff();
            var rebuilder = new MySqlDialectFactory().CreateTableRebuildCommandBuilder();
            return rebuilder.GetCommandText(diff);
        }

        [Fact]
        [DisplayName("MySQL GetCommandText rebuild script creates the tmp table, INSERTs and RENAMEs (backtick identifiers)")]
        public void GetCommandText_BasicRebuild_IncludesTmpCreateInsertAndRename()
        {
            string sql = BuildSql(BuildDefineSchema(), BuildRealSchema());

            Assert.Contains("tmp_st_demo", sql);
            Assert.Contains("INSERT INTO `tmp_st_demo`", sql);
            Assert.Contains("ALTER TABLE `tmp_st_demo` RENAME TO `st_demo`", sql);
        }

        [Fact]
        [DisplayName("MySQL GetCommandText tmp table CREATE carries ENGINE=InnoDB and the utf8mb4_0900_ai_ci collation")]
        public void GetCommandText_TmpTableCreate_IncludesMySqlTableSuffix()
        {
            string sql = BuildSql(BuildDefineSchema(), BuildRealSchema());

            // The tmp table is created through `MySqlCreateTableCommandBuilder`, so it always carries the MySQL table
            // suffix. The case-insensitive collation built in from day one keeps the rebuilt table behaving like the
            // original.
            Assert.Contains("ENGINE=InnoDB", sql);
            Assert.Contains("COLLATE=utf8mb4_0900_ai_ci", sql);
        }

        [Fact]
        [DisplayName("MySQL GetCommandText uses IF EXISTS for DROP TABLE")]
        public void GetCommandText_DropTable_UsesIfExists()
        {
            string sql = BuildSql(BuildDefineSchema(), BuildRealSchema());

            Assert.Contains("DROP TABLE IF EXISTS `tmp_st_demo`;", sql);
            Assert.Contains("DROP TABLE IF EXISTS `st_demo`;", sql);
        }

        [Fact]
        [DisplayName("MySQL GetCommandText leaves a new field out of the INSERT ... SELECT list")]
        public void GetCommandText_AddedField_ExcludedFromDataCopy()
        {
            string sql = BuildSql(BuildDefineSchema(), BuildRealSchema());

            // The age field appears in the tmp table definition,
            Assert.Contains("`age`", sql);
            // but not in the INSERT ... SELECT clause.
            int insertIdx = sql.IndexOf("INSERT INTO `tmp_st_demo`", StringComparison.Ordinal);
            int selectIdx = sql.IndexOf("FROM `st_demo`", insertIdx, StringComparison.Ordinal);
            string insertSelectSection = sql.Substring(insertIdx, selectIdx - insertIdx);
            Assert.DoesNotContain("`age`", insertSelectSection);
        }

        [Fact]
        [DisplayName("MySQL GetCommandText keeps a real-only field (extension field) in the rebuild result")]
        public void GetCommandText_ExtensionField_Preserved()
        {
            string sql = BuildSql(BuildDefineSchema(), BuildRealSchema(withExtraLegacyField: true));

            Assert.Contains("`legacy_col`", sql);
        }

        [Fact]
        [DisplayName("MySQL GetCommandText does not create non-PK indexes on the tmp table in advance")]
        public void GetCommandText_TmpTable_OmitsSecondaryIndexes()
        {
            string sql = BuildSql(BuildDefineSchema(), BuildRealSchema());

            // No ix_tmp_st_demo_name: non-PK indexes are created with their real names after the RENAME.
            Assert.DoesNotContain("ix_tmp_st_demo_name", sql);
        }

        [Fact]
        [DisplayName("MySQL GetCommandText recreates non-PK indexes with their real names after the RENAME")]
        public void GetCommandText_NonPkIndexes_RecreatedWithRealNames()
        {
            string sql = BuildSql(BuildDefineSchema(), BuildRealSchema());

            int renameIdx = sql.IndexOf("RENAME TO `st_demo`", StringComparison.Ordinal);
            int recreateIdx = sql.IndexOf("CREATE INDEX `ix_st_demo_name`", StringComparison.Ordinal);

            Assert.True(renameIdx > 0, "The RENAME step must be present");
            Assert.True(recreateIdx > renameIdx, "Non-PK indexes must be recreated after the RENAME");
            Assert.Contains("ON `st_demo` (`name` ASC)", sql);
        }

        [Fact]
        [DisplayName("MySQL GetCommandText script contains no ALTER INDEX RENAME (same pattern as SQLite and PostgreSQL, avoiding dialect differences)")]
        public void GetCommandText_NeverEmitsAlterIndexRename()
        {
            string sql = BuildSql(BuildDefineSchema(), BuildRealSchema());

            Assert.DoesNotContain("ALTER INDEX", sql);
            Assert.DoesNotContain("RENAME INDEX", sql);
        }

        [Fact]
        [DisplayName("MySQL GetCommandText throws for a new-table diff (which must take the CREATE path)")]
        public void GetCommandText_NewTableDiff_Throws()
        {
            var define = BuildDefineSchema();
            var diff = new TableSchemaComparer(define, realTable: null, DatabaseType.MySQL).CompareToDiff();
            var rebuilder = new MySqlDialectFactory().CreateTableRebuildCommandBuilder();

            Assert.Throws<InvalidOperationException>(() => rebuilder.GetCommandText(diff));
        }
    }
}
