using System.ComponentModel;
using System.Data;
using System.Reflection;
using Avalonia.Controls;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Additional coverage for <see cref="GridControl"/>: the null paths of BuildCellEditor, and AddRow being a
    /// no-op when DataTable is null.
    /// </summary>
    public class GridControlAdditionalTests
    {
        private static DataTable BuildSimpleTable()
        {
            var table = new DataTable("Items");
            table.Columns.Add("sys_id", typeof(string));
            table.Columns.Add("sys_name", typeof(string));
            table.Rows.Add("E001", "Alice");
            return table;
        }

        [Fact]
        [DisplayName("BuildCellEditor returns a TextBlock when rowView is null")]
        public void BuildCellEditor_NullRowView_ReturnsTextBlock()
        {
            var grid = new GridControl();
            grid.Bind(new LayoutGrid("Items", "Items"), null);

            var method = typeof(GridControl).GetMethod(
                "BuildCellEditor", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var result = method!.Invoke(grid,
                new object?[] { null, new LayoutColumn { FieldName = "name" } });

            Assert.IsType<TextBlock>(result);
        }

        [Fact]
        [DisplayName("BuildCellEditor returns a TextBlock when the field is not in the DataTable")]
        public void BuildCellEditor_FieldMissingFromTable_ReturnsTextBlock()
        {
            var table = BuildSimpleTable();
            var grid = new GridControl();
            grid.Bind(new LayoutGrid("Items", "Items"), table);

            var method = typeof(GridControl).GetMethod(
                "BuildCellEditor", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var result = method!.Invoke(grid,
                new object?[] { table.DefaultView[0], new LayoutColumn { FieldName = "not_a_column" } });

            Assert.IsType<TextBlock>(result);
        }

        [Fact]
        [DisplayName("AddRow is a no-op without throwing when DataTable is null")]
        public void AddRow_NullDataTable_IsNoOp()
        {
            var grid = new GridControl();

            var exception = Record.Exception(grid.AddRow);

            Assert.Null(exception);
        }
    }
}
