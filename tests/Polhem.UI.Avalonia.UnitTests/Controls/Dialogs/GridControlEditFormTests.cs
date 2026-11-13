using System.ComponentModel;
using System.Data;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Core.DataObjects;
using Polhem.UI.Core.Editing;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Dialogs
{
    /// <summary>
    /// The <see cref="GridEditMode.EditForm"/> flows of <see cref="GridControl"/> driven through its toolbar buttons and
    /// a double tap, with the row editor opened as an overlay dialog because the host has no native window.
    /// </summary>
    public class GridControlEditFormTests
    {
        private static (FormDataObject DataObject, FormSchema Schema) CreateDataObject(int lines)
        {
            var schema = new FormSchema("Order", "Order");
            var master = schema.Tables!.Add("Order", "Order");
            master.Fields!.Add(new FormField(SysFields.RowId, "Row", FieldDbType.Guid));
            var detail = schema.Tables.Add("OrderLine", "Lines");
            detail.Fields!.Add(new FormField(SysFields.RowId, "Row", FieldDbType.Guid));
            detail.Fields.Add(new FormField(SysFields.MasterRowId, "Order", FieldDbType.Guid));
            detail.Fields.Add(new FormField("note", "Note", FieldDbType.String));

            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var table = dataObject.DataSet.Tables["OrderLine"]!;
            for (var i = 0; i < lines; i++)
            {
                var row = table.NewRow();
                row[SysFields.RowId] = Guid.NewGuid();
                row["note"] = "line " + i;
                table.Rows.Add(row);
            }
            table.AcceptChanges();
            return (dataObject, schema);
        }

        private static (OverlayTestHost Host, GridControl Grid, DataTable Table) CreateGrid(int lines)
        {
            var (dataObject, schema) = CreateDataObject(lines);
            var grid = new GridControl { AllowEdit = true, EditMode = GridEditMode.EditForm };
            var host = new OverlayTestHost(grid);
            grid.Bind(dataObject, FormLayoutGenerator.Generate(schema, "default").Details![0]);
            return (host, grid, dataObject.DataSet.Tables["OrderLine"]!);
        }

        private static Button ToolbarButton(GridControl grid, int index)
        {
            var toolbar = Assert.IsType<StackPanel>(Assert.IsType<DockPanel>(grid.Content).Children[0]);
            return Assert.IsType<Button>(toolbar.Children[index]);
        }

        private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        private static void DoubleTap(Control target)
        {
            var pointer = new Pointer(1, PointerType.Mouse, isPrimary: true);
            var press = new PointerPressedEventArgs(target, pointer, target, default, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None, clickCount: 2);
            target.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, press));
        }

        [Fact]
        [DisplayName("Add opens the row editor on a new row, and OK keeps the row")]
        public void AddButton_Committed_KeepsNewRow()
        {
            using var scope = new InlineContinuationScope();
            var (host, grid, table) = CreateGrid(lines: 1);

            Click(ToolbarButton(grid, 0));
            var panel = host.Panel<RowEditPanel>();
            var added = panel.Row;
            panel.Commit();

            Assert.Equal(2, table.Rows.Count);
            Assert.Same(added, table.Rows[1]);
            Assert.Empty(host.Overlay.Children);
        }

        [Fact]
        [DisplayName("Add followed by Cancel removes the blank row again")]
        public void AddButton_Cancelled_RemovesNewRow()
        {
            using var scope = new InlineContinuationScope();
            var (host, grid, table) = CreateGrid(lines: 1);

            Click(ToolbarButton(grid, 0));
            host.Panel<RowEditPanel>().Cancel();

            Assert.Single(table.Rows.Cast<DataRow>());
            Assert.Empty(host.Overlay.Children);
        }

        [Fact]
        [DisplayName("Edit opens the row editor on the selected row, and OK keeps the edited value")]
        public void EditButton_WithSelection_EditsSelectedRow()
        {
            using var scope = new InlineContinuationScope();
            var (host, grid, table) = CreateGrid(lines: 2);
            grid.SelectedItem = table.DefaultView[1];

            Click(ToolbarButton(grid, 1));
            var panel = host.Panel<RowEditPanel>();
            Assert.Same(table.Rows[1], panel.Row);
            table.Rows[1]["note"] = "edited";
            panel.Commit();

            Assert.Equal("edited", table.Rows[1]["note"]);
            Assert.Empty(host.Overlay.Children);
        }

        [Fact]
        [DisplayName("Edit without a selected row opens nothing")]
        public void EditButton_NoSelection_OpensNothing()
        {
            using var scope = new InlineContinuationScope();
            var (host, grid, _) = CreateGrid(lines: 1);
            grid.SelectedItem = null;

            Click(ToolbarButton(grid, 1));

            Assert.Empty(host.Overlay.Children);
        }

        [Fact]
        [DisplayName("A double tap on the grid in edit-form mode opens the row editor on the selected row")]
        public void DoubleTap_EditFormMode_OpensRowEditor()
        {
            using var scope = new InlineContinuationScope();
            var (host, grid, table) = CreateGrid(lines: 1);
            grid.SelectedItem = table.DefaultView[0];

            DoubleTap(grid.InnerGrid);

            var panel = host.Panel<RowEditPanel>();
            Assert.Same(table.Rows[0], panel.Row);
            panel.Cancel();
            Assert.Empty(host.Overlay.Children);
        }

        [Fact]
        [DisplayName("A double tap while editing is not allowed opens nothing")]
        public void DoubleTap_EditNotAllowed_OpensNothing()
        {
            using var scope = new InlineContinuationScope();
            var (host, grid, table) = CreateGrid(lines: 1);
            grid.SelectedItem = table.DefaultView[0];
            grid.AllowEdit = false;

            DoubleTap(grid.InnerGrid);

            Assert.Empty(host.Overlay.Children);
        }

        [Fact]
        [DisplayName("Delete removes the selected row")]
        public void DeleteButton_WithSelection_RemovesRow()
        {
            var (_, grid, table) = CreateGrid(lines: 2);
            grid.SelectedItem = table.DefaultView[0];

            Click(ToolbarButton(grid, 2));

            Assert.Single(table.DefaultView.Cast<DataRowView>());
        }
    }
}
