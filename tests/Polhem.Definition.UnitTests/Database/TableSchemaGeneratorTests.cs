using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;

namespace Polhem.Definition.UnitTests.Database
{
    /// <summary>
    /// Tests for TableSchemaGenerator converting a FormTable into a TableSchema.
    /// </summary>
    public class TableSchemaGeneratorTests
    {
        [Fact]
        [DisplayName("Generate throws ArgumentNullException for null")]
        public void Generate_NullFormTable_ThrowsArgumentNullException()
        {
            // Arrange

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => TableSchemaGenerator.Generate(null!));
        }

        [Fact]
        [DisplayName("GetCategoryId throws ArgumentNullException for null")]
        public void GetCategoryId_NullFormSchema_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => TableSchemaGenerator.GetCategoryId(null!));
        }

        [Fact]
        [DisplayName("GetCategoryId throws InvalidOperationException mentioning the ProgId when CategoryId is empty")]
        public void GetCategoryId_EmptyCategoryId_ThrowsInvalidOperationException()
        {
            var schema = new FormSchema("Demo", "示範");

            var ex = Assert.Throws<InvalidOperationException>(() => TableSchemaGenerator.GetCategoryId(schema));
            Assert.Contains("Demo", ex.Message);
            Assert.Contains("CategoryId", ex.Message);
        }

        [Fact]
        [DisplayName("GetCategoryId returns the CategoryId string when it is set")]
        public void GetCategoryId_CategoryIdSet_ReturnsValue()
        {
            var schema = new FormSchema("Demo", "示範") { CategoryId = "common" };

            Assert.Equal("common", TableSchemaGenerator.GetCategoryId(schema));
        }

        [Fact]
        [DisplayName("Generate uses DbTableName as the table name when it is set")]
        public void Generate_DbTableNameSpecified_UsesDbTableName()
        {
            // Arrange
            var formTable = BuildFormTable(dbTableName: "st_employee");

            // Act
            var schema = TableSchemaGenerator.Generate(formTable);

            // Assert
            Assert.Equal("st_employee", schema.TableName);
        }

        [Fact]
        [DisplayName("Generate uses TableName when there is no DbTableName")]
        public void Generate_NoDbTableName_UsesTableName()
        {
            // Arrange
            var formTable = BuildFormTable(dbTableName: string.Empty);

            // Act
            var schema = TableSchemaGenerator.Generate(formTable);

            // Assert
            Assert.Equal("Employee", schema.TableName);
        }

        [Fact]
        [DisplayName("Generate adds only DbField-type fields and ignores other field types")]
        public void Generate_OnlyAddsDbFields()
        {
            // Arrange
            var formTable = BuildFormTable();
            formTable.Fields!.Add(new FormField("virtual_field", "虛擬欄位", FieldDbType.String, FieldType.RelationField));

            // Act
            var schema = TableSchemaGenerator.Generate(formTable);

            // Assert
            Assert.DoesNotContain(schema.Fields!, f => f.FieldName == "virtual_field");
        }

        [Fact]
        [DisplayName("Generate adds a primary key index on sys_no automatically")]
        public void Generate_AddsPrimaryKeyIndexOnSysNo()
        {
            // Arrange
            var formTable = BuildFormTable();

            // Act
            var schema = TableSchemaGenerator.Generate(formTable);

            // Assert
            var pk = schema.GetPrimaryKey();
            Assert.NotNull(pk);
            Assert.True(pk!.PrimaryKey);
            Assert.True(pk.Unique);
        }

        [Fact]
        [DisplayName("Generate adds a unique index on sys_rowid automatically")]
        public void Generate_AddsUniqueIndexOnRowId()
        {
            // Arrange
            var formTable = BuildFormTable();

            // Act
            var schema = TableSchemaGenerator.Generate(formTable);

            // Assert
            Assert.Contains(schema.Indexes!, idx =>
                idx.IndexFields!.Contains(SysFields.RowId) && idx.Unique && !idx.PrimaryKey);
        }

        [Fact]
        [DisplayName("Generate maps the field MaxLength to DbField.Length")]
        public void Generate_MapsMaxLengthToDbFieldLength()
        {
            // Arrange
            var formTable = new FormTable("Demo", "示範");
            formTable.Fields!.Add(new FormField("name", "名稱", FieldDbType.String) { MaxLength = 50 });

            // Act
            var schema = TableSchemaGenerator.Generate(formTable);

            // Assert
            Assert.Equal(50, schema.Fields!["name"].Length);
        }

        [Fact]
        [DisplayName("Generate adds a foreign key index for a field with a RelationProgId")]
        public void Generate_FieldWithRelationProgId_AddsForeignKeyIndex()
        {
            // Arrange
            var formTable = BuildFormTable();
            formTable.Fields!.Add(new FormField("dept_rowid", "部門", FieldDbType.String)
            {
                RelationProgId = "Department"
            });

            // Act
            var schema = TableSchemaGenerator.Generate(formTable);

            // Assert
            Assert.Contains(schema.Indexes!, idx => idx.IndexFields!.Contains("dept_rowid") && !idx.Unique);
        }

        private static FormTable BuildFormTable(string dbTableName = "st_employee")
        {
            var formTable = new FormTable("Employee", "員工") { DbTableName = dbTableName };
            formTable.Fields!.Add(SysFields.No, "流水號", FieldDbType.AutoIncrement);
            formTable.Fields!.Add(SysFields.RowId, "識別", FieldDbType.Guid);
            formTable.Fields!.Add(SysFields.Id, "編號", FieldDbType.String);
            formTable.Fields!.Add(SysFields.Name, "名稱", FieldDbType.String);
            return formTable;
        }
    }
}
