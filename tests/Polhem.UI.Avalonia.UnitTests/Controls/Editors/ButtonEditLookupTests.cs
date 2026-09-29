using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// ButtonEdit lookup display binding tests: the display fields supply the text (not the Guid), the display
    /// stays in sync after a lookup write-back (WatchFieldName), the display text is never written back to the rowid field,
    /// and the text box is always read-only in lookup mode.
    /// </summary>
    public class ButtonEditLookupTests
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

        private static (ButtonEdit editor, FormDataObject dataObject, FormField field) BindLookupEditor()
        {
            var schema = BuildOrderSchema();
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var layout = FormLayoutGenerator.Generate(schema, "default");
            var layoutField = layout.Sections![0].Fields!.First(f => f.FieldName == "customer_rowid");

            var editor = new ButtonEdit();
            editor.Bind(dataObject, layoutField);
            return (editor, dataObject, dataObject.GetFormField("customer_rowid")!);
        }

        [Fact]
        [DisplayName("The Text of a lookup field shows the composed \"id - name\", not the Guid")]
        public void RefreshFromSource_ShowsComposedIdAndName()
        {
            var (editor, dataObject, field) = BindLookupEditor();

            dataObject.ApplyLookupSelection(field, BuildSelectedRow(Guid.NewGuid(), "C001", "客戶甲"));

            Assert.Equal("C001 - 客戶甲", editor.Text);
        }

        [Fact]
        [DisplayName("A change to any display field refreshes Text (WatchFieldNames)")]
        public void DisplayFieldChange_RefreshesText()
        {
            var (editor, dataObject, _) = BindLookupEditor();

            // Only the name field has a value. The empty id field is skipped, so the result is the name alone.
            dataObject.SetField("ref_customer_name", "客戶乙");

            Assert.Equal("客戶乙", editor.Text);
        }

        [Fact]
        [DisplayName("Setting Text from code does not write back to the rowid field (the display text is not the bound value)")]
        public void TextAssignment_DoesNotWriteBackToBoundField()
        {
            var (editor, dataObject, field) = BindLookupEditor();
            var customerId = Guid.NewGuid();
            dataObject.ApplyLookupSelection(field, BuildSelectedRow(customerId, "C001", "客戶甲"));

            editor.Text = "hand-typed";

            Assert.Equal(customerId.ToString(), dataObject.GetField("customer_rowid"));
        }

        [Fact]
        [DisplayName("In lookup mode the text box stays read-only in the Edit state")]
        public void SetControlState_LookupMode_TextStaysReadOnly()
        {
            var (editor, _, _) = BindLookupEditor();

            editor.SetControlState(SingleFormMode.Edit);

            Assert.True(editor.IsReadOnly);
            Assert.True(editor.HasLookup);
        }

        [Fact]
        [DisplayName("With a row-scoped binding (EditForm mode) the lookup write-back updates that row and refreshes the display")]
        public void RowScopedBinding_LookupWriteBack_UpdatesRowAndText()
        {
            // Master plus detail, with a product picked on a detail row (the binding path of `RowEditPanel` and `RowEditDialog`).
            var schema = new FormSchema("Order", "訂單") { CategoryId = "company" };
            var master = schema.Tables!.Add("Order", "訂單");
            master.Fields!.Add(new FormField(SysFields.RowId, "唯一識別", FieldDbType.Guid));
            var detail = schema.Tables!.Add("OrderLine", "訂單明細");
            detail.Fields!.Add(new FormField(SysFields.RowId, "唯一識別", FieldDbType.Guid));
            detail.Fields!.Add(new FormField(SysFields.MasterRowId, "主檔識別", FieldDbType.Guid));
            var productField = new FormField("product_rowid", "商品", FieldDbType.Guid)
            {
                RelationProgId = "Product",
            };
            productField.RelationFieldMappings!.Add(SysFields.Name, "ref_product_name");
            detail.Fields!.Add(productField);
            detail.Fields!.Add(new FormField("ref_product_name", "商品名稱", FieldDbType.String, FieldType.RelationField));

            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var lineTable = dataObject.DataSet.Tables["OrderLine"]!;
            var line = lineTable.NewRow();
            lineTable.Rows.Add(line);

            var layout = FormLayoutGenerator.Generate(schema, "default");
            var column = layout.Details![0].Columns!.First(c => c.FieldName == "product_rowid");
            var editor = new ButtonEdit();
            editor.Bind(dataObject, column, line);

            var productId = Guid.NewGuid();
            var selected = BuildSelectedRow(productId, "P001", "商品甲");
            var field = dataObject.GetFormField("OrderLine", "product_rowid")!;
            dataObject.ApplyLookupSelection(field, selected, line);

            Assert.Equal(productId.ToString(), dataObject.GetField(line, "product_rowid"));
            Assert.Equal("商品甲", dataObject.GetField(line, "ref_product_name"));
            Assert.Equal("商品甲", editor.Text);
        }

        [Fact]
        [DisplayName("A non-relation field keeps the TextEdit behavior (editable in Edit, no lookup)")]
        public void NonRelationField_KeepsTextEditBehaviour()
        {
            var schema = BuildOrderSchema();
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();

            var editor = new ButtonEdit();
            editor.Bind(dataObject, SysFields.Id);
            editor.SetControlState(SingleFormMode.Edit);

            Assert.False(editor.HasLookup);
            Assert.False(editor.IsReadOnly);
        }
    }
}
