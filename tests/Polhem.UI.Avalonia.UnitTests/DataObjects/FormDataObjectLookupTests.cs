using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.DataObjects
{
    /// <summary>
    /// FormDataObject lookup write-back tests: ApplyLookupSelection writes the rowid and the mapped fields,
    /// lookup-only mappings are written too, a missing source field is a hard error, and ClearLookupSelection clears them.
    /// </summary>
    public class FormDataObjectLookupTests
    {
        private static FormSchema BuildOrderSchema()
        {
            var schema = new FormSchema("Order", "訂單") { CategoryId = "company" };
            var table = schema.Tables!.Add("Order", "訂單");
            table.Fields!.Add(new FormField(SysFields.RowId, "唯一識別", FieldDbType.Guid));
            table.Fields!.Add(new FormField(SysFields.Id, "單號", FieldDbType.String));

            var customerField = new FormField("customer_rowid", "客戶", FieldDbType.Guid)
            {
                RelationProgId = "Customer",
            };
            customerField.RelationFieldMappings!.Add(SysFields.Id, "ref_customer_id");
            customerField.RelationFieldMappings!.Add(SysFields.Name, "ref_customer_name");
            table.Fields!.Add(customerField);
            table.Fields!.Add(new FormField("ref_customer_id", "客戶代碼", FieldDbType.String, FieldType.RelationField));
            table.Fields!.Add(new FormField("ref_customer_name", "客戶名稱", FieldDbType.String, FieldType.RelationField));
            return schema;
        }

        private static DataRow BuildSelectedRow(Guid rowId, string id, string name)
        {
            var table = new DataTable("Customer");
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Columns.Add(SysFields.Id, typeof(string));
            table.Columns.Add(SysFields.Name, typeof(string));
            table.Rows.Add(rowId, id, name);
            return table.Rows[0];
        }

        [Fact]
        [DisplayName("ApplyLookupSelection writes the rowid and writes back the ref fields by RelationFieldMappings")]
        public void ApplyLookupSelection_WritesRowIdAndMappedFields()
        {
            var dataObject = new FormDataObject(BuildOrderSchema());
            dataObject.InitializeNewMaster();
            var field = dataObject.GetFormField("customer_rowid")!;
            var customerId = Guid.NewGuid();

            dataObject.ApplyLookupSelection(field, BuildSelectedRow(customerId, "C001", "客戶甲"));

            Assert.Equal(customerId.ToString(), dataObject.GetField("customer_rowid"));
            Assert.Equal("C001", dataObject.GetField("ref_customer_id"));
            Assert.Equal("客戶甲", dataObject.GetField("ref_customer_name"));
            Assert.True(dataObject.IsDirty);
        }

        [Fact]
        [DisplayName("ApplyLookupSelection also writes lookup-only mappings, such as the customer's salesperson into the order's own field")]
        public void ApplyLookupSelection_LookupOnlyMappings_AreWritten()
        {
            var schema = BuildOrderSchema();
            var table = schema.MasterTable!;
            table.Fields!.Add(new FormField("sales_rowid", "業務", FieldDbType.Guid) { RelationProgId = "Employee" });
            table.Fields!.Add(new FormField("ref_sales_name", "業務名稱", FieldDbType.String, FieldType.RelationField));
            var customerField = table.Fields!["customer_rowid"];
            customerField.RelationFieldMappings!.Add(new FieldMapping("sales_rowid", "sales_rowid") { LookupOnly = true });
            customerField.RelationFieldMappings!.Add(new FieldMapping("ref_sales_name", "ref_sales_name") { LookupOnly = true });

            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var field = dataObject.GetFormField("customer_rowid")!;
            var selected = BuildSelectedRow(Guid.NewGuid(), "C002", "客戶乙");
            var salesId = Guid.NewGuid();
            selected.Table.Columns.Add("sales_rowid", typeof(Guid));
            selected.Table.Columns.Add("ref_sales_name", typeof(string));
            selected["sales_rowid"] = salesId;
            selected["ref_sales_name"] = "王小明";

            dataObject.ApplyLookupSelection(field, selected);

            Assert.Equal("客戶乙", dataObject.GetField("ref_customer_name"));
            Assert.Equal(salesId.ToString(), dataObject.GetField("sales_rowid"));
            Assert.Equal("王小明", dataObject.GetField("ref_sales_name"));

            dataObject.ClearLookupSelection(field);

            Assert.Equal(Guid.Empty.ToString(), dataObject.GetField("sales_rowid"));
            Assert.Equal(string.Empty, dataObject.GetField("ref_sales_name"));
        }

        [Fact]
        [DisplayName("ApplyLookupSelection throws InvalidOperationException when a source field is missing from the selected row")]
        public void ApplyLookupSelection_MissingSourceField_Throws()
        {
            var dataObject = new FormDataObject(BuildOrderSchema());
            dataObject.InitializeNewMaster();
            var field = dataObject.GetFormField("customer_rowid")!;

            // The selected row lacks `sys_name`, the mapping source field.
            var table = new DataTable("Customer");
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Columns.Add(SysFields.Id, typeof(string));
            table.Rows.Add(Guid.NewGuid(), "C003");

            var ex = Assert.Throws<InvalidOperationException>(
                () => dataObject.ApplyLookupSelection(field, table.Rows[0]));
            Assert.Contains("sys_name", ex.Message);
        }

        [Fact]
        [DisplayName("ApplyLookupSelection throws InvalidOperationException when the selected row has no sys_rowid")]
        public void ApplyLookupSelection_MissingRowId_Throws()
        {
            var dataObject = new FormDataObject(BuildOrderSchema());
            dataObject.InitializeNewMaster();
            var field = dataObject.GetFormField("customer_rowid")!;

            var table = new DataTable("Customer");
            table.Columns.Add(SysFields.Id, typeof(string));
            table.Rows.Add("C004");

            Assert.Throws<InvalidOperationException>(
                () => dataObject.ApplyLookupSelection(field, table.Rows[0]));
        }

        [Fact]
        [DisplayName("ClearLookupSelection clears the rowid and every mapped target field")]
        public void ClearLookupSelection_ResetsRowIdAndMappedFields()
        {
            var dataObject = new FormDataObject(BuildOrderSchema());
            dataObject.InitializeNewMaster();
            var field = dataObject.GetFormField("customer_rowid")!;
            dataObject.ApplyLookupSelection(field, BuildSelectedRow(Guid.NewGuid(), "C001", "客戶甲"));

            dataObject.ClearLookupSelection(field);

            Assert.Equal(Guid.Empty.ToString(), dataObject.GetField("customer_rowid"));
            Assert.Equal(string.Empty, dataObject.GetField("ref_customer_id"));
            Assert.Equal(string.Empty, dataObject.GetField("ref_customer_name"));
        }
    }
}
