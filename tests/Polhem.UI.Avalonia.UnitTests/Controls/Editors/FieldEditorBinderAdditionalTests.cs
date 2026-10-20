using System.ComponentModel;
using Polhem.Core.Data;
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

        [Fact]
        [DisplayName("AttachLayout applies the layout's ReadOnly to an editor that has no data object")]
        public void AttachLayout_NoDataObject_AppliesReadOnly()
        {
            var editor = new TextEdit();
            var binder = ((IFieldEditorBinderHost)editor).Binder;

            binder.AttachLayout(new LayoutField { FieldName = "emp_name", ReadOnly = true });

            Assert.False(binder.IsBound);
            Assert.True(editor.IsReadOnly);
        }

        [Fact]
        [DisplayName("The attached layout survives Unbind and still decides AllowsEdit")]
        public void AttachLayout_SurvivesUnbind()
        {
            var editor = new TextEdit();
            var binder = ((IFieldEditorBinderHost)editor).Binder;
            var field = new LayoutField { FieldName = "emp_name", AllowEditModes = FormEditModes.Add };
            binder.AttachLayout(field);
            editor.Bind(BuildDataObject(), "emp_name");

            editor.Unbind();

            Assert.Same(field, binder.LayoutField);
            Assert.False(binder.AllowsEdit(SingleFormMode.Edit));
            Assert.True(binder.AllowsEdit(SingleFormMode.Add));
        }

        [Fact]
        [DisplayName("A bind by field name only keeps the attached layout, and an explicit layout bind wins over it")]
        public void AttachLayout_BindByNameKeepsIt_ExplicitLayoutWins()
        {
            var editor = new TextEdit();
            var binder = ((IFieldEditorBinderHost)editor).Binder;
            var attached = new LayoutField { FieldName = "emp_name", ReadOnly = true };
            var explicitField = new LayoutField { FieldName = "emp_name" };
            binder.AttachLayout(attached);

            editor.Bind(BuildDataObject(), "emp_name");
            Assert.Same(attached, binder.LayoutField);

            editor.Bind(BuildDataObject(), explicitField);
            Assert.Same(explicitField, binder.LayoutField);
        }
    }
}
