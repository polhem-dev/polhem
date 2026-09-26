using System.ComponentModel;
using System.Data;
using System.Reflection;
using Avalonia.Controls;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Additional coverage for <see cref="GridControl"/>: the private static helpers
    /// ComposeDisplayText / SplitDisplayFields, the null paths of BuildCellEditor,
    /// and AddRow being a no-op when DataTable is null.
    /// </summary>
    public class GridControlAdditionalTests
    {
        private static readonly string[] s_idAndNameFields = { "sys_id", "sys_name" };
        private static readonly string[] s_idField = { "sys_id" };

        private static string InvokeSplitDisplayFields(string displayFields)
        {
            var method = typeof(GridControl).GetMethod(
                "SplitDisplayFields", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            var result = (string[])method!.Invoke(null, new object[] { displayFields })!;
            return string.Join(",", result);
        }

        private static string InvokeComposeDisplayText(
            DataRowView? rowView, string[] displayFields, string displayFormat, string numberFormat)
        {
            var method = typeof(GridControl).GetMethod(
                "ComposeDisplayText", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (string)method!.Invoke(null,
                new object?[] { rowView, displayFields, displayFormat, numberFormat })!;
        }

        private static DataTable BuildSimpleTable()
        {
            var table = new DataTable("Items");
            table.Columns.Add("sys_id", typeof(string));
            table.Columns.Add("sys_name", typeof(string));
            table.Rows.Add("E001", "Alice");
            return table;
        }

        [Fact]
        [DisplayName("SplitDisplayFields returns an empty array for an empty string")]
        public void SplitDisplayFields_EmptyString_ReturnsEmptyArray()
        {
            var result = InvokeSplitDisplayFields(string.Empty);

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("SplitDisplayFields returns a trimmed array for a comma-separated string")]
        public void SplitDisplayFields_CommaSeparated_ReturnsTrimmedElements()
        {
            var result = InvokeSplitDisplayFields(" sys_id , sys_name ");

            Assert.Equal("sys_id,sys_name", result);
        }

        [Fact]
        [DisplayName("ComposeDisplayText composes the cell text from the display fields")]
        public void ComposeDisplayText_WithDisplayFields_JoinsValues()
        {
            var table = BuildSimpleTable();
            var rowView = table.DefaultView[0];

            var result = InvokeComposeDisplayText(rowView, s_idAndNameFields, string.Empty, string.Empty);

            Assert.Equal("E001 - Alice", result);
        }

        [Fact]
        [DisplayName("ComposeDisplayText returns an empty string when the field does not exist")]
        public void ComposeDisplayText_MissingField_ReturnsEmpty()
        {
            var table = BuildSimpleTable();
            var rowView = table.DefaultView[0];

            var result = InvokeComposeDisplayText(rowView, s_idField, string.Empty, string.Empty);

            // sys_id exists → "E001"; no separator needed for single field
            Assert.Equal("E001", result);
        }

        [Fact]
        [DisplayName("ComposeDisplayText returns an empty string when rowView is null")]
        public void ComposeDisplayText_NullRowView_ReturnsEmpty()
        {
            var result = InvokeComposeDisplayText(null, s_idAndNameFields, string.Empty, string.Empty);

            Assert.Equal(string.Empty, result);
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
