using System.ComponentModel;
using System.Reflection;
using Polhem.Base.Data;
using Polhem.Definition.Forms;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Covers the ambient binding path of <see cref="FieldEditorBinder"/>: it calls NotifyAttached / NotifyDetached through
    /// reflection on the private _binder of <see cref="TextEdit"/>,
    /// simulating how OnAttachedToLogicalTree / OnDetachedFromLogicalTree trigger them.
    /// It also verifies that OnBindingContextChanged rebinds when the DataObject changes after attaching.
    /// </summary>
    public class FieldEditorBinderCoverageTests
    {
        private static FormDataObject BuildDataObject(string empName = "Alice")
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add("emp_name", "Name", FieldDbType.String);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            dataObject.SetField("emp_name", empName);
            return dataObject;
        }

        private static object GetBinder(TextEdit editor)
        {
            var field = typeof(TextEdit).GetField("_binder", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return field!.GetValue(editor)!;
        }

        private static void InvokeNotifyAttached(object binder)
        {
            var method = binder.GetType().GetMethod("NotifyAttached");
            Assert.NotNull(method);
            method!.Invoke(binder, null);
        }

        private static void InvokeNotifyDetached(object binder)
        {
            var method = binder.GetType().GetMethod("NotifyDetached");
            Assert.NotNull(method);
            method!.Invoke(binder, null);
        }

        [Fact]
        [DisplayName("NotifyAttached binds the editor and loads the initial value when the ambient DataObject and FieldName are set")]
        public void NotifyAttached_WithAmbientDataObjectAndFieldName_BindsEditorAndLoadsValue()
        {
            var dataObject = BuildDataObject("Alice");
            var editor = new TextEdit();
            editor.FieldName = "emp_name";
            FormScope.SetDataObject(editor, dataObject);

            InvokeNotifyAttached(GetBinder(editor));

            Assert.Equal("Alice", editor.Text);
        }

        [Fact]
        [DisplayName("NotifyAttached does not bind when the ambient DataObject is not set, and Text stays null")]
        public void NotifyAttached_NoAmbientDataObject_DoesNotBind()
        {
            var editor = new TextEdit();
            editor.FieldName = "emp_name";

            InvokeNotifyAttached(GetBinder(editor));

            Assert.Null(editor.Text);
        }

        [Fact]
        [DisplayName("After NotifyDetached on an ambient binding, later SetField calls no longer refresh the editor")]
        public void NotifyDetached_AfterAmbientBind_StopsUpdates()
        {
            var dataObject = BuildDataObject("Alice");
            var editor = new TextEdit();
            editor.FieldName = "emp_name";
            FormScope.SetDataObject(editor, dataObject);
            var binder = GetBinder(editor);
            InvokeNotifyAttached(binder);
            Assert.Equal("Alice", editor.Text);

            InvokeNotifyDetached(binder);

            dataObject.SetField("emp_name", "Bob");
            Assert.Equal("Alice", editor.Text);
        }

        [Fact]
        [DisplayName("OnBindingContextChanged rebinds the editor to the new DataObject when it changes after attaching")]
        public void OnBindingContextChanged_AfterAttach_NewDataObject_Rebinds()
        {
            var dataObject1 = BuildDataObject("Alice");
            var dataObject2 = BuildDataObject("Bob");

            var editor = new TextEdit();
            editor.FieldName = "emp_name";
            FormScope.SetDataObject(editor, dataObject1);
            InvokeNotifyAttached(GetBinder(editor));
            Assert.Equal("Alice", editor.Text);

            // Changing the DataObject fires the `DataObjectProperty.Changed` class handler, which calls `OnBindingContextChanged` to rebind.
            FormScope.SetDataObject(editor, dataObject2);

            Assert.Equal("Bob", editor.Text);
        }

        [Fact]
        [DisplayName("OnBindingContextChanged does not rebind for the same DataObject and FieldName (short-circuit)")]
        public void OnBindingContextChanged_SameDataObjectAndFieldName_IsNoOp()
        {
            var dataObject = BuildDataObject("Carol");
            var editor = new TextEdit();
            editor.FieldName = "emp_name";
            FormScope.SetDataObject(editor, dataObject);
            InvokeNotifyAttached(GetBinder(editor));
            Assert.Equal("Carol", editor.Text);

            editor.Text = "Carol-modified";

            // Setting the same DataObject again short-circuits, so the initial value is not reloaded and Text keeps the edited value.
            FormScope.SetDataObject(editor, dataObject);
            Assert.Equal("Carol-modified", editor.Text);
        }
    }
}
