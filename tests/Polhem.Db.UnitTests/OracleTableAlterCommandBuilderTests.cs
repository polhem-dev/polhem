using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.Oracle;
using Polhem.Db.Schema;
using Polhem.Db.Schema.Changes;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Pure-syntax tests for <see cref="OracleTableAlterCommandBuilder"/>. No live database
    /// connection — verifies the routing of <see cref="ITableChange"/> kinds to Oracle 19c+
    /// dialect output (double-quote quoting, parenthesised ADD/MODIFY, RENAME COLUMN,
    /// DROP INDEX without ON tablename, DROP PRIMARY KEY for PK).
    /// </summary>
    public class OracleTableAlterCommandBuilderTests
    {
        private readonly OracleTableAlterCommandBuilder _builder = new OracleTableAlterCommandBuilder();

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
        [DisplayName("Oracle GetExecutionKind returns Alter for AddFieldChange")]
        public void GetExecutionKind_AddField_IsAlter()
        {
            var change = new AddFieldChange(new DbField("col", "Col", FieldDbType.Integer));
            Assert.Equal(ChangeExecutionKind.Alter, _builder.GetExecutionKind(change));
        }

        [Fact]
        [DisplayName("Oracle GetExecutionKind returns Alter for RenameFieldChange")]
        public void GetExecutionKind_Rename_IsAlter()
        {
            var change = new RenameFieldChange("oldname", new DbField("newname", "New", FieldDbType.String));
            Assert.Equal(ChangeExecutionKind.Alter, _builder.GetExecutionKind(change));
        }

        [Fact]
        [DisplayName("Oracle GetExecutionKind returns Alter for an AlterFieldChange within the family (Integer→Long)")]
        public void GetExecutionKind_AlterFieldSameFamily_IsAlter()
        {
            var oldField = new DbField("col", "Col", FieldDbType.Integer);
            var newField = new DbField("col", "Col", FieldDbType.Long);
            var change = new AlterFieldChange(oldField, newField);
            Assert.Equal(ChangeExecutionKind.Alter, _builder.GetExecutionKind(change));
        }

        [Fact]
        [DisplayName("Oracle GetExecutionKind returns Rebuild for an AlterFieldChange across families (Integer→String)")]
        public void GetExecutionKind_AlterFieldCrossFamily_IsRebuild()
        {
            var oldField = new DbField("col", "Col", FieldDbType.Integer);
            var newField = new DbField("col", "Col", FieldDbType.String) { Length = 50 };
            var change = new AlterFieldChange(oldField, newField);
            Assert.Equal(ChangeExecutionKind.Rebuild, _builder.GetExecutionKind(change));
        }

        // ---------- IsNarrowingChange ----------

        [Fact]
        [DisplayName("Oracle IsNarrowingChange returns true for a shorter String")]
        public void IsNarrowingChange_StringShortened_ReturnsTrue()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 100 };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 50 };
            Assert.True(_builder.IsNarrowingChange(new AlterFieldChange(oldField, newField)));
        }

        [Fact]
        [DisplayName("Oracle IsNarrowingChange returns false for a longer String")]
        public void IsNarrowingChange_StringExtended_ReturnsFalse()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 50 };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 100 };
            Assert.False(_builder.IsNarrowingChange(new AlterFieldChange(oldField, newField)));
        }

        [Fact]
        [DisplayName("Oracle IsNarrowingChange returns true for a smaller Decimal precision")]
        public void IsNarrowingChange_DecimalPrecisionReduced_ReturnsTrue()
        {
            var oldField = new DbField("amount", "Amount", FieldDbType.Decimal) { Precision = 18, Scale = 4 };
            var newField = new DbField("amount", "Amount", FieldDbType.Decimal) { Precision = 12, Scale = 4 };
            Assert.True(_builder.IsNarrowingChange(new AlterFieldChange(oldField, newField)));
        }

        // ---------- GetStatements ----------

        [Fact]
        [DisplayName("Oracle GetStatements for AddField produces ALTER TABLE ADD (...) (double-quoted identifiers and parentheses)")]
        public void GetStatements_AddField_EmitsAlterTableAdd()
        {
            var field = new DbField("age", "Age", FieldDbType.Integer) { AllowNull = false };
            var statements = _builder.GetStatements("st_demo", new AddFieldChange(field));

            var sql = Assert.Single(statements);
            Assert.Contains("ALTER TABLE \"ST_DEMO\" ADD (", sql);
            Assert.Contains("\"AGE\" NUMBER(10) DEFAULT 0 NOT NULL", sql);
            Assert.EndsWith(");", sql);
        }

        [Fact]
        [DisplayName("Oracle GetStatements for an AlterField that only changes the type does not restate nullability in MODIFY (avoids ORA-01442)")]
        public void GetStatements_AlterField_TypeOnlyChange_OmitsNullability()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 50, AllowNull = false };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 100, AllowNull = false };
            var statements = _builder.GetStatements("st_demo", new AlterFieldChange(oldField, newField));

            var sql = Assert.Single(statements);
            Assert.Contains("ALTER TABLE \"ST_DEMO\" MODIFY (", sql);
            Assert.Contains("\"NAME\" VARCHAR2(100 CHAR)", sql);
            // Nullability is unchanged (String is always nullable, NULL
            // on both sides), so no NULL/NOT NULL hint is emitted.
            Assert.DoesNotContain("NULL", sql);
            Assert.EndsWith(");", sql);
        }

        [Fact]
        [DisplayName("Oracle GetStatements for an AlterField from NULL to NOT NULL carries NOT NULL")]
        public void GetStatements_AlterField_NullToNotNull_EmitsNotNull()
        {
            var oldField = new DbField("age", "Age", FieldDbType.Integer) { AllowNull = true };
            var newField = new DbField("age", "Age", FieldDbType.Integer) { AllowNull = false };
            var statements = _builder.GetStatements("st_demo", new AlterFieldChange(oldField, newField));

            var sql = Assert.Single(statements);
            Assert.Contains("ALTER TABLE \"ST_DEMO\" MODIFY (", sql);
            Assert.Contains("NOT NULL", sql);
        }

        [Fact]
        [DisplayName("Oracle GetStatements for an AlterField from NOT NULL to NULL carries NULL")]
        public void GetStatements_AlterField_NotNullToNull_EmitsNull()
        {
            var oldField = new DbField("age", "Age", FieldDbType.Integer) { AllowNull = false };
            var newField = new DbField("age", "Age", FieldDbType.Integer) { AllowNull = true };
            var statements = _builder.GetStatements("st_demo", new AlterFieldChange(oldField, newField));

            var sql = Assert.Single(statements);
            Assert.Contains("ALTER TABLE \"ST_DEMO\" MODIFY (", sql);
            Assert.Contains(" NULL", sql);
            Assert.DoesNotContain("NOT NULL", sql);
        }

        [Fact]
        [DisplayName("Oracle GetStatements for an AlterField with unchanged nullability (NOT NULL→NOT NULL) does not restate it (regression for ORA-01442)")]
        public void GetStatements_AlterField_RedundantNotNull_OmitsNullability()
        {
            // Only the default changes, not the nullability: MODIFY must not carry NOT NULL, or Oracle raises ORA-01442
            // on a column that is already NOT NULL.
            var oldField = new DbField("age", "Age", FieldDbType.Integer) { AllowNull = false, DefaultValue = "0" };
            var newField = new DbField("age", "Age", FieldDbType.Integer) { AllowNull = false, DefaultValue = "1" };
            var statements = _builder.GetStatements("st_demo", new AlterFieldChange(oldField, newField));

            var sql = Assert.Single(statements);
            Assert.Contains("ALTER TABLE \"ST_DEMO\" MODIFY (", sql);
            Assert.DoesNotContain("NOT NULL", sql);
        }

        [Fact]
        [DisplayName("Oracle GetStatements for AlterField does not use the MODIFY COLUMN keyword (Oracle puts the column definition right after MODIFY)")]
        public void GetStatements_AlterField_DoesNotEmitModifyColumn()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 50 };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 100 };
            var statements = _builder.GetStatements("st_demo", new AlterFieldChange(oldField, newField));

            var sql = Assert.Single(statements);
            Assert.DoesNotContain("MODIFY COLUMN", sql);
        }

        // ---------- LOB (CLOB / BLOB) ---------- regression for ORA-22859 / ORA-22858

        [Fact]
        [DisplayName("Oracle GetStatements MODIFY from Text to Text does not restate the CLOB type (regression for ORA-22859)")]
        public void GetStatements_AlterField_TextToText_NeverRestatesClob()
        {
            // Oracle raises ORA-22859 on a MODIFY of a LOB column as soon as it carries a type, even an unchanged one.
            var oldField = new DbField("error_message", "Msg", FieldDbType.Text) { AllowNull = false };
            var newField = new DbField("error_message", "Msg", FieldDbType.Text) { AllowNull = true };
            var statements = _builder.GetStatements("st_demo", new AlterFieldChange(oldField, newField));

            Assert.DoesNotContain(statements, sql => sql.Contains("CLOB", StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("Oracle GetStatements produces no statement for a LOB column with nothing to change (MODIFY (\"COL\") is not valid syntax)")]
        public void GetStatements_AlterField_LobWithNothingModifiable_EmitsNoStatement()
        {
            // On both sides Text is always NULL and its default always empty, so MODIFY has nothing valid to carry.
            var oldField = new DbField("error_message", "Msg", FieldDbType.Text) { AllowNull = false };
            var newField = new DbField("error_message", "Msg", FieldDbType.Text) { AllowNull = true };
            var statements = _builder.GetStatements("st_demo", new AlterFieldChange(oldField, newField));

            Assert.Empty(statements);
        }

        [Fact]
        [DisplayName("Oracle GetStatements emits only DEFAULT, without the type, for a default change on a LOB column")]
        public void GetStatements_AlterField_LobDefaultChange_EmitsDefaultWithoutType()
        {
            // A String with Length > 4000 maps to CLOB, so it is subject
            // to ORA-22859 as well, but its DEFAULT can change.
            var oldField = new DbField("blob_text", "Text", FieldDbType.String) { Length = 5000, AllowNull = false };
            var newField = new DbField("blob_text", "Text", FieldDbType.String) { Length = 5000, AllowNull = false, DefaultValue = "x" };
            var statements = _builder.GetStatements("st_demo", new AlterFieldChange(oldField, newField));

            var sql = Assert.Single(statements);
            Assert.Equal("ALTER TABLE \"ST_DEMO\" MODIFY (\"BLOB_TEXT\" DEFAULT 'x');", sql);
        }

        [Fact]
        [DisplayName("Oracle GetStatements MODIFY from Binary to Binary does not restate the BLOB type (regression for ORA-22859)")]
        public void GetStatements_AlterField_BinaryToBinary_NeverRestatesBlob()
        {
            var oldField = new DbField("payload", "Payload", FieldDbType.Binary) { AllowNull = true };
            var newField = new DbField("payload", "Payload", FieldDbType.Binary) { AllowNull = false };
            var statements = _builder.GetStatements("st_demo", new AlterFieldChange(oldField, newField));

            var sql = Assert.Single(statements);
            Assert.DoesNotContain("BLOB", sql, StringComparison.Ordinal);
            Assert.Equal("ALTER TABLE \"ST_DEMO\" MODIFY (\"PAYLOAD\" NOT NULL);", sql);
        }

        [Fact]
        [DisplayName("Oracle GetExecutionKind returns Rebuild for String→Text (VARCHAR2→CLOB) (regression for ORA-22858)")]
        public void GetExecutionKind_StringToText_IsRebuild()
        {
            // Both belong to the dialect-neutral String family, so an in-place ALTER would be chosen by default,
            // but Oracle cannot cross the LOB boundary with MODIFY.
            var oldField = new DbField("note", "Note", FieldDbType.String) { Length = 100 };
            var newField = new DbField("note", "Note", FieldDbType.Text);
            Assert.Equal(ChangeExecutionKind.Rebuild, _builder.GetExecutionKind(new AlterFieldChange(oldField, newField)));
        }

        [Fact]
        [DisplayName("Oracle GetExecutionKind returns Rebuild for Text→String (CLOB→VARCHAR2) (regression for ORA-22859)")]
        public void GetExecutionKind_TextToString_IsRebuild()
        {
            var oldField = new DbField("note", "Note", FieldDbType.Text);
            var newField = new DbField("note", "Note", FieldDbType.String) { Length = 100 };
            Assert.Equal(ChangeExecutionKind.Rebuild, _builder.GetExecutionKind(new AlterFieldChange(oldField, newField)));
        }

        [Fact]
        [DisplayName("Oracle GetExecutionKind returns Rebuild when a String length crosses the VARCHAR2 limit")]
        public void GetExecutionKind_StringCrossingVarcharCeiling_IsRebuild()
        {
            // Both sides are `FieldDbType.String`, but 5000 falls into CLOB and 100 into VARCHAR2.
            var oldField = new DbField("note", "Note", FieldDbType.String) { Length = 100 };
            var newField = new DbField("note", "Note", FieldDbType.String) { Length = 5000 };
            Assert.Equal(ChangeExecutionKind.Rebuild, _builder.GetExecutionKind(new AlterFieldChange(oldField, newField)));
        }

        [Fact]
        [DisplayName("Oracle GetStatements for RenameField produces RENAME COLUMN (double quotes)")]
        public void GetStatements_RenameField_EmitsRenameColumn()
        {
            var change = new RenameFieldChange("oldname", new DbField("newname", "New", FieldDbType.String) { Length = 50 });
            var statements = _builder.GetStatements("st_demo", change);

            var sql = Assert.Single(statements);
            Assert.Equal("ALTER TABLE \"ST_DEMO\" RENAME COLUMN \"OLDNAME\" TO \"NEWNAME\";", sql);
        }

        [Fact]
        [DisplayName("Oracle GetStatements for AddIndex of a non-PK index produces CREATE INDEX with ASC")]
        public void GetStatements_AddIndex_NonPk_EmitsCreateIndex()
        {
            var index = BuildIndex("ix_{0}_col", "col", unique: false);
            var statements = _builder.GetStatements("st_demo", new AddIndexChange(index));

            var sql = Assert.Single(statements);
            Assert.Contains("CREATE INDEX \"IX_ST_DEMO_COL\" ON \"ST_DEMO\"", sql);
            Assert.Contains("\"COL\" ASC", sql);
        }

        [Fact]
        [DisplayName("Oracle GetStatements for AddIndex of a unique index produces CREATE UNIQUE INDEX")]
        public void GetStatements_AddIndex_Unique_EmitsCreateUniqueIndex()
        {
            var index = BuildIndex("uk_{0}_col", "col", unique: true);
            var statements = _builder.GetStatements("st_demo", new AddIndexChange(index));

            var sql = Assert.Single(statements);
            Assert.Contains("CREATE UNIQUE INDEX \"UK_ST_DEMO_COL\" ON \"ST_DEMO\"", sql);
        }

        [Fact]
        [DisplayName("Oracle GetStatements for AddIndex of a PK produces ADD CONSTRAINT PRIMARY KEY without ASC/DESC")]
        public void GetStatements_AddIndex_Pk_EmitsAddConstraint()
        {
            var pk = BuildPrimaryKey("id");
            var statements = _builder.GetStatements("st_demo", new AddIndexChange(pk));

            var sql = Assert.Single(statements);
            Assert.Contains("ALTER TABLE \"ST_DEMO\" ADD CONSTRAINT \"PK_ST_DEMO\"", sql);
            Assert.Contains("PRIMARY KEY (\"ID\")", sql);
            // Columns inside an Oracle PK constraint do not accept ASC/DESC.
            Assert.DoesNotContain("PRIMARY KEY (\"ID\" ASC", sql);
        }

        [Fact]
        [DisplayName("Oracle GetStatements for DropIndex of a non-PK index produces DROP INDEX (without ON tablename)")]
        public void GetStatements_DropIndex_NonPk_EmitsDropIndex()
        {
            var index = BuildIndex("ix_{0}_col", "col", unique: false);
            // Simulates an existing, already resolved index name, so the {0} replacement is skipped.
            index.Name = "ix_st_demo_col";
            var statements = _builder.GetStatements("st_demo", new DropIndexChange(index));

            var sql = Assert.Single(statements);
            // Unlike MySQL, Oracle's DROP INDEX takes no ON tablename.
            Assert.Equal("DROP INDEX \"IX_ST_DEMO_COL\";", sql);
            Assert.DoesNotContain("ON \"ST_DEMO\"", sql);
        }

        [Fact]
        [DisplayName("Oracle GetStatements for DropIndex of a PK produces DROP PRIMARY KEY")]
        public void GetStatements_DropIndex_Pk_EmitsDropPrimaryKey()
        {
            var pk = BuildPrimaryKey("id");
            var statements = _builder.GetStatements("st_demo", new DropIndexChange(pk));

            var sql = Assert.Single(statements);
            Assert.Equal("ALTER TABLE \"ST_DEMO\" DROP PRIMARY KEY;", sql);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("Oracle GetStatements throws for a blank tableName")]
        public void GetStatements_EmptyTableName_Throws(string? tableName)
        {
            var change = new AddFieldChange(new DbField("col", "Col", FieldDbType.Integer));
            Assert.ThrowsAny<ArgumentException>(() => _builder.GetStatements(tableName!, change));
        }
    }
}
