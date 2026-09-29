using System.ComponentModel;
using Polhem.Definition.Forms;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Database;

namespace Polhem.Definition.UnitTests
{
    public class FormSchemaTests
    {
        [Fact]
        [DisplayName("A new FormSchema has an empty CategoryId (required, no default)")]
        public void CategoryId_NewInstance_DefaultsToEmpty()
        {
            var schema = new FormSchema();

            Assert.Equal(string.Empty, schema.CategoryId);
        }

        [Fact]
        [DisplayName("FormSchema CategoryId round-trips through XML as an XmlAttribute")]
        public void CategoryId_RoundTripsThroughXml()
        {
            var schema = new FormSchema("Demo", "示範") { CategoryId = "sales" };

            var xml = XmlCodec.Serialize(schema);
            var restored = XmlCodec.Deserialize<FormSchema>(xml);

            Assert.NotNull(restored);
            Assert.Equal("sales", restored!.CategoryId);
            Assert.Contains("CategoryId=\"sales\"", xml);
        }

        [Fact]
        [DisplayName("FormSchema CurrencyField round-trips through XML as an XmlAttribute")]
        public void CurrencyField_RoundTripsThroughXml()
        {
            var schema = new FormSchema("Order", "訂單") { CategoryId = "company", CurrencyField = "sys_currency" };

            var xml = XmlCodec.Serialize(schema);
            var restored = XmlCodec.Deserialize<FormSchema>(xml);

            Assert.NotNull(restored);
            Assert.Equal("sys_currency", restored!.CurrencyField);
            Assert.Contains("CurrencyField=\"sys_currency\"", xml);
        }

        [Fact]
        [DisplayName("FormSchema omits the CurrencyField attribute when it has its empty default")]
        public void CurrencyField_Empty_OmitsXmlAttribute()
        {
            var schema = new FormSchema("Demo", "示範") { CategoryId = "sales" };

            var xml = XmlCodec.Serialize(schema);

            Assert.DoesNotContain("CurrencyField=", xml);
        }


        [Fact]
        [DisplayName("A department FormSchema has a valid master table")]
        public void CreateFormSchema_DepartmentWithRelations_HasMasterTable()
        {
            var formSchema = new FormSchema("Department", "部門");
            var table = formSchema.Tables!.Add("Department", "部門");
            table.DbTableName = "st_department";
            table.Fields!.Add("sys_no", "流水號", FieldDbType.AutoIncrement);
            table.Fields!.Add("sys_rowid", "唯一識別", FieldDbType.Guid);
            table.Fields!.Add("sys_id", "部門編號", FieldDbType.String);
            table.Fields!.Add("sys_name", "部門名稱", FieldDbType.String);
            var managerField = new FormField("manager_rowid", "部門主管唯一識別", FieldDbType.String)
            {
                RelationProgId = "Employee"
            };
            managerField.RelationFieldMappings!.Add("sys_id", "ref_manager_id");
            managerField.RelationFieldMappings!.Add("sys_name", "ref_manager_name");
            table.Fields!.Add(managerField);
            table.Fields!.Add(new FormField("ref_manager_id", "部門主管編號", FieldDbType.String, FieldType.RelationField));
            table.Fields!.Add(new FormField("ref_manager_name", "部門主管名稱", FieldDbType.String, FieldType.RelationField));

            Assert.Same(table, formSchema.MasterTable);
            Assert.Equal("Employee", table.RelationFieldReferences["ref_manager_name"].SourceProgId);
        }

        [Fact]
        [DisplayName("An employee FormSchema contains relation field references")]
        public void CreateFormSchema_EmployeeWithRelations_HasRelationFieldReferences()
        {
            var formSchema = new FormSchema("Employee", "員工");
            var table = formSchema.Tables!.Add("Employee", "員工");
            table.DbTableName = "st_employee";
            table.Fields!.Add("sys_no", "流水號", FieldDbType.AutoIncrement);
            table.Fields!.Add("sys_rowid", "唯一識別", FieldDbType.Guid);
            table.Fields!.Add("sys_id", "員工編號", FieldDbType.String);
            table.Fields!.Add("sys_name", "員工姓名", FieldDbType.String);
            var deptField = new FormField("dept_rowid", "部門唯一識別", FieldDbType.String)
            {
                RelationProgId = "Department"
            };
            deptField.RelationFieldMappings!.Add("sys_id", "ref_dept_id");
            deptField.RelationFieldMappings!.Add("sys_name", "ref_dept_name");
            deptField.RelationFieldMappings!.Add("ref_manager_id", "ref_supervisor_id");
            deptField.RelationFieldMappings!.Add("ref_manager_name", "ref_supervisor_name");
            table.Fields!.Add(deptField);
            table.Fields!.Add(new FormField("ref_dept_id", "部門編號", FieldDbType.String, FieldType.RelationField));
            table.Fields!.Add(new FormField("ref_dept_name", "部門名稱", FieldDbType.String, FieldType.RelationField));
            table.Fields!.Add(new FormField("ref_supervisor_id", "直屬主管編號", FieldDbType.String, FieldType.RelationField));
            table.Fields!.Add(new FormField("ref_supervisor_name", "直屬主管名稱", FieldDbType.String, FieldType.RelationField));

            var references = table.RelationFieldReferences;

            // Every mapped destination field resolves back to its foreign key and the source field it copies.
            Assert.Equal(4, references.Count);
            var supervisor = references["ref_supervisor_name"];
            Assert.Equal("dept_rowid", supervisor.ForeignKeyField.FieldName);
            Assert.Equal("Department", supervisor.SourceProgId);
            Assert.Equal("ref_manager_name", supervisor.SourceField);
        }

        [Fact]
        [DisplayName("GetListLayout adds Columns in ListFields order and appends a hidden sys_rowid")]
        public void GetListLayout_ValidSchema_ContainsListFieldsAndHiddenRowId()
        {
            var schema = new FormSchema("Demo", "示範") { ListFields = "sys_id,sys_name" };
            var table = schema.Tables!.Add("Demo", "示範");
            table.Fields!.Add(new FormField(SysFields.RowId, "唯一識別", FieldDbType.Guid) { Visible = false });
            table.Fields!.Add(new FormField("sys_id", "編號", FieldDbType.String) { Width = 150 });
            table.Fields!.Add(new FormField("sys_name", "名稱", FieldDbType.String));

            var grid = schema.GetListLayout();

            Assert.Equal("Demo", grid.TableName);
            Assert.Equal(3, grid.Columns!.Count);

            // The ListFields order comes first, and sys_rowid is appended last.
            Assert.Equal("sys_id", grid.Columns![0].FieldName);
            Assert.Equal(150, grid.Columns![0].Width);
            Assert.Equal("sys_name", grid.Columns![1].FieldName);
            Assert.Equal(0, grid.Columns![1].Width);

            var rowIdColumn = grid.Columns![2];
            Assert.Equal(SysFields.RowId, rowIdColumn.FieldName);
            Assert.False(rowIdColumn.Visible);
        }
    }
}
