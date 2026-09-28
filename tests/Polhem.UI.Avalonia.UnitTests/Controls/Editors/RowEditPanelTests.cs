using System.ComponentModel;
using System.Data;
using Avalonia.Input;
using Polhem.Base.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Behaviour checks for <see cref="RowEditPanel"/>: buffered edit session
    /// lifecycle (bind / commit / cancel / rebind) through the
    /// <see cref="FormDataObject"/> row edit protocol.
    /// </summary>
    public class RowEditPanelTests
    {
        private static FormDataObject BuildDataObject()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add("emp_name", "Name", FieldDbType.String);
            var detail = schema.Tables.Add("EmployeePhone", "Phones");
            detail.Fields!.Add("phone", "Phone", FieldDbType.String);
            detail.Fields.Add("is_primary", "Primary", FieldDbType.Boolean);

            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var table = dataObject.DataSet.Tables["EmployeePhone"]!;
            table.Rows.Add("02-1234-5678", false);
            table.Rows.Add("0912-345-678", true);
            table.AcceptChanges();
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        private static LayoutGrid BuildLayout()
        {
            var layout = new LayoutGrid("EmployeePhone", "Phones");
            layout.Columns!.Add(new LayoutColumn("phone", "Phone", ControlType.TextEdit));
            layout.Columns.Add(new LayoutColumn("is_primary", "Primary", ControlType.CheckEdit));
            layout.Columns.Add(new LayoutColumn("hidden", "Hidden", ControlType.TextEdit) { Visible = false });
            return layout;
        }

        // The editors sit in a grid inside the scrolled area; the buttons are docked outside it.
        private static global::Avalonia.Controls.Grid EditorGrid(RowEditPanel panel)
        {
            var host = Assert.IsType<global::Avalonia.Controls.DockPanel>(panel.Content);
            var scroller = host.Children.OfType<global::Avalonia.Controls.ScrollViewer>().Single();
            var padding = Assert.IsType<global::Avalonia.Controls.Border>(scroller.Content);
            return Assert.IsType<global::Avalonia.Controls.Grid>(padding.Child);
        }

        private static T FindEditor<T>(RowEditPanel panel)
            where T : global::Avalonia.Controls.Control
        {
            var grid = EditorGrid(panel);
            return grid.Children
                .OfType<global::Avalonia.Controls.StackPanel>()
                .Select(cell => cell.Children[1])
                .OfType<T>()
                .First();
        }

        [Fact]
        [DisplayName("Bind starts an edit session and creates editors for the visible fields")]
        public void Bind_BuildsEditorsAndStartsSession()
        {
            var dataObject = BuildDataObject();
            var row = dataObject.DataSet.Tables["EmployeePhone"]!.Rows[0];
            var panel = new RowEditPanel();

            panel.Bind(dataObject, BuildLayout(), row);

            Assert.Same(row, panel.Row);
            Assert.True(row.HasVersion(DataRowVersion.Proposed));
            var textEditor = FindEditor<TextEdit>(panel);
            Assert.Equal("02-1234-5678", textEditor.Text);
            var checkEditor = FindEditor<CheckEdit>(panel);
            Assert.False(checkEditor.IsChecked);
            // The invisible column produces no editor: one text + one check only.
            Assert.Equal(2, EditorGrid(panel).Children.Count);
        }

        [Fact]
        [DisplayName("Commit applies the edit, publishes the events and marks dirty")]
        public void Commit_WritesThroughAndPublishes()
        {
            var dataObject = BuildDataObject();
            var row = dataObject.DataSet.Tables["EmployeePhone"]!.Rows[0];
            var panel = new RowEditPanel();
            panel.Bind(dataObject, BuildLayout(), row);

            var raised = new List<FieldValueChangedEventArgs>();
            dataObject.FieldValueChanged += (_, e) => raised.Add(e);
            var committedRaised = 0;
            panel.EditCommitted += (_, _) => committedRaised++;

            var editor = FindEditor<TextEdit>(panel);
            editor.Text = "07-999-8888";
            // Commit-on-leave: the value writes to the buffered row when the field commits
            // (Enter here; clicking OK blurs the field in the real UI), still suppressed by
            // the edit session.
            editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            Assert.Empty(raised);   // buffered: nothing publishes during the session

            panel.Commit();

            Assert.Equal("07-999-8888", row["phone"]);
            Assert.Equal(1, committedRaised);
            var args = Assert.Single(raised);
            Assert.Equal("phone", args.FieldName, ignoreCase: true);
            Assert.True(dataObject.IsDirty);
            Assert.Null(panel.Row);
        }

        [Fact]
        [DisplayName("Cancel restores everything with no events and does not mark dirty")]
        public void Cancel_RestoresSilently()
        {
            var dataObject = BuildDataObject();
            var row = dataObject.DataSet.Tables["EmployeePhone"]!.Rows[0];
            var panel = new RowEditPanel();
            panel.Bind(dataObject, BuildLayout(), row);

            var raisedCount = 0;
            dataObject.FieldValueChanged += (_, _) => raisedCount++;
            var cancelledRaised = 0;
            panel.EditCancelled += (_, _) => cancelledRaised++;

            var editor = FindEditor<TextEdit>(panel);
            editor.Text = "07-999-8888";
            editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            panel.Cancel();

            Assert.Equal("02-1234-5678", row["phone"]);
            Assert.Equal(0, raisedCount);
            Assert.Equal(1, cancelledRaised);
            Assert.False(dataObject.IsDirty);
        }

        [Fact]
        [DisplayName("An editor in the edit form refreshes when another party writes its field during the session")]
        public void Session_WriteByAnotherParty_RefreshesEditor()
        {
            var dataObject = BuildDataObject();
            var row = dataObject.DataSet.Tables["EmployeePhone"]!.Rows[0];
            var panel = new RowEditPanel();
            panel.Bind(dataObject, BuildLayout(), row);

            // What a live recompute of a computed field does while the edit form is open.
            row["phone"] = "07-999-8888";

            Assert.Equal("07-999-8888", FindEditor<TextEdit>(panel).Text);
            panel.Cancel();
            Assert.Equal("02-1234-5678", row["phone"]);
        }

        [Theory]
        [InlineData(false, 2)]  // wide screen → two columns
        [InlineData(true, 1)]   // compact screen → single column
        [DisplayName("The compact flag decides the column count of the edit form")]
        public void Compact_DrivesColumnCount(bool compact, int expectedColumns)
        {
            var dataObject = BuildDataObject();
            var row = dataObject.DataSet.Tables["EmployeePhone"]!.Rows[0];
            var panel = new RowEditPanel { Compact = compact };

            panel.Bind(dataObject, BuildLayout(), row);

            Assert.Equal(expectedColumns, EditorGrid(panel).ColumnDefinitions.Count);
        }

        [Fact]
        [DisplayName("The OK and Cancel buttons are docked below the scrolled editor area, so a height limit never hides them")]
        public void Bind_ButtonsDockedOutsideScrolledEditors()
        {
            var dataObject = BuildDataObject();
            var row = dataObject.DataSet.Tables["EmployeePhone"]!.Rows[0];
            var panel = new RowEditPanel();

            panel.Bind(dataObject, BuildLayout(), row);

            var host = Assert.IsType<global::Avalonia.Controls.DockPanel>(panel.Content);
            var buttons = host.Children.OfType<global::Avalonia.Controls.StackPanel>().Single();
            Assert.Equal(global::Avalonia.Controls.Dock.Bottom, global::Avalonia.Controls.DockPanel.GetDock(buttons));
            Assert.Equal(2, buttons.Children.OfType<global::Avalonia.Controls.Button>().Count());
            // The scroller is the fill child, which a DockPanel sizes last from what the buttons leave.
            Assert.IsType<global::Avalonia.Controls.ScrollViewer>(host.Children[^1]);
        }

        [Theory]
        [InlineData(400.0, true)]   // phone-sized → compact
        [InlineData(900.0, false)]  // desktop → not compact
        [InlineData(600.0, false)]  // exactly threshold → not compact
        [InlineData(0.0, false)]    // unmeasured → not compact
        [DisplayName("IsCompactWidth decides compact from the screen width")]
        public void IsCompactWidth_ByScreenWidth(double width, bool expected)
        {
            Assert.Equal(expected, RowEditPanel.IsCompactWidth(width));
        }

        [Fact]
        [DisplayName("Binding again cancels the previous session")]
        public void Rebind_CancelsPreviousSession()
        {
            var dataObject = BuildDataObject();
            var table = dataObject.DataSet.Tables["EmployeePhone"]!;
            var panel = new RowEditPanel();

            panel.Bind(dataObject, BuildLayout(), table.Rows[0]);
            FindEditor<TextEdit>(panel).Text = "07-999-8888";

            panel.Bind(dataObject, BuildLayout(), table.Rows[1]);

            Assert.Equal("02-1234-5678", table.Rows[0]["phone"]);
            Assert.False(table.Rows[0].HasVersion(DataRowVersion.Proposed));
            Assert.Same(table.Rows[1], panel.Row);
        }
    }
}
