using System.ComponentModel;
using System.Data;
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
        private static (GridControl Grid, DataTable Lines) BindDetailGrid(int rows)
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
            return (grid, lines);
        }

        [Fact]
        [DisplayName("RefreshRows keeps the selected row selected")]
        public void RefreshRows_SelectedRow_StaysSelected()
        {
            var (grid, lines) = BindDetailGrid(5);
            grid.InnerGrid.SelectedItem = lines.DefaultView[3];

            grid.RefreshRows();

            var selected = Assert.IsType<DataRowView>(grid.InnerGrid.SelectedItem);
            Assert.Same(lines.Rows[3], selected.Row);
        }

        [Fact]
        [DisplayName("RefreshRows after the selected row was deleted leaves nothing selected")]
        public void RefreshRows_SelectedRowDeleted_SelectsNothing()
        {
            var (grid, lines) = BindDetailGrid(5);
            grid.InnerGrid.SelectedItem = lines.DefaultView[3];

            grid.DeleteSelectedRow();

            Assert.Null(grid.InnerGrid.SelectedItem);
        }

        [Fact]
        [DisplayName("AddRow selects the new row")]
        public void AddRow_SelectsNewRow()
        {
            var (grid, lines) = BindDetailGrid(5);

            grid.AddRow();

            var selected = Assert.IsType<DataRowView>(grid.InnerGrid.SelectedItem);
            Assert.Same(lines.Rows[^1], selected.Row);
        }
    }
}
