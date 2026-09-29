using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Providers.Oracle;
using Polhem.Db.Schema;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Pure-syntax tests for the Oracle rebuild command builder. Verifies the 6-step
    /// rebuild script (drop tmp / create tmp / copy data / drop old / rename / recreate
    /// secondary indexes) emits the expected fragments with double-quote quoting and the
    /// Oracle-specific PL/SQL DROP-IF-EXISTS pattern.
    /// </summary>
    /// <remarks>
    /// The rebuild builder is internal; tests resolve it via
    /// <see cref="OracleDialectFactory.CreateTableRebuildCommandBuilder"/> rather than
    /// instantiating it directly.
    /// </remarks>
    public class OracleTableRebuildCommandBuilderTests : IClassFixture<SharedDbFixture>
    {
        public OracleTableRebuildCommandBuilderTests(SharedDbFixture _) { }

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
            var diff = new TableSchemaComparer(define, real, DatabaseType.Oracle).CompareToDiff();
            var rebuilder = new OracleDialectFactory().CreateTableRebuildCommandBuilder();
            return rebuilder.GetCommandText(diff);
        }

        [Fact]
        [DisplayName("Oracle GetCommandText rebuild script creates the tmp table, INSERTs and RENAMEs (double-quoted identifiers)")]
        public void GetCommandText_BasicRebuild_IncludesTmpCreateInsertAndRename()
        {
            string sql = BuildSql(BuildDefineSchema(), BuildRealSchema());

            Assert.Contains("tmp_st_demo", sql);
            Assert.Contains("INSERT INTO \"TMP_ST_DEMO\"", sql);
            Assert.Contains("ALTER TABLE \"TMP_ST_DEMO\" RENAME TO \"ST_DEMO\"", sql);
        }

        [Fact]
        [DisplayName("Oracle GetCommandText creates the tmp table through OracleCreateTableCommandBuilder (double-quoted identifiers, no ENGINE/CHARSET suffix)")]
        public void GetCommandText_TmpTableCreate_UsesOracleCreateTableShape()
        {
            string sql = BuildSql(BuildDefineSchema(), BuildRealSchema());

            // The tmp table is created through `OracleCreateTableCommandBuilder`,
            // so no MySQL/SQLite style suffix appears.
            Assert.Contains("CREATE TABLE \"TMP_ST_DEMO\"", sql);
            Assert.DoesNotContain("ENGINE=InnoDB", sql);
            Assert.DoesNotContain("COLLATE=", sql);
        }

        [Fact]
        [DisplayName("Oracle GetCommandText wraps DROP TABLE in a PL/SQL block that swallows ORA-00942 (instead of IF EXISTS)")]
        public void GetCommandText_DropTable_UsesPlSqlExceptionSuppression()
        {
            string sql = BuildSql(BuildDefineSchema(), BuildRealSchema());

            // Oracle has no DROP TABLE IF EXISTS; the DDL is wrapped in an anonymous block that ignores only ORA-00942.
            Assert.Contains("EXECUTE IMMEDIATE 'DROP TABLE \"TMP_ST_DEMO\" CASCADE CONSTRAINTS'", sql);
            Assert.Contains("EXECUTE IMMEDIATE 'DROP TABLE \"ST_DEMO\" CASCADE CONSTRAINTS'", sql);
            Assert.Contains("WHEN OTHERS THEN IF SQLCODE != -942 THEN RAISE", sql);
            Assert.DoesNotContain("DROP TABLE IF EXISTS", sql);
        }

        [Fact]
        [DisplayName("Oracle GetCommandText leaves a new field out of the INSERT ... SELECT list")]
        public void GetCommandText_AddedField_ExcludedFromDataCopy()
        {
            string sql = BuildSql(BuildDefineSchema(), BuildRealSchema());

            // The age field appears in the tmp table definition,
            Assert.Contains("\"AGE\"", sql);
            // but not in the INSERT ... SELECT clause.
            int insertIdx = sql.IndexOf("INSERT INTO \"TMP_ST_DEMO\"", StringComparison.Ordinal);
            int selectIdx = sql.IndexOf("FROM \"ST_DEMO\"", insertIdx, StringComparison.Ordinal);
            string insertSelectSection = sql.Substring(insertIdx, selectIdx - insertIdx);
            Assert.DoesNotContain("\"AGE\"", insertSelectSection);
        }

        [Fact]
        [DisplayName("Oracle GetCommandText keeps a real-only field (extension field) in the rebuild result")]
        public void GetCommandText_ExtensionField_Preserved()
        {
            string sql = BuildSql(BuildDefineSchema(), BuildRealSchema(withExtraLegacyField: true));

            Assert.Contains("\"LEGACY_COL\"", sql);
        }

        [Fact]
        [DisplayName("Oracle GetCommandText does not create non-PK indexes on the tmp table in advance")]
        public void GetCommandText_TmpTable_OmitsSecondaryIndexes()
        {
            string sql = BuildSql(BuildDefineSchema(), BuildRealSchema());

            // No ix_tmp_st_demo_name: non-PK indexes are created with their real names after the RENAME.
            Assert.DoesNotContain("ix_tmp_st_demo_name", sql);
        }

        [Fact]
        [DisplayName("Oracle GetCommandText recreates non-PK indexes with their real names after the RENAME")]
        public void GetCommandText_NonPkIndexes_RecreatedWithRealNames()
        {
            string sql = BuildSql(BuildDefineSchema(), BuildRealSchema());

            int renameIdx = sql.IndexOf("RENAME TO \"ST_DEMO\"", StringComparison.Ordinal);
            int recreateIdx = sql.IndexOf("CREATE INDEX \"IX_ST_DEMO_NAME\"", StringComparison.Ordinal);

            Assert.True(renameIdx > 0, "The RENAME step must be present");
            Assert.True(recreateIdx > renameIdx, "Non-PK indexes must be recreated after the RENAME");
            Assert.Contains("ON \"ST_DEMO\" (\"NAME\" ASC)", sql);
        }

        [Fact]
        [DisplayName("Oracle GetCommandText script contains no ALTER INDEX RENAME (consistent with the other dialects)")]
        public void GetCommandText_NeverEmitsAlterIndexRename()
        {
            string sql = BuildSql(BuildDefineSchema(), BuildRealSchema());

            Assert.DoesNotContain("ALTER INDEX", sql);
            Assert.DoesNotContain("RENAME INDEX", sql);
        }

        [Fact]
        [DisplayName("Oracle GetCommandText throws for a new-table diff (which must take the CREATE path)")]
        public void GetCommandText_NewTableDiff_Throws()
        {
            var define = BuildDefineSchema();
            var diff = new TableSchemaComparer(define, realTable: null, DatabaseType.Oracle).CompareToDiff();
            var rebuilder = new OracleDialectFactory().CreateTableRebuildCommandBuilder();

            Assert.Throws<InvalidOperationException>(() => rebuilder.GetCommandText(diff));
        }

        [Fact]
        [DisplayName("Oracle GetCommandText adds CASCADE CONSTRAINTS to DROP TABLE (removes referencing FKs)")]
        public void GetCommandText_DropTable_IncludesCascadeConstraints()
        {
            string sql = BuildSql(BuildDefineSchema(), BuildRealSchema());

            Assert.Contains("CASCADE CONSTRAINTS", sql);
        }

        [Fact]
        [DisplayName("Oracle GetCommandText script runs drop tmp, create tmp, insert, drop old, rename and recreate index in order")]
        public void GetCommandText_StepsInExpectedOrder()
        {
            string sql = BuildSql(BuildDefineSchema(), BuildRealSchema());

            int dropTmpIdx = sql.IndexOf("DROP TABLE \"TMP_ST_DEMO\"", StringComparison.Ordinal);
            int createTmpIdx = sql.IndexOf("CREATE TABLE \"TMP_ST_DEMO\"", StringComparison.Ordinal);
            int insertIdx = sql.IndexOf("INSERT INTO \"TMP_ST_DEMO\"", StringComparison.Ordinal);
            int dropOldIdx = sql.IndexOf("DROP TABLE \"ST_DEMO\"", StringComparison.Ordinal);
            int renameIdx = sql.IndexOf("RENAME TO \"ST_DEMO\"", StringComparison.Ordinal);
            int recreateIdx = sql.IndexOf("CREATE INDEX \"IX_ST_DEMO_NAME\"", StringComparison.Ordinal);

            Assert.True(dropTmpIdx >= 0 && createTmpIdx > dropTmpIdx, "drop tmp → create tmp");
            Assert.True(createTmpIdx > 0 && insertIdx > createTmpIdx, "create tmp → insert");
            Assert.True(insertIdx > 0 && dropOldIdx > insertIdx, "insert → drop old");
            Assert.True(dropOldIdx > 0 && renameIdx > dropOldIdx, "drop old → rename");
            Assert.True(renameIdx > 0 && recreateIdx > renameIdx, "rename → recreate index");
        }
    }
}
