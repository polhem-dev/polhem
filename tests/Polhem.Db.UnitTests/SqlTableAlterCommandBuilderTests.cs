using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Providers.SqlServer;
using Polhem.Db.Schema;
using Polhem.Db.Schema.Changes;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class SqlTableAlterCommandBuilderTests : IClassFixture<SharedDbFixture>
    {
        public SqlTableAlterCommandBuilderTests(SharedDbFixture _) { }

        private readonly SqlTableAlterCommandBuilder _builder = new();

        // ---------- GetExecutionKind ----------

        [Fact]
        [DisplayName("GetExecutionKind returns Alter for AddFieldChange")]
        public void GetExecutionKind_AddField_ReturnsAlter()
        {
            var change = new AddFieldChange(new DbField("age", "Age", FieldDbType.Integer));

            Assert.Equal(ChangeExecutionKind.Alter, _builder.GetExecutionKind(change));
        }

        [Fact]
        [DisplayName("GetExecutionKind returns Alter for an AlterFieldChange within the family")]
        public void GetExecutionKind_AlterFieldSameFamily_ReturnsAlter()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 50 };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 100 };
            var change = new AlterFieldChange(oldField, newField);

            Assert.Equal(ChangeExecutionKind.Alter, _builder.GetExecutionKind(change));
        }

        [Fact]
        [DisplayName("GetExecutionKind returns Rebuild for an AlterFieldChange across families")]
        public void GetExecutionKind_AlterFieldCrossFamily_ReturnsRebuild()
        {
            var oldField = new DbField("v", "V", FieldDbType.String) { Length = 50 };
            var newField = new DbField("v", "V", FieldDbType.Integer);
            var change = new AlterFieldChange(oldField, newField);

            Assert.Equal(ChangeExecutionKind.Rebuild, _builder.GetExecutionKind(change));
        }

        [Fact]
        [DisplayName("GetExecutionKind returns Rebuild for an AlterFieldChange that toggles AutoIncrement")]
        public void GetExecutionKind_AlterFieldAutoIncrementToggle_ReturnsRebuild()
        {
            var oldField = new DbField("id", "Id", FieldDbType.Integer);
            var newField = new DbField("id", "Id", FieldDbType.AutoIncrement);
            var change = new AlterFieldChange(oldField, newField);

            Assert.Equal(ChangeExecutionKind.Rebuild, _builder.GetExecutionKind(change));
        }

        [Fact]
        [DisplayName("GetExecutionKind returns Alter for AddIndexChange")]
        public void GetExecutionKind_AddIndex_ReturnsAlter()
        {
            var index = new DbTableIndex { Name = "ix_demo_name" };
            index.IndexFields!.Add("name");

            Assert.Equal(ChangeExecutionKind.Alter, _builder.GetExecutionKind(new AddIndexChange(index)));
        }

        [Fact]
        [DisplayName("GetExecutionKind returns Alter for DropIndexChange")]
        public void GetExecutionKind_DropIndex_ReturnsAlter()
        {
            var index = new DbTableIndex { Name = "ix_demo_name" };
            index.IndexFields!.Add("name");

            Assert.Equal(ChangeExecutionKind.Alter, _builder.GetExecutionKind(new DropIndexChange(index)));
        }

        // ---------- IsNarrowingChange ----------

        [Fact]
        [DisplayName("IsNarrowingChange returns false for a change other than AlterField")]
        public void IsNarrowingChange_NonAlterChange_ReturnsFalse()
        {
            var change = new AddFieldChange(new DbField("age", "Age", FieldDbType.Integer));

            Assert.False(_builder.IsNarrowingChange(change));
        }

        [Fact]
        [DisplayName("IsNarrowingChange returns true for a shorter String")]
        public void IsNarrowingChange_StringShortened_ReturnsTrue()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 100 };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 50 };

            Assert.True(_builder.IsNarrowingChange(new AlterFieldChange(oldField, newField)));
        }

        // ---------- AddField statements ----------

        [Fact]
        [DisplayName("GetStatements for AddField produces ALTER TABLE ADD with DEFAULT")]
        public void GetStatements_AddField_EmitsAlterTableAdd()
        {
            var field = new DbField("age", "Age", FieldDbType.Integer) { AllowNull = false };
            var statements = _builder.GetStatements("st_demo", new AddFieldChange(field));

            var sql = Assert.Single(statements);
            Assert.Contains("ALTER TABLE [st_demo] ADD", sql);
            Assert.Contains("[age] [int] NOT NULL", sql);
            Assert.Contains("DEFAULT", sql);
        }

        [Fact]
        [DisplayName("GetStatements for AddField of a nullable field produces no DEFAULT")]
        public void GetStatements_AddFieldNullable_NoDefault()
        {
            var field = new DbField("note", "Note", FieldDbType.String) { Length = 100, AllowNull = true };
            var statements = _builder.GetStatements("st_demo", new AddFieldChange(field));

            var sql = Assert.Single(statements);
            Assert.Contains("[note] [nvarchar](100) NULL", sql);
            Assert.DoesNotContain("DEFAULT", sql);
        }

        // ---------- AlterField statements ----------

        [Fact]
        [DisplayName("GetStatements for an AlterField that only changes the length produces drop-default, ALTER COLUMN and add-default")]
        public void GetStatements_AlterFieldLengthChanged_EmitsThreeStatements()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 50, AllowNull = false };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 100, AllowNull = false };
            var statements = _builder.GetStatements("st_demo", new AlterFieldChange(oldField, newField));

            Assert.Equal(3, statements.Count);
            Assert.Contains("DROP CONSTRAINT", statements[0]);
            Assert.Contains("ALTER COLUMN [name] [nvarchar](100) NOT NULL", statements[1]);
            Assert.Contains("ADD CONSTRAINT [df_st_demo_name] DEFAULT", statements[2]);
        }

        [Fact]
        [DisplayName("GetStatements for an AlterField from NOT NULL to NULL runs ALTER COLUMN and adds no default")]
        public void GetStatements_AlterFieldToNullable_NoAddDefault()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 50, AllowNull = false };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 50, AllowNull = true };
            var statements = _builder.GetStatements("st_demo", new AlterFieldChange(oldField, newField));

            Assert.Equal(2, statements.Count);
            Assert.Contains("DROP CONSTRAINT", statements[0]);
            Assert.Contains("ALTER COLUMN [name] [nvarchar](50) NULL", statements[1]);
            Assert.DoesNotContain(statements, s => s.Contains("ADD CONSTRAINT") && s.Contains("DEFAULT"));
        }

        [Fact]
        [DisplayName("GetStatements for an AlterField that only changes the default contains no ALTER COLUMN")]
        public void GetStatements_AlterFieldDefaultOnly_NoAlterColumn()
        {
            var oldField = new DbField("code", "Code", FieldDbType.String) { Length = 10, AllowNull = false, DefaultValue = "A" };
            var newField = new DbField("code", "Code", FieldDbType.String) { Length = 10, AllowNull = false, DefaultValue = "B" };
            var statements = _builder.GetStatements("st_demo", new AlterFieldChange(oldField, newField));

            Assert.Equal(2, statements.Count);
            Assert.Contains("DROP CONSTRAINT", statements[0]);
            Assert.DoesNotContain(statements, s => s.Contains("ALTER COLUMN"));
            Assert.Contains("ADD CONSTRAINT [df_st_demo_code] DEFAULT", statements[1]);
        }

        // ---------- AddIndex statements ----------

        [Fact]
        [DisplayName("GetStatements for AddIndex of a non-unique index produces CREATE INDEX")]
        public void GetStatements_AddRegularIndex_EmitsCreateIndex()
        {
            var index = new DbTableIndex { Name = "ix_{0}_name" };
            index.IndexFields!.Add("name");
            var statements = _builder.GetStatements("st_demo", new AddIndexChange(index));

            var sql = Assert.Single(statements);
            Assert.Equal("CREATE INDEX [ix_st_demo_name] ON [st_demo] ([name] ASC);", sql);
        }

        [Fact]
        [DisplayName("GetStatements for AddIndex of a unique index includes UNIQUE")]
        public void GetStatements_AddUniqueIndex_EmitsUniqueClause()
        {
            var index = new DbTableIndex { Name = "ix_{0}_name", Unique = true };
            index.IndexFields!.Add("name");
            var statements = _builder.GetStatements("st_demo", new AddIndexChange(index));

            var sql = Assert.Single(statements);
            Assert.Contains("CREATE UNIQUE INDEX", sql);
        }

        [Fact]
        [DisplayName("GetStatements for AddIndex of a primary key produces ALTER TABLE ADD CONSTRAINT PRIMARY KEY")]
        public void GetStatements_AddPrimaryKey_EmitsAddConstraintPrimaryKey()
        {
            var index = new DbTableIndex { Name = "pk_{0}", PrimaryKey = true, Unique = true };
            index.IndexFields!.Add("id");
            var statements = _builder.GetStatements("st_demo", new AddIndexChange(index));

            var sql = Assert.Single(statements);
            Assert.Equal("ALTER TABLE [st_demo] ADD CONSTRAINT [pk_st_demo] PRIMARY KEY ([id] ASC);", sql);
        }

        // ---------- DropIndex statements ----------

        [Fact]
        [DisplayName("GetStatements for DropIndex of a non-primary key produces DROP INDEX")]
        public void GetStatements_DropRegularIndex_EmitsDropIndex()
        {
            var index = new DbTableIndex { Name = "ix_st_demo_name" };
            index.IndexFields!.Add("name");
            var statements = _builder.GetStatements("st_demo", new DropIndexChange(index));

            var sql = Assert.Single(statements);
            Assert.Equal("DROP INDEX [ix_st_demo_name] ON [st_demo];", sql);
        }

        [Fact]
        [DisplayName("GetStatements for DropIndex of a primary key produces ALTER TABLE DROP CONSTRAINT")]
        public void GetStatements_DropPrimaryKey_EmitsDropConstraint()
        {
            var index = new DbTableIndex { Name = "pk_st_demo", PrimaryKey = true };
            index.IndexFields!.Add("id");
            var statements = _builder.GetStatements("st_demo", new DropIndexChange(index));

            var sql = Assert.Single(statements);
            Assert.Equal("ALTER TABLE [st_demo] DROP CONSTRAINT [pk_st_demo];", sql);
        }

        // ---------- Edge cases ----------

        [Fact]
        [DisplayName("GetStatements throws for a null tableName")]
        public void GetStatements_NullTableName_Throws()
        {
            var change = new AddFieldChange(new DbField("a", "A", FieldDbType.Integer));

            Assert.ThrowsAny<ArgumentException>(() => _builder.GetStatements(null!, change));
        }

        // ---------- RenameFieldChange ----------

        [Fact]
        [DisplayName("GetExecutionKind returns Alter for RenameFieldChange")]
        public void GetExecutionKind_RenameField_ReturnsAlter()
        {
            var change = new RenameFieldChange("emp_name", new DbField("employee_name", "Name", FieldDbType.String) { Length = 50 });

            Assert.Equal(ChangeExecutionKind.Alter, _builder.GetExecutionKind(change));
        }

        [Fact]
        [DisplayName("IsNarrowingChange returns false for RenameFieldChange")]
        public void IsNarrowingChange_RenameField_ReturnsFalse()
        {
            var change = new RenameFieldChange("emp_name", new DbField("employee_name", "Name", FieldDbType.String) { Length = 50 });

            Assert.False(_builder.IsNarrowingChange(change));
        }

        [Fact]
        [DisplayName("GetStatements for RenameFieldChange produces an sp_rename statement")]
        public void GetStatements_RenameField_EmitsSpRename()
        {
            var change = new RenameFieldChange("emp_name", new DbField("employee_name", "Name", FieldDbType.String) { Length = 50 });
            var statements = _builder.GetStatements("st_demo", change);

            var sql = Assert.Single(statements);
            Assert.Equal("EXEC sp_rename N'st_demo.emp_name', N'employee_name', N'COLUMN';", sql);
        }
    }
}
