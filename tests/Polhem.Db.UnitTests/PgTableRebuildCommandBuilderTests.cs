using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Providers.PostgreSql;
using Polhem.Db.Schema;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class PgTableRebuildCommandBuilderTests : IClassFixture<SharedDbFixture>
    {
        public PgTableRebuildCommandBuilderTests(SharedDbFixture _) { }

        private static TableSchema BuildDefineSchema()
        {
            var schema = new TableSchema { TableName = "st_demo" };
            schema.Fields!.Add("id", "Id", FieldDbType.Guid);
            schema.Fields!.Add("name", "Name", FieldDbType.String, 50);
            schema.Fields!.Add("age", "Age", FieldDbType.Integer);
            schema.Indexes!.AddPrimaryKey("id");
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
        [DisplayName("PG GetCommandText rebuild script creates the tmp table, INSERTs and RENAMEs")]
        public void GetCommandText_BasicRebuild_IncludesTmpCreateInsertAndRename()
        {
            var define = BuildDefineSchema();
            var real = BuildRealSchema();
            var diff = new TableSchemaComparer(define, real, DatabaseType.PostgreSQL).CompareToDiff();

            var sql = new PgTableRebuildCommandBuilder().GetCommandText(diff);

            Assert.Contains("tmp_st_demo", sql);
            Assert.Contains("INSERT INTO \"tmp_st_demo\"", sql);
            Assert.Contains("ALTER TABLE \"tmp_st_demo\" RENAME TO \"st_demo\"", sql);
        }

        [Fact]
        [DisplayName("PG GetCommandText uses IF EXISTS for DROP TABLE (the PostgreSQL idiom)")]
        public void GetCommandText_DropTable_UsesIfExists()
        {
            var define = BuildDefineSchema();
            var real = BuildRealSchema();
            var diff = new TableSchemaComparer(define, real, DatabaseType.PostgreSQL).CompareToDiff();

            var sql = new PgTableRebuildCommandBuilder().GetCommandText(diff);

            Assert.Contains("DROP TABLE IF EXISTS \"tmp_st_demo\";", sql);
            Assert.Contains("DROP TABLE IF EXISTS \"st_demo\";", sql);
        }

        [Fact]
        [DisplayName("PG GetCommandText leaves a new field out of the INSERT ... SELECT list")]
        public void GetCommandText_AddedField_ExcludedFromDataCopy()
        {
            var define = BuildDefineSchema();
            var real = BuildRealSchema();
            var diff = new TableSchemaComparer(define, real, DatabaseType.PostgreSQL).CompareToDiff();

            var sql = new PgTableRebuildCommandBuilder().GetCommandText(diff);

            // The new age field appears in the tmp definition,
            Assert.Contains("\"age\"", sql);
            // but not in the INSERT column list.
            int insertIdx = sql.IndexOf("INSERT INTO \"tmp_st_demo\"", StringComparison.Ordinal);
            int selectIdx = sql.IndexOf("FROM \"st_demo\"", insertIdx, StringComparison.Ordinal);
            string insertSelectSection = sql.Substring(insertIdx, selectIdx - insertIdx);
            Assert.DoesNotContain("\"age\"", insertSelectSection);
        }

        [Fact]
        [DisplayName("PG GetCommandText keeps a real-only field (extension field) in the rebuild result")]
        public void GetCommandText_ExtensionField_Preserved()
        {
            var define = BuildDefineSchema();
            var real = BuildRealSchema(withExtraLegacyField: true);
            var diff = new TableSchemaComparer(define, real, DatabaseType.PostgreSQL).CompareToDiff();

            var sql = new PgTableRebuildCommandBuilder().GetCommandText(diff);

            Assert.Contains("\"legacy_col\"", sql);
        }

        [Fact]
        [DisplayName("PG GetCommandText renames indexes with ALTER INDEX RENAME TO")]
        public void GetCommandText_RenameIndex_UsesAlterIndex()
        {
            var define = BuildDefineSchema();
            var real = BuildRealSchema();
            var diff = new TableSchemaComparer(define, real, DatabaseType.PostgreSQL).CompareToDiff();

            var sql = new PgTableRebuildCommandBuilder().GetCommandText(diff);

            Assert.Contains("ALTER INDEX", sql);
            Assert.Contains("RENAME TO", sql);
        }

        [Fact]
        [DisplayName("PG GetCommandText throws for a new-table diff (which must take the CREATE path)")]
        public void GetCommandText_NewTableDiff_Throws()
        {
            var define = BuildDefineSchema();
            var diff = new TableSchemaComparer(define, realTable: null, DatabaseType.PostgreSQL).CompareToDiff();

            Assert.Throws<InvalidOperationException>(() => new PgTableRebuildCommandBuilder().GetCommandText(diff));
        }
    }
}
