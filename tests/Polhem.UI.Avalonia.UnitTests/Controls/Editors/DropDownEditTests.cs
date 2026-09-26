using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Behaviour checks for <see cref="DropDownEdit"/>: option loading from
    /// <c>FormField.ListItems</c>, value selection and write-back.
    /// </summary>
    public class DropDownEditTests
    {
        private static FormDataObject BuildDataObject()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            var dept = master.Fields!.Add("dept_id", "Department", FieldDbType.String);
            dept.ListItems!.Add("HR", "Human Resources");
            dept.ListItems.Add("IT", "Information Technology");
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        [Fact]
        [DisplayName("Bind loads FormField.ListItems as the options")]
        public void Bind_FieldWithListItems_LoadsOptions()
        {
            var dataObject = BuildDataObject();
            var editor = new DropDownEdit();

            editor.Bind(dataObject, "dept_id");

            var items = Assert.IsType<IEnumerable<ListItem>>(editor.ItemsSource, exactMatch: false).ToList();
            Assert.Equal(2, items.Count);
            Assert.Equal("HR", items[0].Value);
        }

        [Fact]
        [DisplayName("Bind selects the item matching the field value")]
        public void Bind_ExistingValue_SelectsMatchingItem()
        {
            var dataObject = BuildDataObject();
            dataObject.SetField("dept_id", "IT");

            var editor = new DropDownEdit();
            editor.Bind(dataObject, "dept_id");

            var selected = Assert.IsType<ListItem>(editor.SelectedItem);
            Assert.Equal("IT", selected.Value);
        }

        [Fact]
        [DisplayName("A selection change is written back as ListItem.Value")]
        public void SelectionChanged_AfterBind_WritesBackValue()
        {
            var dataObject = BuildDataObject();
            var editor = new DropDownEdit();
            editor.Bind(dataObject, "dept_id");

            editor.SelectedIndex = 0;

            Assert.Equal("HR", dataObject.GetField("dept_id"));
        }

        [Fact]
        [DisplayName("A SetField from another writer selects the new value")]
        public void FieldValueChanged_OtherWriter_UpdatesSelection()
        {
            var dataObject = BuildDataObject();
            var editor = new DropDownEdit();
            editor.Bind(dataObject, "dept_id");

            dataObject.SetField("dept_id", "HR");

            var selected = Assert.IsType<ListItem>(editor.SelectedItem);
            Assert.Equal("HR", selected.Value);
        }

        [Fact]
        [DisplayName("With AllowEditModes=Add only Add mode enables the editor")]
        public void SetControlState_AllowEditModesAdd_OnlyAddEnabled()
        {
            var dataObject = BuildDataObject();
            var field = new LayoutField { FieldName = "dept_id", AllowEditModes = FormEditModes.Add };
            var editor = new DropDownEdit();
            editor.Bind(dataObject, field);

            // Read-only swaps to a flat, non-interactive display rather than greying out,
            // so editability is observed through IsHitTestVisible instead of IsEnabled.
            editor.SetControlState(SingleFormMode.Add);
            Assert.True(editor.IsHitTestVisible);

            editor.SetControlState(SingleFormMode.Edit);
            Assert.False(editor.IsHitTestVisible);

            editor.SetControlState(SingleFormMode.View);
            Assert.False(editor.IsHitTestVisible);
        }

        [Fact]
        [DisplayName("ReadOnlyText shows the text of the selected item after Bind")]
        public void ReadOnlyText_AfterBind_ShowsSelectedItemText()
        {
            var dataObject = BuildDataObject();
            dataObject.SetField("dept_id", "IT");

            var editor = new DropDownEdit();
            editor.Bind(dataObject, "dept_id");

            Assert.Equal("Information Technology", editor.ReadOnlyText);
        }
    }
}
