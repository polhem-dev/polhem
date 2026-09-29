using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Providers.PostgreSql;
using Polhem.Db.Schema;
using Polhem.Db.Schema.Changes;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class PgTableAlterCommandBuilderTests : IClassFixture<SharedDbFixture>
    {
        public PgTableAlterCommandBuilderTests(SharedDbFixture _) { }

        private readonly PgTableAlterCommandBuilder _builder = new();

        // ---------- GetExecutionKind ----------

        [Fact]
        [DisplayName("PG GetExecutionKind returns Alter for AddFieldChange")]
        public void GetExecutionKind_AddField_ReturnsAlter()
        {
            var change = new AddFieldChange(new DbField("age", "Age", FieldDbType.Integer));

            Assert.Equal(ChangeExecutionKind.Alter, _builder.GetExecutionKind(change));
        }

        [Fact]
        [DisplayName("PG GetExecutionKind returns Alter for an AlterFieldChange within the family")]
        public void GetExecutionKind_AlterFieldSameFamily_ReturnsAlter()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 50 };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 100 };
            var change = new AlterFieldChange(oldField, newField);

            Assert.Equal(ChangeExecutionKind.Alter, _builder.GetExecutionKind(change));
        }

        [Fact]
        [DisplayName("PG GetExecutionKind returns Rebuild for an AlterFieldChange across families")]
        public void GetExecutionKind_AlterFieldCrossFamily_ReturnsRebuild()
        {
            var oldField = new DbField("v", "V", FieldDbType.String) { Length = 50 };
            var newField = new DbField("v", "V", FieldDbType.Integer);
            var change = new AlterFieldChange(oldField, newField);

            Assert.Equal(ChangeExecutionKind.Rebuild, _builder.GetExecutionKind(change));
        }

        [Fact]
        [DisplayName("PG GetExecutionKind returns Rebuild for an AlterFieldChange that toggles AutoIncrement")]
        public void GetExecutionKind_AlterFieldAutoIncrementToggle_ReturnsRebuild()
        {
            var oldField = new DbField("id", "Id", FieldDbType.Integer);
            var newField = new DbField("id", "Id", FieldDbType.AutoIncrement);
            var change = new AlterFieldChange(oldField, newField);

            Assert.Equal(ChangeExecutionKind.Rebuild, _builder.GetExecutionKind(change));
        }

        [Fact]
        [DisplayName("PG GetExecutionKind returns Alter for AddIndexChange")]
        public void GetExecutionKind_AddIndex_ReturnsAlter()
        {
            var index = new DbTableIndex { Name = "ix_demo_name" };
            index.IndexFields!.Add("name");

            Assert.Equal(ChangeExecutionKind.Alter, _builder.GetExecutionKind(new AddIndexChange(index)));
        }

        [Fact]
        [DisplayName("PG GetExecutionKind returns Alter for DropIndexChange")]
        public void GetExecutionKind_DropIndex_ReturnsAlter()
        {
            var index = new DbTableIndex { Name = "ix_demo_name" };
            index.IndexFields!.Add("name");

            Assert.Equal(ChangeExecutionKind.Alter, _builder.GetExecutionKind(new DropIndexChange(index)));
        }

        // ---------- IsNarrowingChange ----------

        [Fact]
        [DisplayName("PG IsNarrowingChange returns false for a change other than AlterField")]
        public void IsNarrowingChange_NonAlterChange_ReturnsFalse()
        {
            var change = new AddFieldChange(new DbField("age", "Age", FieldDbType.Integer));

            Assert.False(_builder.IsNarrowingChange(change));
        }

        [Fact]
        [DisplayName("PG IsNarrowingChange returns true for a shorter String")]
        public void IsNarrowingChange_StringShortened_ReturnsTrue()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 100 };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 50 };

            Assert.True(_builder.IsNarrowingChange(new AlterFieldChange(oldField, newField)));
        }

        // ---------- AddField statements ----------

        [Fact]
        [DisplayName("PG GetStatements for AddField produces ALTER TABLE ADD COLUMN with DEFAULT")]
        public void GetStatements_AddField_EmitsAlterTableAddColumn()
        {
            var field = new DbField("age", "Age", FieldDbType.Integer) { AllowNull = false };
            var statements = _builder.GetStatements("st_demo", new AddFieldChange(field));

            var sql = Assert.Single(statements);
            Assert.Contains("ALTER TABLE \"st_demo\" ADD COLUMN", sql);
            Assert.Contains("\"age\" integer NOT NULL", sql);
            Assert.Contains("DEFAULT", sql);
        }

        [Fact]
        [DisplayName("PG GetStatements for AddField of a nullable field produces no DEFAULT")]
        public void GetStatements_AddFieldNullable_NoDefault()
        {
            var field = new DbField("note", "Note", FieldDbType.String) { Length = 100, AllowNull = true };
            var statements = _builder.GetStatements("st_demo", new AddFieldChange(field));

            var sql = Assert.Single(statements);
            Assert.Contains("\"note\" varchar(100) NULL", sql);
            Assert.DoesNotContain("DEFAULT", sql);
        }

        // ---------- AlterField statements ----------

        [Fact]
        [DisplayName("PG GetStatements for an AlterField that only changes the length produces a single ALTER COLUMN TYPE")]
        public void GetStatements_AlterFieldLengthChanged_EmitsAlterColumnType()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 50, AllowNull = false };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 100, AllowNull = false };
            var statements = _builder.GetStatements("st_demo", new AlterFieldChange(oldField, newField));

            var sql = Assert.Single(statements);
            Assert.Equal("ALTER TABLE \"st_demo\" ALTER COLUMN \"name\" TYPE varchar(100);", sql);
        }

        [Fact]
        [DisplayName("PG GetStatements for an AlterField from NOT NULL to NULL produces DROP NOT NULL + DROP DEFAULT")]
        public void GetStatements_AlterFieldToNullable_DropsNotNullAndDefault()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 50, AllowNull = false };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 50, AllowNull = true };
            var statements = _builder.GetStatements("st_demo", new AlterFieldChange(oldField, newField));

            Assert.Equal(2, statements.Count);
            Assert.Equal("ALTER TABLE \"st_demo\" ALTER COLUMN \"name\" DROP NOT NULL;", statements[0]);
            Assert.Equal("ALTER TABLE \"st_demo\" ALTER COLUMN \"name\" DROP DEFAULT;", statements[1]);
        }

        [Fact]
        [DisplayName("PG GetStatements for an AlterField that only changes the default produces a single SET DEFAULT")]
        public void GetStatements_AlterFieldDefaultOnly_EmitsSetDefault()
        {
            var oldField = new DbField("code", "Code", FieldDbType.String) { Length = 10, AllowNull = false, DefaultValue = "A" };
            var newField = new DbField("code", "Code", FieldDbType.String) { Length = 10, AllowNull = false, DefaultValue = "B" };
            var statements = _builder.GetStatements("st_demo", new AlterFieldChange(oldField, newField));

            var sql = Assert.Single(statements);
            Assert.Equal("ALTER TABLE \"st_demo\" ALTER COLUMN \"code\" SET DEFAULT 'B';", sql);
        }

        // ---------- AddIndex statements ----------

        [Fact]
        [DisplayName("PG GetStatements for AddIndex of a non-unique index produces CREATE INDEX")]
        public void GetStatements_AddRegularIndex_EmitsCreateIndex()
        {
            var index = new DbTableIndex { Name = "ix_{0}_name" };
            index.IndexFields!.Add("name");
            var statements = _builder.GetStatements("st_demo", new AddIndexChange(index));

            var sql = Assert.Single(statements);
            Assert.Equal("CREATE INDEX \"ix_st_demo_name\" ON \"st_demo\" (\"name\" ASC);", sql);
        }

        [Fact]
        [DisplayName("PG GetStatements for AddIndex of a unique index includes UNIQUE")]
        public void GetStatements_AddUniqueIndex_EmitsUniqueClause()
        {
            var index = new DbTableIndex { Name = "ix_{0}_name", Unique = true };
            index.IndexFields!.Add("name");
            var statements = _builder.GetStatements("st_demo", new AddIndexChange(index));

            var sql = Assert.Single(statements);
            Assert.Contains("CREATE UNIQUE INDEX", sql);
        }

        [Fact]
        [DisplayName("PG GetStatements for AddIndex of a primary key produces ALTER TABLE ADD CONSTRAINT PRIMARY KEY")]
        public void GetStatements_AddPrimaryKey_EmitsAddConstraintPrimaryKey()
        {
            var index = new DbTableIndex { Name = "pk_{0}", PrimaryKey = true, Unique = true };
            index.IndexFields!.Add("id");
            var statements = _builder.GetStatements("st_demo", new AddIndexChange(index));

            var sql = Assert.Single(statements);
            // PG rejects ASC/DESC inside a PRIMARY KEY constraint — emit bare column name.
            Assert.Equal("ALTER TABLE \"st_demo\" ADD CONSTRAINT \"pk_st_demo\" PRIMARY KEY (\"id\");", sql);
        }

        // ---------- DropIndex statements ----------

        [Fact]
        [DisplayName("PG GetStatements for DropIndex of a non-primary key produces DROP INDEX")]
        public void GetStatements_DropRegularIndex_EmitsDropIndex()
        {
            var index = new DbTableIndex { Name = "ix_st_demo_name" };
            index.IndexFields!.Add("name");
            var statements = _builder.GetStatements("st_demo", new DropIndexChange(index));

            var sql = Assert.Single(statements);
            Assert.Equal("DROP INDEX \"ix_st_demo_name\";", sql);
        }

        [Fact]
        [DisplayName("PG GetStatements for DropIndex of a primary key produces ALTER TABLE DROP CONSTRAINT")]
        public void GetStatements_DropPrimaryKey_EmitsDropConstraint()
        {
            var index = new DbTableIndex { Name = "pk_st_demo", PrimaryKey = true };
            index.IndexFields!.Add("id");
            var statements = _builder.GetStatements("st_demo", new DropIndexChange(index));

            var sql = Assert.Single(statements);
            Assert.Equal("ALTER TABLE \"st_demo\" DROP CONSTRAINT \"pk_st_demo\";", sql);
        }

        // ---------- Edge cases ----------

        [Fact]
        [DisplayName("PG GetStatements throws for a null tableName")]
        public void GetStatements_NullTableName_Throws()
        {
            var change = new AddFieldChange(new DbField("a", "A", FieldDbType.Integer));

            Assert.ThrowsAny<ArgumentException>(() => _builder.GetStatements(null!, change));
        }

        // ---------- RenameFieldChange ----------

        [Fact]
        [DisplayName("PG GetExecutionKind returns Alter for RenameFieldChange")]
        public void GetExecutionKind_RenameField_ReturnsAlter()
        {
            var change = new RenameFieldChange("emp_name", new DbField("employee_name", "Name", FieldDbType.String) { Length = 50 });

            Assert.Equal(ChangeExecutionKind.Alter, _builder.GetExecutionKind(change));
        }

        [Fact]
        [DisplayName("PG IsNarrowingChange returns false for RenameFieldChange")]
        public void IsNarrowingChange_RenameField_ReturnsFalse()
        {
            var change = new RenameFieldChange("emp_name", new DbField("employee_name", "Name", FieldDbType.String) { Length = 50 });

            Assert.False(_builder.IsNarrowingChange(change));
        }

        [Fact]
        [DisplayName("PG GetStatements for RenameFieldChange produces an ALTER TABLE RENAME COLUMN statement")]
        public void GetStatements_RenameField_EmitsRenameColumn()
        {
            var change = new RenameFieldChange("emp_name", new DbField("employee_name", "Name", FieldDbType.String) { Length = 50 });
            var statements = _builder.GetStatements("st_demo", change);

            var sql = Assert.Single(statements);
            Assert.Equal("ALTER TABLE \"st_demo\" RENAME COLUMN \"emp_name\" TO \"employee_name\";", sql);
        }
    }
}
