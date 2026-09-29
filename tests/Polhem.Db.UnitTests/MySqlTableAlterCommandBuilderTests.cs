using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Providers.MySql;
using Polhem.Db.Schema;
using Polhem.Db.Schema.Changes;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Pure-syntax tests for <see cref="MySqlTableAlterCommandBuilder"/>. No live database
    /// connection — verifies the routing of <see cref="ITableChange"/> kinds to MySQL 8.0+
    /// dialect output (backtick quoting, MODIFY COLUMN re-definition, PK via DROP PRIMARY KEY).
    /// </summary>
    public class MySqlTableAlterCommandBuilderTests
    {
        private readonly MySqlTableAlterCommandBuilder _builder = new MySqlTableAlterCommandBuilder();

        private static DbTableIndex BuildIndex(string indexName, string field, bool unique)
        {
            var schema = new TableSchema { TableName = "st_demo" };
            return schema.Indexes!.Add(indexName, field, unique);
        }

        private static DbTableIndex BuildPrimaryKey(string field)
        {
            var schema = new TableSchema { TableName = "st_demo" };
            return schema.Indexes!.AddPrimaryKey(field);
        }

        // ---------- ExecutionKind ----------

        [Fact]
        [DisplayName("MySQL GetExecutionKind returns Alter for AddFieldChange")]
        public void GetExecutionKind_AddField_IsAlter()
        {
            var change = new AddFieldChange(new DbField("col", "Col", FieldDbType.Integer));
            Assert.Equal(ChangeExecutionKind.Alter, _builder.GetExecutionKind(change));
        }

        [Fact]
        [DisplayName("MySQL GetExecutionKind returns Alter for RenameFieldChange")]
        public void GetExecutionKind_Rename_IsAlter()
        {
            var change = new RenameFieldChange("oldname", new DbField("newname", "New", FieldDbType.String));
            Assert.Equal(ChangeExecutionKind.Alter, _builder.GetExecutionKind(change));
        }

        [Fact]
        [DisplayName("MySQL GetExecutionKind returns Alter for an AlterFieldChange within the family (Integer→Long)")]
        public void GetExecutionKind_AlterFieldSameFamily_IsAlter()
        {
            var oldField = new DbField("col", "Col", FieldDbType.Integer);
            var newField = new DbField("col", "Col", FieldDbType.Long);
            var change = new AlterFieldChange(oldField, newField);
            Assert.Equal(ChangeExecutionKind.Alter, _builder.GetExecutionKind(change));
        }

        [Fact]
        [DisplayName("MySQL GetExecutionKind returns Rebuild for an AlterFieldChange across families (Integer→String)")]
        public void GetExecutionKind_AlterFieldCrossFamily_IsRebuild()
        {
            var oldField = new DbField("col", "Col", FieldDbType.Integer);
            var newField = new DbField("col", "Col", FieldDbType.String) { Length = 50 };
            var change = new AlterFieldChange(oldField, newField);
            Assert.Equal(ChangeExecutionKind.Rebuild, _builder.GetExecutionKind(change));
        }

        // ---------- IsNarrowingChange ----------

        [Fact]
        [DisplayName("MySQL IsNarrowingChange returns true for a shorter String")]
        public void IsNarrowingChange_StringShortened_ReturnsTrue()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 100 };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 50 };
            Assert.True(_builder.IsNarrowingChange(new AlterFieldChange(oldField, newField)));
        }

        [Fact]
        [DisplayName("MySQL IsNarrowingChange returns false for a longer String")]
        public void IsNarrowingChange_StringExtended_ReturnsFalse()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 50 };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 100 };
            Assert.False(_builder.IsNarrowingChange(new AlterFieldChange(oldField, newField)));
        }

        // ---------- GetStatements ----------

        [Fact]
        [DisplayName("MySQL GetStatements for AddField produces ALTER TABLE ADD COLUMN (backtick identifiers)")]
        public void GetStatements_AddField_EmitsAlterTableAddColumn()
        {
            var field = new DbField("age", "Age", FieldDbType.Integer) { AllowNull = false };
            var statements = _builder.GetStatements("st_demo", new AddFieldChange(field));

            var sql = Assert.Single(statements);
            Assert.Contains("ALTER TABLE `st_demo` ADD COLUMN", sql);
            Assert.Contains("`age` INT NOT NULL DEFAULT 0", sql);
        }

        [Fact]
        [DisplayName("MySQL GetStatements splits AddField of a NOT NULL Guid in two (constant default, then SET DEFAULT (UUID())) to stay replication-safe")]
        public void GetStatements_AddGuidNotNull_SplitsIntoSafeTwoStep()
        {
            var field = new DbField("user_rowid", "User", FieldDbType.Guid) { AllowNull = false };
            var statements = _builder.GetStatements("st_employee", new AddFieldChange(field));

            Assert.Equal(2, statements.Count);
            // Statement 1: ADD with a constant empty Guid default, which
            // is replication-safe and must not contain (UUID()).
            Assert.Contains("ALTER TABLE `st_employee` ADD COLUMN `user_rowid`", statements[0]);
            Assert.Contains("NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'", statements[0]);
            Assert.DoesNotContain("UUID()", statements[0]);
            // Statement 2: a metadata-only restore of the real default (only new rows get UUID()).
            Assert.Contains("ALTER TABLE `st_employee` ALTER COLUMN `user_rowid` SET DEFAULT (UUID())", statements[1]);
        }

        [Fact]
        [DisplayName("MySQL GetStatements keeps AddField of a nullable Guid as one statement (no UUID() default, no split)")]
        public void GetStatements_AddGuidNullable_SingleStatement()
        {
            var field = new DbField("ref_rowid", "Ref", FieldDbType.Guid) { AllowNull = true };
            var statements = _builder.GetStatements("st_demo", new AddFieldChange(field));

            var sql = Assert.Single(statements);
            Assert.Contains("ADD COLUMN `ref_rowid`", sql);
            Assert.DoesNotContain("UUID()", sql);
        }

        [Fact]
        [DisplayName("MySQL GetStatements for AlterField produces MODIFY COLUMN with the full column definition")]
        public void GetStatements_AlterField_EmitsModifyColumn()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 50, AllowNull = false };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 100, AllowNull = false };
            var statements = _builder.GetStatements("st_demo", new AlterFieldChange(oldField, newField));

            var sql = Assert.Single(statements);
            Assert.Contains("ALTER TABLE `st_demo` MODIFY COLUMN", sql);
            Assert.Contains("`name` VARCHAR(100) NOT NULL", sql);
        }

        [Fact]
        [DisplayName("MySQL GetStatements for RenameField produces RENAME COLUMN (backticks)")]
        public void GetStatements_RenameField_EmitsRenameColumn()
        {
            var change = new RenameFieldChange("oldname", new DbField("newname", "New", FieldDbType.String) { Length = 50 });
            var statements = _builder.GetStatements("st_demo", change);

            var sql = Assert.Single(statements);
            Assert.Equal("ALTER TABLE `st_demo` RENAME COLUMN `oldname` TO `newname`;", sql);
        }

        [Fact]
        [DisplayName("MySQL GetStatements for AddIndex of a non-PK index produces CREATE INDEX")]
        public void GetStatements_AddIndex_NonPk_EmitsCreateIndex()
        {
            var index = BuildIndex("ix_{0}_col", "col", unique: false);
            var statements = _builder.GetStatements("st_demo", new AddIndexChange(index));

            var sql = Assert.Single(statements);
            Assert.Contains("CREATE INDEX `ix_st_demo_col` ON `st_demo`", sql);
        }

        [Fact]
        [DisplayName("MySQL GetStatements for AddIndex of a unique index produces CREATE UNIQUE INDEX")]
        public void GetStatements_AddIndex_Unique_EmitsCreateUniqueIndex()
        {
            var index = BuildIndex("uk_{0}_col", "col", unique: true);
            var statements = _builder.GetStatements("st_demo", new AddIndexChange(index));

            var sql = Assert.Single(statements);
            Assert.Contains("CREATE UNIQUE INDEX `uk_st_demo_col` ON `st_demo`", sql);
        }

        [Fact]
        [DisplayName("MySQL GetStatements for AddIndex of a PK produces ADD CONSTRAINT PRIMARY KEY")]
        public void GetStatements_AddIndex_Pk_EmitsAddConstraint()
        {
            var pk = BuildPrimaryKey("id");
            var statements = _builder.GetStatements("st_demo", new AddIndexChange(pk));

            var sql = Assert.Single(statements);
            Assert.Contains("ALTER TABLE `st_demo` ADD CONSTRAINT", sql);
            Assert.Contains("PRIMARY KEY (`id`", sql);
        }

        [Fact]
        [DisplayName("MySQL GetStatements for DropIndex of a non-PK index produces DROP INDEX ON")]
        public void GetStatements_DropIndex_NonPk_EmitsDropIndexOn()
        {
            var index = BuildIndex("ix_{0}_col", "col", unique: false);
            // Simulates an existing, already resolved index name, so the {0} replacement is skipped.
            index.Name = "ix_st_demo_col";
            var statements = _builder.GetStatements("st_demo", new DropIndexChange(index));

            var sql = Assert.Single(statements);
            Assert.Equal("DROP INDEX `ix_st_demo_col` ON `st_demo`;", sql);
        }

        [Fact]
        [DisplayName("MySQL GetStatements for DropIndex of a PK produces DROP PRIMARY KEY")]
        public void GetStatements_DropIndex_Pk_EmitsDropPrimaryKey()
        {
            var pk = BuildPrimaryKey("id");
            var statements = _builder.GetStatements("st_demo", new DropIndexChange(pk));

            var sql = Assert.Single(statements);
            Assert.Equal("ALTER TABLE `st_demo` DROP PRIMARY KEY;", sql);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("MySQL GetStatements throws for a blank tableName")]
        public void GetStatements_EmptyTableName_Throws(string? tableName)
        {
            var change = new AddFieldChange(new DbField("col", "Col", FieldDbType.Integer));
            Assert.ThrowsAny<ArgumentException>(() => _builder.GetStatements(tableName!, change));
        }
    }
}
