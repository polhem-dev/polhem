using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;

namespace Polhem.Definition.UnitTests.Forms
{
    /// <summary>
    /// Tests for FormTable paths not covered elsewhere: ToString, GenerateDbTable, and the
    /// KeyNotFoundException/InvalidOperationException branches of RelationFieldReferences.
    /// </summary>
    public class FormTableTests
    {
        private static FormTable BuildBasic(string tableName = "Employee", string displayName = "員工")
        {
            var ft = new FormTable(tableName, displayName);
            ft.Fields!.Add(new FormField("sys_no", "流水號", FieldDbType.AutoIncrement));
            ft.Fields!.Add(new FormField("sys_rowid", "識別", FieldDbType.Guid));
            ft.Fields!.Add(new FormField("name", "名稱", FieldDbType.String));
            return ft;
        }

        [Fact]
        [DisplayName("ToString returns TableName and DisplayName joined")]
        public void ToString_ReturnsTableNameDashDisplayName()
        {
            var ft = new FormTable("Customer", "客戶");
            Assert.Equal("Customer - 客戶", ft.ToString());
        }

        [Fact]
        [DisplayName("GenerateDbTable builds a TableSchema through TableSchemaGenerator")]
        public void GenerateDbTable_DelegatesToSchemaGenerator()
        {
            var ft = BuildBasic();
            ft.DbTableName = "st_employee";

            var schema = ft.GenerateDbTable();

            Assert.NotNull(schema);
            Assert.Equal("st_employee", schema.TableName);
        }

        [Fact]
        [DisplayName("RelationFieldReferences throws KeyNotFoundException when a DestinationField is not in Fields")]
        public void RelationFieldReferences_MissingDestinationField_Throws()
        {
            var ft = BuildBasic();
            // A DbField with a RelationProgId whose mapping's `DestinationField` points to a field that does not exist.
            var rel = new FormField("dept_rowid", "部門", FieldDbType.String)
            {
                RelationProgId = "Department"
            };
            rel.RelationFieldMappings!.Add(new FieldMapping("dept_name", "ghost_field"));
            ft.Fields!.Add(rel);

            Assert.Throws<KeyNotFoundException>(() => _ = ft.RelationFieldReferences);
        }

        [Fact]
        [DisplayName("RelationFieldReferences throws InvalidOperationException for a duplicate DestinationField")]
        public void RelationFieldReferences_DuplicateDestinationField_Throws()
        {
            var ft = BuildBasic();
            // Two different relation fields whose mappings both write the same `DestinationField`, which makes `seen.Add` fail.
            var rel1 = new FormField("dept_rowid", "部門", FieldDbType.String)
            {
                RelationProgId = "Department"
            };
            rel1.RelationFieldMappings!.Add(new FieldMapping("dept_name", "name"));

            var rel2 = new FormField("dept2_rowid", "部門2", FieldDbType.String)
            {
                RelationProgId = "Department"
            };
            rel2.RelationFieldMappings!.Add(new FieldMapping("dept2_name", "name"));

            ft.Fields!.Add(rel1);
            ft.Fields!.Add(rel2);

            Assert.Throws<InvalidOperationException>(() => _ = ft.RelationFieldReferences);
        }

        [Fact]
        [DisplayName("RelationFieldReferences builds the matching references for a valid mapping")]
        public void RelationFieldReferences_ValidMapping_BuildsReference()
        {
            var ft = BuildBasic();
            var rel = new FormField("dept_rowid", "部門", FieldDbType.String)
            {
                RelationProgId = "Department"
            };
            rel.RelationFieldMappings!.Add(new FieldMapping("dept_name", "name"));
            ft.Fields!.Add(rel);

            var refs = ft.RelationFieldReferences;

            Assert.Single(refs);
            Assert.Equal("name", refs[0].FieldName);
        }

        [Fact]
        [DisplayName("RelationFieldReferences skips lookup-only mappings, so they may share a destination with another relation")]
        public void RelationFieldReferences_LookupOnlyMapping_IsSkipped()
        {
            var ft = BuildBasic();
            ft.Fields!.Add(new FormField("ref_dept_name", "Department", FieldDbType.String) { Type = FieldType.RelationField });

            var dept = new FormField("dept_rowid", "Department", FieldDbType.Guid) { RelationProgId = "Department" };
            dept.RelationFieldMappings!.Add(new FieldMapping("sys_name", "ref_dept_name"));
            var emp = new FormField("emp_rowid", "Employee", FieldDbType.Guid) { RelationProgId = "Employee" };
            emp.RelationFieldMappings!.Add(new FieldMapping("dept_rowid", "dept_rowid") { LookupOnly = true });
            emp.RelationFieldMappings!.Add(new FieldMapping("ref_dept_name", "ref_dept_name") { LookupOnly = true });
            ft.Fields!.Add(dept);
            ft.Fields!.Add(emp);

            var reference = Assert.Single(ft.RelationFieldReferences);
            Assert.Equal("ref_dept_name", reference.FieldName);
            Assert.Same(dept, reference.ForeignKeyField);
        }
    }
}
