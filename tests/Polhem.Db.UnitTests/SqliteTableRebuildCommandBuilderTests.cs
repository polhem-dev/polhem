using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.Sqlite;
using Polhem.Db.Schema;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class SqliteTableRebuildCommandBuilderTests : IClassFixture<SharedDbFixture>
    {
        public SqliteTableRebuildCommandBuilderTests(SharedDbFixture _) { }

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

        [Fact]
        [DisplayName("SQLite GetCommandText rebuild script creates the tmp table, INSERTs and RENAMEs")]
        public void GetCommandText_BasicRebuild_IncludesTmpCreateInsertAndRename()
        {
            var define = BuildDefineSchema();
            var real = BuildRealSchema();
            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLite).CompareToDiff();

            var sql = new SqliteTableRebuildCommandBuilder().GetCommandText(diff);

            Assert.Contains("tmp_st_demo", sql);
            Assert.Contains("INSERT INTO \"tmp_st_demo\"", sql);
            Assert.Contains("ALTER TABLE \"tmp_st_demo\" RENAME TO \"st_demo\"", sql);
        }

        [Fact]
        [DisplayName("SQLite GetCommandText uses IF EXISTS for DROP TABLE")]
        public void GetCommandText_DropTable_UsesIfExists()
        {
            var define = BuildDefineSchema();
            var real = BuildRealSchema();
            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLite).CompareToDiff();

            var sql = new SqliteTableRebuildCommandBuilder().GetCommandText(diff);

            Assert.Contains("DROP TABLE IF EXISTS \"tmp_st_demo\";", sql);
            Assert.Contains("DROP TABLE IF EXISTS \"st_demo\";", sql);
        }

        [Fact]
        [DisplayName("SQLite GetCommandText leaves a new field out of the INSERT ... SELECT list")]
        public void GetCommandText_AddedField_ExcludedFromDataCopy()
        {
            var define = BuildDefineSchema();
            var real = BuildRealSchema();
            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLite).CompareToDiff();

            var sql = new SqliteTableRebuildCommandBuilder().GetCommandText(diff);

            // The age field appears in the tmp table definition,
            Assert.Contains("\"age\"", sql);
            // but not in the INSERT column list.
            int insertIdx = sql.IndexOf("INSERT INTO \"tmp_st_demo\"", StringComparison.Ordinal);
            int selectIdx = sql.IndexOf("FROM \"st_demo\"", insertIdx, StringComparison.Ordinal);
            string insertSelectSection = sql.Substring(insertIdx, selectIdx - insertIdx);
            Assert.DoesNotContain("\"age\"", insertSelectSection);
        }

        [Fact]
        [DisplayName("SQLite GetCommandText keeps a real-only field (extension field) in the rebuild result")]
        public void GetCommandText_ExtensionField_Preserved()
        {
            var define = BuildDefineSchema();
            var real = BuildRealSchema(withExtraLegacyField: true);
            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLite).CompareToDiff();

            var sql = new SqliteTableRebuildCommandBuilder().GetCommandText(diff);

            Assert.Contains("\"legacy_col\"", sql);
        }

        [Fact]
        [DisplayName("SQLite GetCommandText does not create non-PK indexes on the tmp table in advance (there is no ALTER INDEX RENAME)")]
        public void GetCommandText_TmpTable_OmitsSecondaryIndexes()
        {
            var define = BuildDefineSchema();
            var real = BuildRealSchema();
            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLite).CompareToDiff();

            var sql = new SqliteTableRebuildCommandBuilder().GetCommandText(diff);

            // No ix_tmp_st_demo_name: non-PK indexes are created with their real names after the RENAME.
            Assert.DoesNotContain("ix_tmp_st_demo_name", sql);
        }

        [Fact]
        [DisplayName("SQLite GetCommandText recreates non-PK indexes with their real names after the RENAME")]
        public void GetCommandText_NonPkIndexes_RecreatedWithRealNames()
        {
            var define = BuildDefineSchema();
            var real = BuildRealSchema();
            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLite).CompareToDiff();

            var sql = new SqliteTableRebuildCommandBuilder().GetCommandText(diff);

            int renameIdx = sql.IndexOf("RENAME TO \"st_demo\"", StringComparison.Ordinal);
            int recreateIdx = sql.IndexOf("CREATE INDEX \"ix_st_demo_name\"", StringComparison.Ordinal);

            Assert.True(renameIdx > 0, "The RENAME step must be present");
            Assert.True(recreateIdx > renameIdx, "Non-PK indexes must be recreated after the RENAME");
            Assert.Contains("ON \"st_demo\" (\"name\" ASC)", sql);
        }

        [Fact]
        [DisplayName("SQLite GetCommandText script contains no ALTER INDEX RENAME (SQLite does not support it)")]
        public void GetCommandText_NeverEmitsAlterIndexRename()
        {
            var define = BuildDefineSchema();
            var real = BuildRealSchema();
            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLite).CompareToDiff();

            var sql = new SqliteTableRebuildCommandBuilder().GetCommandText(diff);

            Assert.DoesNotContain("ALTER INDEX", sql);
        }

        [Fact]
        [DisplayName("SQLite GetCommandText throws for a new-table diff (which must take the CREATE path)")]
        public void GetCommandText_NewTableDiff_Throws()
        {
            var define = BuildDefineSchema();
            var diff = new TableSchemaComparer(define, realTable: null, DatabaseType.SQLite).CompareToDiff();

            Assert.Throws<InvalidOperationException>(() => new SqliteTableRebuildCommandBuilder().GetCommandText(diff));
        }
    }
}
