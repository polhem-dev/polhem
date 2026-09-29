using System.ComponentModel;
using System.Data;
using System.Reflection;
using Avalonia.Controls;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// GridControl lookup column tests: a lookup column bypasses the DataGrid edit pipeline (the column is read-only),
    /// the cell shows the composed DisplayFields value instead of the Guid, an editable cell is wrapped in a hit-testable host,
    /// and read-only / list mode shows plain text.
    /// </summary>
    public class GridControlLookupTests
    {
        private static FormSchema BuildOrderSchema()
        {
            var schema = new FormSchema("Order", "訂單") { CategoryId = "company" };
            var master = schema.Tables!.Add("Order", "訂單");
            master.Fields!.Add(new FormField(SysFields.RowId, "唯一識別", FieldDbType.Guid));
            var detail = schema.Tables!.Add("OrderLine", "訂單明細");
            detail.Fields!.Add(new FormField(SysFields.RowId, "唯一識別", FieldDbType.Guid));
            detail.Fields!.Add(new FormField(SysFields.MasterRowId, "主檔識別", FieldDbType.Guid));
            var productField = new FormField("product_rowid", "商品", FieldDbType.Guid)
            {
                RelationProgId = "Product",
            };
            productField.RelationFieldMappings!.Add(SysFields.Name, "ref_product_name");
            detail.Fields!.Add(productField);
            detail.Fields!.Add(new FormField("ref_product_name", "商品名稱", FieldDbType.String, FieldType.RelationField));
            detail.Fields!.Add(new FormField("qty", "數量", FieldDbType.Integer));
            return schema;
        }

        private static (GridControl grid, FormDataObject dataObject, DataRow line) BindDetailGrid()
        {
            var schema = BuildOrderSchema();
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var lineTable = dataObject.DataSet.Tables["OrderLine"]!;
            var line = lineTable.NewRow();
            line["ref_product_name"] = "商品甲";
            lineTable.Rows.Add(line);

            var layout = FormLayoutGenerator.Generate(schema, "default").Details![0];
            var grid = new GridControl { AllowEdit = true, EditMode = GridEditMode.InCell };
            grid.Bind(dataObject, layout);
            return (grid, dataObject, line);
        }

        [Fact]
        [DisplayName("The DataGrid column of a lookup field is read-only (bypasses the edit pipeline)")]
        public void Bind_LookupColumn_BypassesEditPipeline()
        {
            var (grid, _, _) = BindDetailGrid();

            var lookupColumn = grid.InnerGrid.Columns
                .OfType<DataGridTemplateColumn>()
                .First(c => Equals(c.Header, "商品"));

            Assert.True(lookupColumn.IsReadOnly);
            // A non-lookup column is unaffected: `qty` still goes through the standard edit pipeline.
            var qtyColumn = grid.InnerGrid.Columns
                .OfType<DataGridTemplateColumn>()
                .First(c => Equals(c.Header, "數量"));
            Assert.False(qtyColumn.IsReadOnly);
        }

        [Fact]
        [DisplayName("An editable lookup cell is a hit-testable host showing the composed DisplayFields value with a lookup icon")]
        public void BuildLookupCell_Editable_WrapsTextInHost()
        {
            var (grid, dataObject, line) = BindDetailGrid();
            var field = dataObject.GetFormField("OrderLine", "product_rowid")!;
            var rowView = line.Table.DefaultView[0];
            var column = grid.Layout!.Columns!.First(c => c.FieldName == "product_rowid");

            var cell = InvokeBuildLookupCell(grid, rowView, column, field);

            var host = Assert.IsType<Border>(cell);
            Assert.NotNull(host.Background);
            // Editable state: the text on the left and the lookup icon on the right (a `DockPanel` container).
            var content = Assert.IsType<DockPanel>(host.Child);
            var text = content.Children.OfType<TextBlock>().Single();
            Assert.Equal("商品甲", text.Text);
            var icon = content.Children.OfType<PathIcon>().Single();
            Assert.Equal(Dock.Right, DockPanel.GetDock(icon));
        }

        [Fact]
        [DisplayName("A lookup cell on a read-only grid is plain text showing the DisplayField")]
        public void BuildLookupCell_ReadOnlyGrid_PlainText()
        {
            var (grid, dataObject, line) = BindDetailGrid();
            var field = dataObject.GetFormField("OrderLine", "product_rowid")!;
            var rowView = line.Table.DefaultView[0];
            var column = grid.Layout!.Columns!.First(c => c.FieldName == "product_rowid");

            // A read-only grid (View mode and list mode take the same path).
            grid.AllowEdit = false;

            var cell = InvokeBuildLookupCell(grid, rowView, column, field);

            var text = Assert.IsType<TextBlock>(cell);
            Assert.Equal("商品甲", text.Text);
        }

        [Fact]
        [DisplayName("In list mode, a text cell of a ButtonEdit column bound with Bind(layout, rows) uses DisplayFields")]
        public void ListMode_ButtonEditColumn_UsesDisplayFieldText()
        {
            // In list mode there is no data object, so lookup detection returns null and the text branch runs.
            // The text cell must still use `DisplayFields` rather than the rowid.
            var schema = BuildOrderSchema();
            var layout = FormLayoutGenerator.Generate(schema, "default").Details![0];
            var rows = new DataTable("OrderLine");
            rows.Columns.Add(SysFields.RowId, typeof(Guid));
            rows.Columns.Add("product_rowid", typeof(Guid));
            rows.Columns.Add("ref_product_name", typeof(string));
            rows.Columns.Add("qty", typeof(int));
            rows.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), "商品乙", 3);

            var grid = new GridControl();
            grid.Bind(layout, rows);

            // Build a real cell through the column's `CellTemplate` to check where the text comes from.
            var column = grid.InnerGrid.Columns
                .OfType<DataGridTemplateColumn>()
                .First(c => Equals(c.Header, "商品"));
            var cell = column.CellTemplate!.Build(rows.DefaultView[0]);

            var text = Assert.IsType<TextBlock>(cell);
            Assert.Equal("商品乙", text.Text);
        }

        private static Control InvokeBuildLookupCell(
            GridControl grid, DataRowView rowView, LayoutColumn column, FormField field)
        {
            var method = typeof(GridControl).GetMethod(
                "BuildLookupCell", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (Control)method.Invoke(grid, new object?[] { rowView, column, field })!;
        }
    }
}
