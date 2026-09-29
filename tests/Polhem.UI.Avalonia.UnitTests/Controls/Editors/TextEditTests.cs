using System.ComponentModel;
using Avalonia.Input;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Behaviour checks for <see cref="TextEdit"/> (and the <see cref="FieldEditorBinder"/>
    /// plumbing it shares with the other editors): explicit bind, metadata application,
    /// write-back, event-driven refresh and form-mode state.
    /// </summary>
    public class TextEditTests
    {
        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add("emp_name", "Name", FieldDbType.String);
            var empId = master.Fields.Add("emp_id", "ID", FieldDbType.String);
            empId.MaxLength = 10;
            return schema;
        }

        private static FormDataObject BuildDataObject()
        {
            var dataObject = new FormDataObject(BuildSchema());
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        [Fact]
        [DisplayName("Bind loads the initial field value")]
        public void Bind_ExistingValue_LoadsIntoText()
        {
            var dataObject = BuildDataObject();
            dataObject.SetField("emp_name", "Alice");

            var editor = new TextEdit();
            editor.Bind(dataObject, "emp_name");

            Assert.Equal("Alice", editor.Text);
        }

        [Fact]
        [DisplayName("Typing does not write back (committing takes leaving the control or Enter)")]
        public void Typing_AloneDoesNotWriteBack()
        {
            var dataObject = BuildDataObject();
            var editor = new TextEdit();
            editor.Bind(dataObject, "emp_name");

            editor.Text = "Bob";

            // Per-keystroke text changes no longer commit; the value writes back on leaving
            // the control (LostFocus) or pressing Enter.
            Assert.NotEqual("Bob", dataObject.GetField("emp_name"));
            Assert.False(dataObject.IsDirty);
        }

        [Fact]
        [DisplayName("A single-line TextEdit commits on Enter and writes back to FormDataObject")]
        public void EnterKey_OnSingleLine_WritesBack()
        {
            var dataObject = BuildDataObject();
            var editor = new TextEdit();
            editor.Bind(dataObject, "emp_name");

            editor.Text = "Bob";
            editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            Assert.Equal("Bob", dataObject.GetField("emp_name"));
            Assert.True(dataObject.IsDirty);
        }

        [Fact]
        [DisplayName("Bind applies FormField.MaxLength")]
        public void Bind_FieldWithMaxLength_AppliesMaxLength()
        {
            var dataObject = BuildDataObject();
            var editor = new TextEdit();

            editor.Bind(dataObject, "emp_id");

            Assert.Equal(10, editor.MaxLength);
        }

        [Fact]
        [DisplayName("Bind with LayoutField.ReadOnly makes the editor read-only")]
        public void Bind_ReadOnlyLayoutField_SetsIsReadOnly()
        {
            var dataObject = BuildDataObject();
            var field = new LayoutField { FieldName = "emp_name", ReadOnly = true };
            var editor = new TextEdit();

            editor.Bind(dataObject, field);

            Assert.True(editor.IsReadOnly);
        }

        [Fact]
        [DisplayName("The editor refreshes when another writer calls SetField on the same field")]
        public void FieldValueChanged_OtherWriter_RefreshesEditor()
        {
            var dataObject = BuildDataObject();
            var editor = new TextEdit();
            editor.Bind(dataObject, "emp_name");

            dataObject.SetField("emp_name", "Carol");

            Assert.Equal("Carol", editor.Text);
        }

        [Fact]
        [DisplayName("The editor reloads its value after DataSetReplaced")]
        public void DataSetReplaced_AfterBind_RefreshesEditor()
        {
            var dataObject = BuildDataObject();
            var editor = new TextEdit();
            editor.Bind(dataObject, "emp_name");
            editor.Text = "Bob";

            dataObject.InitializeNewMaster();

            Assert.Equal(string.Empty, editor.Text);
        }

        [Fact]
        [DisplayName("An editor refresh does not write back or dirty the data (echo guard)")]
        public void Refresh_FromSource_DoesNotDirtyDataObject()
        {
            var dataObject = BuildDataObject();
            dataObject.SetField("emp_name", "Alice");
            var editor = new TextEdit();

            editor.Bind(dataObject, "emp_name");
            dataObject.InitializeNewMaster();

            Assert.False(dataObject.IsDirty);
        }

        [Fact]
        [DisplayName("SetControlState makes the editor read-only in View mode and editable in Edit mode")]
        public void SetControlState_ViewMode_TogglesReadOnly()
        {
            var dataObject = BuildDataObject();
            var editor = new TextEdit();
            editor.Bind(dataObject, "emp_name");

            editor.SetControlState(SingleFormMode.View);
            Assert.True(editor.IsReadOnly);
            // Read-only collapses the box border to a single bottom line.
            Assert.Equal(new global::Avalonia.Thickness(0, 0, 0, 1), editor.BorderThickness);

            editor.SetControlState(SingleFormMode.Edit);
            Assert.False(editor.IsReadOnly);
            // Editable clears the local override, restoring the theme border.
            Assert.False(editor.IsSet(global::Avalonia.Controls.Primitives.TemplatedControl.BorderThicknessProperty));
        }

        [Fact]
        [DisplayName("Input no longer writes back after Unbind")]
        public void Unbind_AfterBind_StopsWriteBack()
        {
            var dataObject = BuildDataObject();
            var editor = new TextEdit();
            editor.Bind(dataObject, "emp_name");

            editor.Unbind();
            editor.Text = "Bob";
            editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            Assert.Equal(string.Empty, dataObject.GetField("emp_name"));
        }

        [Fact]
        [DisplayName("Row binding loads the detail row value, writes back to that row and applies the detail table metadata")]
        public void BindRow_DetailRow_LoadsWritesAndAppliesMetadata()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add("emp_name", "Name", FieldDbType.String);
            var detail = schema.Tables.Add("EmployeePhone", "Phones");
            var phone = detail.Fields!.Add("phone", "Phone", FieldDbType.String);
            phone.MaxLength = 15;

            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var table = dataObject.DataSet.Tables["EmployeePhone"]!;
            table.Rows.Add("02-1234-5678");
            var row = table.Rows[0];

            var editor = new TextEdit();
            editor.Bind(dataObject, new LayoutColumn("phone", "Phone", ControlType.TextEdit), row);

            Assert.Equal("02-1234-5678", editor.Text);
            Assert.Equal(15, editor.MaxLength);

            editor.Text = "0912-345-678";
            editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            Assert.Equal("0912-345-678", row["phone"]);
        }

        [Fact]
        [DisplayName("Row binding: a change on another row does not refresh; another writer's change on this row does")]
        public void BindRow_EventFiltering_MatchesTargetRowOnly()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add("emp_name", "Name", FieldDbType.String);
            var detail = schema.Tables.Add("EmployeePhone", "Phones");
            detail.Fields!.Add("phone", "Phone", FieldDbType.String);

            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var table = dataObject.DataSet.Tables["EmployeePhone"]!;
            table.Rows.Add("row0");
            table.Rows.Add("row1");

            var editor = new TextEdit();
            editor.Bind(dataObject, new LayoutColumn("phone", "Phone", ControlType.TextEdit), table.Rows[0]);

            dataObject.SetField(table.Rows[1], "phone", "other-row");
            Assert.Equal("row0", editor.Text);

            dataObject.SetField(table.Rows[0], "phone", "target-row");
            Assert.Equal("target-row", editor.Text);
        }

        [Fact]
        [DisplayName("MemoEdit defaults to multi-line settings")]
        public void MemoEdit_Defaults_AreMultiLine()
        {
            var editor = new MemoEdit();

            Assert.True(editor.AcceptsReturn);
            Assert.Equal(global::Avalonia.Media.TextWrapping.Wrap, editor.TextWrapping);
            Assert.Equal(60, editor.MinHeight);
        }

        [Fact]
        [DisplayName("ButtonEdit embeds a button and forwards ButtonClick")]
        public void ButtonEdit_EmbeddedButton_RaisesButtonClick()
        {
            var editor = new ButtonEdit();
            var button = Assert.IsType<global::Avalonia.Controls.Button>(editor.InnerRightContent);

            var raised = 0;
            editor.ButtonClick += (_, _) => raised++;
            button.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(
                global::Avalonia.Controls.Button.ClickEvent));

            Assert.Equal(1, raised);
        }

        [Fact]
        [DisplayName("ButtonEdit disables the embedded button in View mode and restores it in Edit mode")]
        public void ButtonEdit_SetControlState_TogglesButtonEnabled()
        {
            var dataObject = BuildDataObject();
            var editor = new ButtonEdit();
            editor.Bind(dataObject, "emp_name");
            var button = Assert.IsType<global::Avalonia.Controls.Button>(editor.InnerRightContent);

            editor.SetControlState(SingleFormMode.View);
            Assert.False(button.IsEnabled);

            editor.SetControlState(SingleFormMode.Edit);
            Assert.True(button.IsEnabled);
        }

        [Fact]
        [DisplayName("ButtonEdit disables the embedded button when bound to a ReadOnly LayoutField")]
        public void ButtonEdit_BindReadOnlyLayoutField_DisablesButton()
        {
            var dataObject = BuildDataObject();
            var field = new LayoutField { FieldName = "emp_name", ReadOnly = true };
            var editor = new ButtonEdit();

            editor.Bind(dataObject, field);

            var button = Assert.IsType<global::Avalonia.Controls.Button>(editor.InnerRightContent);
            Assert.False(button.IsEnabled);
        }

        [Fact]
        [DisplayName("With AllowEditModes=Add only Add mode is editable (for example a document number field)")]
        public void SetControlState_AllowEditModesAdd_OnlyAddEditable()
        {
            var dataObject = BuildDataObject();
            var field = new LayoutField { FieldName = "emp_name", AllowEditModes = FormEditModes.Add };
            var editor = new TextEdit();
            editor.Bind(dataObject, field);

            editor.SetControlState(SingleFormMode.Add);
            Assert.False(editor.IsReadOnly);

            editor.SetControlState(SingleFormMode.Edit);
            Assert.True(editor.IsReadOnly);

            editor.SetControlState(SingleFormMode.View);
            Assert.True(editor.IsReadOnly);
        }

        [Fact]
        [DisplayName("With AllowEditModes=Add the embedded ButtonEdit button is enabled and disabled with the mode")]
        public void ButtonEdit_AllowEditModesAdd_ButtonFollowsMode()
        {
            var dataObject = BuildDataObject();
            var field = new LayoutField { FieldName = "emp_name", AllowEditModes = FormEditModes.Add };
            var editor = new ButtonEdit();
            editor.Bind(dataObject, field);
            var button = Assert.IsType<global::Avalonia.Controls.Button>(editor.InnerRightContent);

            editor.SetControlState(SingleFormMode.Add);
            Assert.True(button.IsEnabled);

            editor.SetControlState(SingleFormMode.Edit);
            Assert.False(button.IsEnabled);
        }
    }
}
