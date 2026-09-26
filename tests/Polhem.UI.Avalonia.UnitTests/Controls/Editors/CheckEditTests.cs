using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Behaviour checks for <see cref="CheckEdit"/>: boolean round-trip and
    /// form-mode state.
    /// </summary>
    public class CheckEditTests
    {
        private static FormDataObject BuildDataObject()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add("is_active", "Active", FieldDbType.Boolean);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        [Fact]
        [DisplayName("Bind loads the initial boolean value")]
        public void Bind_TrueValue_LoadsIntoIsChecked()
        {
            var dataObject = BuildDataObject();
            dataObject.SetField("is_active", bool.TrueString);

            var editor = new CheckEdit();
            editor.Bind(dataObject, "is_active");

            Assert.True(editor.IsChecked);
        }

        [Fact]
        [DisplayName("A change of the check state is written back to FormDataObject")]
        public void IsCheckedChanged_AfterBind_WritesBack()
        {
            var dataObject = BuildDataObject();
            var editor = new CheckEdit();
            editor.Bind(dataObject, "is_active");

            editor.IsChecked = true;

            Assert.Equal("True", dataObject.GetField("is_active"));
        }

        [Fact]
        [DisplayName("SetControlState disables the editor in View mode and enables it in Edit mode")]
        public void SetControlState_ViewMode_TogglesIsEnabled()
        {
            var dataObject = BuildDataObject();
            var editor = new CheckEdit();
            editor.Bind(dataObject, "is_active");

            editor.SetControlState(SingleFormMode.View);
            Assert.False(editor.IsEnabled);

            editor.SetControlState(SingleFormMode.Edit);
            Assert.True(editor.IsEnabled);
        }

        [Fact]
        [DisplayName("With AllowEditModes=Add only Add mode enables the editor")]
        public void SetControlState_AllowEditModesAdd_OnlyAddEnabled()
        {
            var dataObject = BuildDataObject();
            var field = new LayoutField { FieldName = "is_active", AllowEditModes = FormEditModes.Add };
            var editor = new CheckEdit();
            editor.Bind(dataObject, field);

            editor.SetControlState(SingleFormMode.Add);
            Assert.True(editor.IsEnabled);

            editor.SetControlState(SingleFormMode.Edit);
            Assert.False(editor.IsEnabled);

            editor.SetControlState(SingleFormMode.View);
            Assert.False(editor.IsEnabled);
        }

        [Fact]
        [DisplayName("FieldValue accepts bool and string representations")]
        public void FieldValue_BoolAndString_MapToIsChecked()
        {
            var editor = new CheckEdit();

            editor.FieldValue = true;
            Assert.True(editor.IsChecked);

            editor.FieldValue = "false";
            Assert.False(editor.IsChecked);
        }
    }
}
