using System.ComponentModel;
using System.Reflection;
using Avalonia.Controls;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Additional coverage for <see cref="ButtonEdit"/>: SetControlState of a non-lookup field (calls base),
    /// OnPropertyChanged syncing the button's enabled state when IsReadOnly changes,
    /// and the layout-level DisplayFields override path of ResolveDisplayFieldNames.
    /// </summary>
    public class ButtonEditPropertyTests
    {
        private static Button GetButton(ButtonEdit editor)
        {
            var field = typeof(ButtonEdit).GetField("_button", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return (Button)field!.GetValue(editor)!;
        }

        private static FormSchema BuildOrderSchema()
        {
            var schema = new FormSchema("Order", "Order");
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add(new FormField("order_id", "Order ID", FieldDbType.String));
            var customerField = new FormField("customer_rowid", "Customer", FieldDbType.Guid)
            {
                RelationProgId = "Customer",
            };
            customerField.RelationFieldMappings!.Add("sys_id", "ref_customer_id");
            customerField.RelationFieldMappings!.Add("sys_name", "ref_customer_name");
            table.Fields!.Add(customerField);
            table.Fields!.Add(new FormField("ref_customer_id", "Customer Code", FieldDbType.String,
                Polhem.Definition.Database.FieldType.RelationField));
            table.Fields!.Add(new FormField("ref_customer_name", "Customer Name", FieldDbType.String,
                Polhem.Definition.Database.FieldType.RelationField));
            return schema;
        }

        [Fact]
        [DisplayName("SetControlState in Edit mode on a non-lookup field sets IsReadOnly=false and enables the button")]
        public void SetControlState_NonLookup_EditMode_IsReadOnlyFalseButtonEnabled()
        {
            var schema = BuildOrderSchema();
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var editor = new ButtonEdit();
            editor.Bind(dataObject, "order_id");
            Assert.False(editor.HasLookup);

            editor.SetControlState(SingleFormMode.Edit);

            Assert.False(editor.IsReadOnly);
            Assert.True(GetButton(editor).IsEnabled);
        }

        [Fact]
        [DisplayName("SetControlState in View mode on a non-lookup field sets IsReadOnly=true and disables the button")]
        public void SetControlState_NonLookup_ViewMode_IsReadOnlyTrueButtonDisabled()
        {
            var schema = BuildOrderSchema();
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var editor = new ButtonEdit();
            editor.Bind(dataObject, "order_id");
            editor.SetControlState(SingleFormMode.Edit);

            editor.SetControlState(SingleFormMode.View);

            Assert.True(editor.IsReadOnly);
            Assert.False(GetButton(editor).IsEnabled);
        }

        [Fact]
        [DisplayName("Setting IsReadOnly directly on a non-lookup field syncs the button's enabled state (OnPropertyChanged path)")]
        public void IsReadOnly_SetTrue_NonLookup_ButtonBecomesDisabled()
        {
            var editor = new ButtonEdit();
            editor.IsReadOnly = false;

            editor.IsReadOnly = true;

            Assert.False(GetButton(editor).IsEnabled);
        }

        [Fact]
        [DisplayName("With a layout-level DisplayFields override, Text shows only the field values the layout names")]
        public void RefreshFromSource_LayoutDisplayFieldsOverride_ShowsOnlyLayoutFields()
        {
            var schema = BuildOrderSchema();
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            dataObject.SetField("ref_customer_id", "C001");
            dataObject.SetField("ref_customer_name", "Alice");

            var layoutField = new LayoutField
            {
                FieldName = "customer_rowid",
                DisplayFields = "ref_customer_name",
            };
            var editor = new ButtonEdit();
            editor.Bind(dataObject, layoutField);

            Assert.Equal("Alice", editor.Text);
        }
    }
}
