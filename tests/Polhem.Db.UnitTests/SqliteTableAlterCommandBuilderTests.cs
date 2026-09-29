using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Providers.Sqlite;
using Polhem.Db.Schema;
using Polhem.Db.Schema.Changes;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class SqliteTableAlterCommandBuilderTests : IClassFixture<SharedDbFixture>
    {
        public SqliteTableAlterCommandBuilderTests(SharedDbFixture _) { }

        private readonly SqliteTableAlterCommandBuilder _builder = new();

        /// <summary>
        /// Test-only fake to drive the default branches in <c>GetExecutionKind</c> and
        /// <c>GetStatements</c>; production <see cref="ITableChange"/> implementations are all
        /// matched by the switch arms above the default case.
        /// </summary>
        private sealed class UnknownChange : ITableChange
        {
            public string Describe() => "unknown";
        }

        // ---------- GetExecutionKind ----------

        [Fact]
        [DisplayName("SQLite GetExecutionKind returns Alter for AddFieldChange")]
        public void GetExecutionKind_AddField_ReturnsAlter()
        {
            var change = new AddFieldChange(new DbField("age", "Age", FieldDbType.Integer));
            Assert.Equal(ChangeExecutionKind.Alter, _builder.GetExecutionKind(change));
        }

        [Fact]
        [DisplayName("SQLite GetExecutionKind returns Alter for RenameFieldChange")]
        public void GetExecutionKind_RenameField_ReturnsAlter()
        {
            var change = new RenameFieldChange("oldname", new DbField("newname", "New", FieldDbType.String));
            Assert.Equal(ChangeExecutionKind.Alter, _builder.GetExecutionKind(change));
        }

        [Fact]
        [DisplayName("SQLite GetExecutionKind returns Alter for AddIndexChange")]
        public void GetExecutionKind_AddIndex_ReturnsAlter()
        {
            var index = new DbTableIndex { Name = "ix_demo_name" };
            index.IndexFields!.Add("name");
            Assert.Equal(ChangeExecutionKind.Alter, _builder.GetExecutionKind(new AddIndexChange(index)));
        }

        [Fact]
        [DisplayName("SQLite GetExecutionKind returns Alter for DropIndexChange")]
        public void GetExecutionKind_DropIndex_ReturnsAlter()
        {
            var index = new DbTableIndex { Name = "ix_demo_name" };
            index.IndexFields!.Add("name");
            Assert.Equal(ChangeExecutionKind.Alter, _builder.GetExecutionKind(new DropIndexChange(index)));
        }

        [Fact]
        [DisplayName("SQLite GetExecutionKind returns Rebuild for an AlterFieldChange within the family")]
        public void GetExecutionKind_AlterFieldSameFamily_ReturnsRebuild()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 50 };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 100 };
            Assert.Equal(ChangeExecutionKind.Rebuild,
                _builder.GetExecutionKind(new AlterFieldChange(oldField, newField)));
        }

        [Fact]
        [DisplayName("SQLite GetExecutionKind returns Rebuild for an AlterFieldChange across families")]
        public void GetExecutionKind_AlterFieldCrossFamily_ReturnsRebuild()
        {
            var oldField = new DbField("v", "V", FieldDbType.String) { Length = 50 };
            var newField = new DbField("v", "V", FieldDbType.Integer);
            Assert.Equal(ChangeExecutionKind.Rebuild,
                _builder.GetExecutionKind(new AlterFieldChange(oldField, newField)));
        }

        [Fact]
        [DisplayName("SQLite GetExecutionKind returns NotSupported for an AlterFieldChange involving Unknown")]
        public void GetExecutionKind_AlterFieldUnknown_ReturnsNotSupported()
        {
            var oldField = new DbField("v", "V", FieldDbType.Unknown);
            var newField = new DbField("v", "V", FieldDbType.Integer);
            Assert.Equal(ChangeExecutionKind.NotSupported,
                _builder.GetExecutionKind(new AlterFieldChange(oldField, newField)));
        }

        // ---------- IsNarrowingChange ----------

        [Fact]
        [DisplayName("SQLite IsNarrowingChange returns true for a shorter String")]
        public void IsNarrowingChange_StringShortened_ReturnsTrue()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 100 };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 50 };
            Assert.True(_builder.IsNarrowingChange(new AlterFieldChange(oldField, newField)));
        }

        [Fact]
        [DisplayName("SQLite IsNarrowingChange returns false for a change other than AlterField")]
        public void IsNarrowingChange_NonAlterChange_ReturnsFalse()
        {
            var change = new AddFieldChange(new DbField("age", "Age", FieldDbType.Integer));
            Assert.False(_builder.IsNarrowingChange(change));
        }

        // ---------- Statements ----------

        [Fact]
        [DisplayName("SQLite GetStatements for AddField produces ALTER TABLE ADD COLUMN")]
        public void GetStatements_AddField_EmitsAlterTableAddColumn()
        {
            var field = new DbField("age", "Age", FieldDbType.Integer) { AllowNull = false };
            var statements = _builder.GetStatements("st_demo", new AddFieldChange(field));

            var sql = Assert.Single(statements);
            Assert.Contains("ALTER TABLE \"st_demo\" ADD COLUMN", sql);
            Assert.Contains("\"age\" INTEGER NOT NULL", sql);
            Assert.Contains("DEFAULT", sql);
        }

        [Fact]
        [DisplayName("SQLite GetStatements for AddField of a text field carries COLLATE NOCASE (consistent with CREATE TABLE)")]
        public void GetStatements_AddStringField_IncludesCollateNocase()
        {
            // CREATE and ALTER share `SqliteSchemaSyntax.GetColumnDefinition`, so a text field added by
            // ALTER TABLE ADD COLUMN gets COLLATE NOCASE automatically, consistent with CREATE.
            var field = new DbField("name", "Name", FieldDbType.String) { Length = 50, AllowNull = false };
            var statements = _builder.GetStatements("st_demo", new AddFieldChange(field));

            var sql = Assert.Single(statements);
            Assert.Contains("ALTER TABLE \"st_demo\" ADD COLUMN", sql);
            Assert.Contains("\"name\" VARCHAR(50) COLLATE NOCASE NOT NULL", sql);
        }

        [Fact]
        [DisplayName("SQLite GetStatements for RenameField produces RENAME COLUMN")]
        public void GetStatements_RenameField_EmitsRenameColumn()
        {
            var change = new RenameFieldChange("oldname", new DbField("newname", "New", FieldDbType.String) { Length = 50 });
            var statements = _builder.GetStatements("st_demo", change);

            var sql = Assert.Single(statements);
            Assert.Equal("ALTER TABLE \"st_demo\" RENAME COLUMN \"oldname\" TO \"newname\";", sql);
        }

        [Fact]
        [DisplayName("SQLite GetStatements for AddIndex produces CREATE INDEX")]
        public void GetStatements_AddIndex_EmitsCreateIndex()
        {
            var index = new DbTableIndex { Name = "ix_{0}_col" };
            index.IndexFields!.Add("col");

            var statements = _builder.GetStatements("st_demo", new AddIndexChange(index));

            var sql = Assert.Single(statements);
            Assert.Equal("CREATE INDEX \"ix_st_demo_col\" ON \"st_demo\" (\"col\" ASC);", sql);
        }

        [Fact]
        [DisplayName("SQLite GetStatements for AddIndex of a unique index produces CREATE UNIQUE INDEX")]
        public void GetStatements_AddIndexUnique_EmitsCreateUniqueIndex()
        {
            var index = new DbTableIndex { Name = "uk_{0}_col", Unique = true };
            index.IndexFields!.Add("col");

            var statements = _builder.GetStatements("st_demo", new AddIndexChange(index));

            Assert.Contains("CREATE UNIQUE INDEX", statements[0]);
        }

        [Fact]
        [DisplayName("SQLite GetStatements throws NotSupportedException for AddIndex of a PrimaryKey")]
        public void GetStatements_AddPrimaryKeyIndex_Throws()
        {
            var index = new DbTableIndex { Name = "pk_st_demo", PrimaryKey = true };
            index.IndexFields!.Add("sys_rowid");

            Assert.Throws<NotSupportedException>(() =>
                _builder.GetStatements("st_demo", new AddIndexChange(index)));
        }

        [Fact]
        [DisplayName("SQLite GetStatements for DropIndex produces DROP INDEX")]
        public void GetStatements_DropIndex_EmitsDropIndex()
        {
            var index = new DbTableIndex { Name = "ix_st_demo_col" };
            index.IndexFields!.Add("col");

            var statements = _builder.GetStatements("st_demo", new DropIndexChange(index));

            var sql = Assert.Single(statements);
            Assert.Equal("DROP INDEX \"ix_st_demo_col\";", sql);
        }

        [Fact]
        [DisplayName("SQLite GetStatements throws NotSupportedException for DropIndex of a PrimaryKey")]
        public void GetStatements_DropPrimaryKeyIndex_Throws()
        {
            var index = new DbTableIndex { Name = "pk_st_demo", PrimaryKey = true };
            index.IndexFields!.Add("sys_rowid");

            Assert.Throws<NotSupportedException>(() =>
                _builder.GetStatements("st_demo", new DropIndexChange(index)));
        }

        [Fact]
        [DisplayName("SQLite GetStatements throws InvalidOperationException for AlterField (it must take the rebuild path)")]
        public void GetStatements_AlterField_Throws()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 50 };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 100 };

            Assert.Throws<InvalidOperationException>(() =>
                _builder.GetStatements("st_demo", new AlterFieldChange(oldField, newField)));
        }

        [Fact]
        [DisplayName("SQLite GetExecutionKind returns NotSupported for an unknown ITableChange subclass")]
        public void GetExecutionKind_UnknownChange_ReturnsNotSupported()
        {
            Assert.Equal(ChangeExecutionKind.NotSupported, _builder.GetExecutionKind(new UnknownChange()));
        }

        [Fact]
        [DisplayName("SQLite GetStatements throws InvalidOperationException for an unknown ITableChange subclass")]
        public void GetStatements_UnknownChange_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                _builder.GetStatements("st_demo", new UnknownChange()));
            Assert.Contains("Unsupported change type", ex.Message);
        }
    }
}
