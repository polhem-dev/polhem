using System.ComponentModel;
using Polhem.Core.Data;
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

        [Fact]
        [DisplayName("Generate adds the fk_{0}_master index on sys_master_rowid for a detail table")]
        public void Generate_DetailTable_AddsMasterIndex()
        {
            var formTable = BuildFormTable();
            formTable.Fields!.Add(SysFields.MasterRowId, "主檔識別", FieldDbType.Guid);

            var schema = TableSchemaGenerator.Generate(formTable);

            var index = schema.Indexes!.GetOrDefault("fk_{0}_master");
            Assert.NotNull(index);
            Assert.False(index!.Unique);
            Assert.False(index.PrimaryKey);
            Assert.Equal([SysFields.MasterRowId], index.IndexFields!.Select(f => f.FieldName));
        }

        [Fact]
        [DisplayName("Generate adds no master index for a table without sys_master_rowid")]
        public void Generate_MasterTable_AddsNoMasterIndex()
        {
            var formTable = BuildFormTable();

            var schema = TableSchemaGenerator.Generate(formTable);

            Assert.False(schema.Indexes!.Contains("fk_{0}_master"));
            Assert.DoesNotContain(schema.Indexes!, idx => idx.IndexFields!.Contains(SysFields.MasterRowId));
        }

        [Fact]
        [DisplayName("Generate keeps a single fk_{0}_master index when a relation field already produced that name")]
        public void Generate_MasterIndexNameAlreadyUsed_DoesNotAddDuplicate()
        {
            var formTable = BuildFormTable();
            formTable.Fields!.Add(SysFields.MasterRowId, "主檔識別", FieldDbType.Guid);
            formTable.Fields!.Add(new FormField("master", "主檔", FieldDbType.String) { RelationProgId = "Order" });

            var schema = TableSchemaGenerator.Generate(formTable);

            Assert.Single(schema.Indexes!, idx => idx.Name == "fk_{0}_master");
        }

        [Fact]
        [DisplayName("Generate adds no second index on sys_master_rowid when a relation on it already indexes the column")]
        public void Generate_MasterRowIdAlreadyIndexed_DoesNotAddDuplicate()
        {
            var formTable = BuildFormTable();
            formTable.Fields!.Add(new FormField(SysFields.MasterRowId, "主檔識別", FieldDbType.Guid)
            {
                RelationProgId = "Order"
            });

            var schema = TableSchemaGenerator.Generate(formTable);

            Assert.Single(schema.Indexes!, idx => idx.IndexFields!.Contains(SysFields.MasterRowId));
        }

        [Fact]
        [DisplayName("Generate adds sys_company_id after sys_rowid to a table of a company form schema")]
        public void Generate_CompanyFormSchema_AddsCompanyIdAfterRowId()
        {
            var formTable = AddToSchema(BuildFormTable(), DbCategoryIds.Company);

            var schema = TableSchemaGenerator.Generate(formTable);

            var names = schema.Fields!.Select(f => f.FieldName).ToList();
            Assert.Equal(names.IndexOf(SysFields.RowId) + 1, names.IndexOf(SysFields.CompanyId));
            var field = schema.Fields![SysFields.CompanyId];
            Assert.Equal(FieldDbType.String, field.DbType);
            Assert.Equal(Polhem.Definition.Identity.CompanyInfo.CompanyIdMaxLength, field.Length);
            Assert.False(field.AllowNull);
        }

        [Fact]
        [DisplayName("Generate leads the uk_{0} index with sys_company_id on a table of a company form schema")]
        public void Generate_CompanyFormSchema_LeadsUniqueIndexWithCompanyId()
        {
            var formTable = AddToSchema(BuildFormTable(), DbCategoryIds.Company);

            var schema = TableSchemaGenerator.Generate(formTable);

            var uk = schema.Indexes!["uk_{0}"];
            Assert.True(uk.Unique);
            Assert.Equal(s_companyUniqueKey, uk.IndexFields!.Select(f => f.FieldName));
        }

        [Fact]
        [DisplayName("Generate adds sys_company_id to a detail table of a company form schema too")]
        public void Generate_CompanyFormSchemaDetailTable_AddsCompanyId()
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = DbCategoryIds.Company };
            schema.Tables!.Add(BuildFormTable("ft_order"));
            var detail = new FormTable("OrderDetail", "Order Detail");
            detail.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            detail.Fields!.Add(SysFields.MasterRowId, "Master Row Id", FieldDbType.Guid);
            schema.Tables!.Add(detail);

            var generated = TableSchemaGenerator.Generate(detail);

            Assert.True(generated.Fields!.Contains(SysFields.CompanyId));
        }

        [Theory]
        [InlineData(DbCategoryIds.Common)]
        [InlineData(DbCategoryIds.Log)]
        [DisplayName("Generate adds no sys_company_id and keeps uk_{0} on sys_id alone outside the company category")]
        public void Generate_NonCompanyFormSchema_AddsNoCompanyId(string categoryId)
        {
            var formTable = AddToSchema(BuildFormTable(), categoryId);

            var schema = TableSchemaGenerator.Generate(formTable);

            Assert.False(schema.Fields!.Contains(SysFields.CompanyId));
            Assert.Equal(s_idOnlyUniqueKey, schema.Indexes!["uk_{0}"].IndexFields!.Select(f => f.FieldName));
        }

        [Fact]
        [DisplayName("Generate adds no sys_company_id to a form table that no form schema owns")]
        public void Generate_UnownedFormTable_AddsNoCompanyId()
        {
            var schema = TableSchemaGenerator.Generate(BuildFormTable());

            Assert.False(schema.Fields!.Contains(SysFields.CompanyId));
        }

        [Fact]
        [DisplayName("Generate keeps a single sys_company_id when the company form table declares it itself")]
        public void Generate_CompanyIdDeclaredByForm_IsNotDuplicated()
        {
            var formTable = BuildFormTable();
            formTable.Fields!.Add(SysFields.CompanyId, "Company", FieldDbType.String);
            AddToSchema(formTable, DbCategoryIds.Company);

            var schema = TableSchemaGenerator.Generate(formTable);

            Assert.Single(schema.Fields!, f => f.FieldName == SysFields.CompanyId);
        }

        private static readonly string[] s_companyUniqueKey = [SysFields.CompanyId, SysFields.Id];
        private static readonly string[] s_idOnlyUniqueKey = [SysFields.Id];

        private static FormTable AddToSchema(FormTable formTable, string categoryId)
        {
            var schema = new FormSchema("Employee", "Employee") { CategoryId = categoryId };
            schema.Tables!.Add(formTable);
            return formTable;
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
