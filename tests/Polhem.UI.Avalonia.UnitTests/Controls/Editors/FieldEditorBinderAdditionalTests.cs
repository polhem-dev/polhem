using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Additional coverage for <see cref="FieldEditorBinder"/>: the <c>OnFormModeChanged</c> path triggered through
    /// the FormScope property (the class handler path, as opposed to calling
    /// <c>SetControlState</c> directly).
    /// </summary>
    public class FieldEditorBinderAdditionalTests
    {
        private static FormDataObject BuildDataObject()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add("emp_name", "Name", FieldDbType.String);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        [Fact]
        [DisplayName("Setting FormScope.FormModeProperty triggers OnFormModeChanged and View mode applies read-only")]
        public void OnFormModeChanged_ViaFormScopeProperty_ViewMode_SetsReadOnly()
        {
            var dataObject = BuildDataObject();
            var editor = new TextEdit();
            editor.Bind(dataObject, "emp_name");
            editor.SetControlState(SingleFormMode.Edit);
            Assert.False(editor.IsReadOnly);

            // class handler: FormScope.FormModeProperty.Changed.AddClassHandler<TextEdit>(
            //   (o, e) => o._binder.OnFormModeChanged((SingleFormMode)e.NewValue!))
            FormScope.SetFormMode(editor, SingleFormMode.View);

            Assert.True(editor.IsReadOnly);
        }

        [Fact]
        [DisplayName("Switching FormScope.FormModeProperty to Edit mode makes the editor editable")]
        public void OnFormModeChanged_ViaFormScopeProperty_EditMode_ClearsReadOnly()
        {
            var dataObject = BuildDataObject();
            var editor = new TextEdit();
            editor.Bind(dataObject, "emp_name");
            FormScope.SetFormMode(editor, SingleFormMode.View);
            Assert.True(editor.IsReadOnly);

            FormScope.SetFormMode(editor, SingleFormMode.Edit);

            Assert.False(editor.IsReadOnly);
        }

        [Fact]
        [DisplayName("With AllowEditModes=Add the editor stays read-only after Edit mode is set through FormScope")]
        public void OnFormModeChanged_ViaFormScope_AllowEditModesAdd_EditModeReadOnly()
        {
            var dataObject = BuildDataObject();
            var field = new LayoutField { FieldName = "emp_name", AllowEditModes = FormEditModes.Add };
            var editor = new TextEdit();
            editor.Bind(dataObject, field);

            FormScope.SetFormMode(editor, SingleFormMode.Add);
            Assert.False(editor.IsReadOnly);

            FormScope.SetFormMode(editor, SingleFormMode.Edit);
            Assert.True(editor.IsReadOnly);
        }
    }
}
