using System.ComponentModel;
using System.Data;
using System.Reflection;
using Avalonia.Input;
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
    /// Additional coverage for <see cref="ButtonEdit"/>: the ButtonClick event (non-lookup field),
    /// Delete/Back clearing the lookup selection, and the layout-level DisplayFields override.
    /// </summary>
    public class ButtonEditAdditionalTests
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

        private static DataRow BuildSelectedCustomerRow(Guid rowId, string id, string name)
        {
            var table = new DataTable("Customer");
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Columns.Add(SysFields.Id, typeof(string));
            table.Columns.Add(SysFields.Name, typeof(string));
            table.Rows.Add(rowId, id, name);
            return table.Rows[0];
        }

        private static FormDataObject BuildOrderDataObject()
        {
            var schema = BuildOrderSchema();
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        private static void InvokeOnKeyDown(ButtonEdit editor, Key key)
        {
            var method = typeof(ButtonEdit).GetMethod(
                "OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var args = new KeyEventArgs { Key = key };
            method!.Invoke(editor, new object[] { args });
        }

        private static async Task InvokeOnButtonClickAsync(ButtonEdit editor)
        {
            var method = typeof(ButtonEdit).GetMethod(
                "OnButtonClickAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            await (Task)method!.Invoke(editor, null)!;
        }

        [Fact]
        [DisplayName("Clicking the button of a non-lookup field raises the ButtonClick event")]
        public async Task OnButtonClickAsync_NonLookupField_RaisesButtonClickEvent()
        {
            var schema = BuildOrderSchema();
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var editor = new ButtonEdit();
            editor.Bind(dataObject, SysFields.Id);
            Assert.False(editor.HasLookup);

            var invoked = false;
            editor.ButtonClick += (_, _) => invoked = true;

            await InvokeOnButtonClickAsync(editor);

            Assert.True(invoked);
        }

        [Fact]
        [DisplayName("The Delete key clears the selected value when the lookup allows editing")]
        public void OnKeyDown_Delete_LookupModeAllowEdit_ClearsSelection()
        {
            var dataObject = BuildOrderDataObject();
            var layout = FormLayoutGenerator.Generate(BuildOrderSchema(), "default");
            var layoutField = layout.Sections![0].Fields!.First(f => f.FieldName == "customer_rowid");
            var editor = new ButtonEdit();
            editor.Bind(dataObject, layoutField);
            editor.SetControlState(SingleFormMode.Edit);
            Assert.True(editor.HasLookup);

            var field = dataObject.GetFormField("customer_rowid")!;
            dataObject.ApplyLookupSelection(field, BuildSelectedCustomerRow(Guid.NewGuid(), "C001", "客戶甲"));
            Assert.False(string.IsNullOrEmpty(editor.Text));

            InvokeOnKeyDown(editor, Key.Delete);

            Assert.Equal(string.Empty, editor.Text);
        }

        [Fact]
        [DisplayName("The Back key also clears the lookup selection")]
        public void OnKeyDown_Back_LookupModeAllowEdit_ClearsSelection()
        {
            var dataObject = BuildOrderDataObject();
            var layout = FormLayoutGenerator.Generate(BuildOrderSchema(), "default");
            var layoutField = layout.Sections![0].Fields!.First(f => f.FieldName == "customer_rowid");
            var editor = new ButtonEdit();
            editor.Bind(dataObject, layoutField);
            editor.SetControlState(SingleFormMode.Edit);

            var field = dataObject.GetFormField("customer_rowid")!;
            dataObject.ApplyLookupSelection(field, BuildSelectedCustomerRow(Guid.NewGuid(), "C001", "客戶甲"));

            InvokeOnKeyDown(editor, Key.Back);

            Assert.Equal(string.Empty, editor.Text);
        }

        [Fact]
        [DisplayName("Layout-level DisplayFields override the schema default and show only the given fields")]
        public void RefreshFromSource_WithLayoutDisplayFields_UsesLayoutOverride()
        {
            var schema = BuildOrderSchema();
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();

            var layoutField = new LayoutField
            {
                FieldName = "customer_rowid",
                DisplayFields = "ref_customer_id",
            };
            var editor = new ButtonEdit();
            editor.Bind(dataObject, layoutField);
            editor.SetControlState(SingleFormMode.Edit);
            Assert.True(editor.HasLookup);

            var field = dataObject.GetFormField("customer_rowid")!;
            dataObject.ApplyLookupSelection(
                field,
                BuildSelectedCustomerRow(Guid.NewGuid(), "C001", "客戶甲"));

            Assert.Equal("C001", editor.Text);
        }
    }
}
