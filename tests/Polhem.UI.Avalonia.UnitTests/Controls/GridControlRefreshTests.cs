using System.ComponentModel;
using System.Data;
using System.Reflection;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Core.DataObjects;
using Polhem.UI.Core.Editing;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    /// <summary>
    /// Re-realizing a <see cref="GridControl"/>'s rows keeps the selected row selected, and adding a row selects it,
    /// because a detail grid has a bounded height and is re-realized after every edit that changes a computed field.
    /// </summary>
    /// <remarks>
    /// The scroll position is restored as well, but restoring it needs a layout pass, which these tests do not run.
    /// </remarks>
    public class GridControlRefreshTests
    {
        private static (GridControl Grid, DataTable Lines, FormDataObject DataObject) BindDetailGrid(int rows)
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var master = schema.Tables!.Add("Order", "Order");
            master.Fields!.Add(new FormField(SysFields.RowId, "Row id", FieldDbType.Guid));
            var detail = schema.Tables!.Add("OrderLine", "Order line");
            detail.Fields!.Add(new FormField(SysFields.RowId, "Row id", FieldDbType.Guid));
            detail.Fields!.Add(new FormField(SysFields.MasterRowId, "Master", FieldDbType.Guid));
            detail.Fields!.Add(new FormField("qty", "Qty", FieldDbType.Integer));

            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var lines = dataObject.DataSet.Tables["OrderLine"]!;
            for (int i = 0; i < rows; i++)
            {
                var row = lines.NewRow();
                row["qty"] = i;
                lines.Rows.Add(row);
            }

            var layout = FormLayoutGenerator.Generate(schema, "default").Details![0];
            var grid = new GridControl { AllowEdit = true, EditMode = GridEditMode.InCell };
            grid.Bind(dataObject, layout);
            return (grid, lines, dataObject);
        }

        [Fact]
        [DisplayName("RefreshRows keeps the selected row selected")]
        public void RefreshRows_SelectedRow_StaysSelected()
        {
            var (grid, lines, _) = BindDetailGrid(5);
            grid.InnerGrid.SelectedItem = lines.DefaultView[3];

            grid.RefreshRows();

            var selected = Assert.IsType<DataRowView>(grid.InnerGrid.SelectedItem);
            Assert.Same(lines.Rows[3], selected.Row);
        }

        [Fact]
        [DisplayName("RefreshRows after another row was deleted keeps the selected row selected")]
        public void RefreshRows_OtherRowDeleted_KeepsSelection()
        {
            var (grid, lines, _) = BindDetailGrid(5);
            grid.InnerGrid.SelectedItem = lines.DefaultView[1];

            lines.Rows[3].Delete();
            grid.RefreshRows();

            var selected = Assert.IsType<DataRowView>(grid.InnerGrid.SelectedItem);
            Assert.Same(lines.Rows[1], selected.Row);
        }

        [Fact]
        [DisplayName("Deleting the selected row leaves nothing selected rather than selecting the deleted row again")]
        public void DeleteSelectedRow_SelectsNothing()
        {
            var (grid, lines, _) = BindDetailGrid(5);
            grid.InnerGrid.SelectedItem = lines.DefaultView[3];

            grid.DeleteSelectedRow();

            Assert.Null(grid.InnerGrid.SelectedItem);
        }

        [Fact]
        [DisplayName("Switching an in-cell grid between View and Edit keeps the selected row selected")]
        public void SetControlState_ViewAndBack_KeepsSelection()
        {
            var (grid, lines, _) = BindDetailGrid(5);
            grid.SetControlState(SingleFormMode.Edit);
            grid.InnerGrid.SelectedItem = lines.DefaultView[2];

            grid.SetControlState(SingleFormMode.View);
            grid.SetControlState(SingleFormMode.Edit);

            var selected = Assert.IsType<DataRowView>(grid.InnerGrid.SelectedItem);
            Assert.Same(lines.Rows[2], selected.Row);
        }

        [Fact]
        [DisplayName("When the data object replaces its data set, as a save or reload does, the row with the same sys_rowid stays selected")]
        public void DataSetReplaced_SelectsRowWithSameRowId()
        {
            var (grid, lines, dataObject) = BindDetailGrid(5);
            grid.InnerGrid.SelectedItem = lines.DefaultView[2];
            var rowId = (Guid)lines.Rows[2][SysFields.RowId];

            // The data object replaces its data set only after a server call; the private method is what those
            // calls end in.
            var replace = typeof(FormDataObject).GetMethod("ReplaceDataSet", BindingFlags.Instance | BindingFlags.NonPublic)!;
            replace.Invoke(dataObject, [dataObject.DataSet.Copy(), true]);

            var selected = Assert.IsType<DataRowView>(grid.InnerGrid.SelectedItem);
            Assert.NotSame(lines.Rows[2], selected.Row);
            Assert.Equal(rowId, (Guid)selected.Row[SysFields.RowId]);
        }

        [Fact]
        [DisplayName("AddRow selects the new row")]
        public void AddRow_SelectsNewRow()
        {
            var (grid, lines, _) = BindDetailGrid(5);

            grid.AddRow();

            var selected = Assert.IsType<DataRowView>(grid.InnerGrid.SelectedItem);
            Assert.Same(lines.Rows[^1], selected.Row);
        }
    }
}
