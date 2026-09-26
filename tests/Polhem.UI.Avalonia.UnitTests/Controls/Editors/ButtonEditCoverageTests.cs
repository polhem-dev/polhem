using System.ComponentModel;
using System.Reflection;
using Polhem.Base.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Additional coverage for <see cref="ButtonEdit"/>: RefreshFromSource returns an empty string when the lookup has
    /// no display fields (the HasLookup plus empty displayFields path of ApplyMetadata), and
    /// OpenLookupAsync returns early without calling LookupDialog when editing is not allowed.
    /// </summary>
    public class ButtonEditCoverageTests
    {
        private static FormDataObject BuildDataObjectWithLookupNoDisplayFields()
        {
            var schema = new FormSchema("Order", "Order");
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add(new FormField("order_id", "Order ID", FieldDbType.String));
            // A lookup field without `RelationFieldMappings` makes `GetDisplayFields()` return an empty collection.
            table.Fields!.Add(new FormField("vendor_rowid", "Vendor", FieldDbType.Guid)
            {
                RelationProgId = "Vendor",
            });
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        private static FormDataObject BuildOrderDataObjectWithLookup()
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
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        private static async Task InvokeOnButtonClickAsync(ButtonEdit editor)
        {
            var method = typeof(ButtonEdit).GetMethod(
                "OnButtonClickAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            await (Task)method!.Invoke(editor, null)!;
        }

        [Fact]
        [DisplayName("A lookup field without RelationFieldMappings still has HasLookup true but shows an empty Text")]
        public void RefreshFromSource_LookupWithNoDisplayFields_SetsEmptyText()
        {
            var dataObject = BuildDataObjectWithLookupNoDisplayFields();
            var editor = new ButtonEdit();
            editor.Bind(dataObject, "vendor_rowid");

            Assert.True(editor.HasLookup);
            Assert.Equal(string.Empty, editor.Text);
        }

        [Fact]
        [DisplayName("A lookup field with a value but no DisplayFields still shows an empty string after a refresh")]
        public void RefreshFromSource_LookupValueSetNoDisplayFields_TextRemainsEmpty()
        {
            var dataObject = BuildDataObjectWithLookupNoDisplayFields();
            var editor = new ButtonEdit();
            editor.Bind(dataObject, "vendor_rowid");

            dataObject.SetField("vendor_rowid", Guid.NewGuid().ToString());

            Assert.Equal(string.Empty, editor.Text);
        }

        [Fact]
        [DisplayName("OnButtonClickAsync returns early in View mode (editing not allowed) without calling LookupDialog")]
        public async Task OnButtonClickAsync_LookupInViewMode_ReturnsEarlyWithoutDialog()
        {
            var dataObject = BuildOrderDataObjectWithLookup();
            var editor = new ButtonEdit();
            editor.Bind(dataObject, "customer_rowid");
            editor.SetControlState(SingleFormMode.View);
            Assert.True(editor.HasLookup);

            // View mode sets `_allowLookupEdit` to false, so `OpenLookupAsync` returns without opening `LookupDialog`.
            var exception = await Record.ExceptionAsync(() => InvokeOnButtonClickAsync(editor));

            Assert.Null(exception);
        }
    }
}
