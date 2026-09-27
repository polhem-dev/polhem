using Polhem.Api.Client;
using System.ComponentModel;
using System.Data;
using System.Reflection;
using Avalonia.Controls;
using Polhem.Definition;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;
using Polhem.Tests.Shared;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Additional coverage for <see cref="GridControl"/>: the per-type paths of FormatCell,
    /// the success and exception paths of TryConvertCellValue, each column state for TryGetRowId,
    /// SetControlState toggling AllowEdit by layout mode, AddRow,
    /// BuildCellEditor for CheckEdit/DateEdit, and the read-only path of BuildInteractiveCell.
    /// </summary>
    public class GridControlFormatTests
    {
        private static string InvokeFormatCell(
            DataRowView? rowView, string fieldName, string displayFormat, string numberFormat)
        {
            var method = typeof(GridControl).GetMethod(
                "FormatCell", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (string)method!.Invoke(null,
                new object?[] { rowView, fieldName, displayFormat, numberFormat })!;
        }

        private static bool InvokeTryConvertCellValue(string? value, DataColumn column)
        {
            var method = typeof(GridControl).GetMethod(
                "TryConvertCellValue", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            var args = new object?[] { value, column, null };
            return (bool)method!.Invoke(null, args)!;
        }

        // The grid reads the selected row's id through the helper it shares with the Blazor grid.
        private static bool InvokeTryGetRowId(DataRow row, out Guid rowId)
            => FormDataGuard.TryGetRowId(row, out rowId);

        private static Control InvokeBuildCellEditor(GridControl grid, DataRowView? rowView, LayoutColumn column)
        {
            var method = typeof(GridControl).GetMethod(
                "BuildCellEditor", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            return (Control)method!.Invoke(grid, new object?[] { rowView, column })!;
        }

        private static Control InvokeBuildInteractiveCell(GridControl grid, DataRowView? rowView, LayoutColumn column)
        {
            var method = typeof(GridControl).GetMethod(
                "BuildInteractiveCell", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            return (Control)method!.Invoke(grid, new object?[] { rowView, column })!;
        }

        private static (GridControl grid, DataTable table) BindSimpleGrid(string colName, Type colType)
        {
            var table = new DataTable("T");
            table.Columns.Add(colName, colType);
            var layout = new LayoutGrid("T", "T");
            layout.Columns!.Add(new LayoutColumn { FieldName = colName, Caption = colName, Visible = true });
            var grid = new GridControl();
            grid.Bind(layout, table);
            return (grid, table);
        }

        [Fact]
        [DisplayName("FormatCell returns an empty string when rowView is null")]
        public void FormatCell_NullRowView_ReturnsEmptyString()
        {
            var result = InvokeFormatCell(null, "col", string.Empty, string.Empty);

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("FormatCell returns an empty string when the field does not exist")]
        public void FormatCell_MissingColumn_ReturnsEmptyString()
        {
            var table = new DataTable("T");
            table.Columns.Add("name", typeof(string));
            table.Rows.Add("Alice");

            var result = InvokeFormatCell(table.DefaultView[0], "nonexistent", string.Empty, string.Empty);

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("FormatCell returns an empty string when the value is DBNull")]
        public void FormatCell_DBNullValue_ReturnsEmptyString()
        {
            var table = new DataTable("T");
            table.Columns.Add("col", typeof(string));
            table.Rows.Add(DBNull.Value);

            var result = InvokeFormatCell(table.DefaultView[0], "col", string.Empty, string.Empty);

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("FormatCell formats a DateTime with a time part as the culture's general date and time")]
        public void FormatCell_DateTimeWithTime_FormatsWithTimePart()
        {
            using var culture = new CultureScope("de-DE");
            var dt = new DateTime(2026, 1, 15, 14, 30, 0, DateTimeKind.Unspecified);
            var table = new DataTable("T");
            table.Columns.Add("ts", typeof(DateTime));
            table.Rows.Add(dt);

            var result = InvokeFormatCell(table.DefaultView[0], "ts", string.Empty, string.Empty);

            Assert.Equal("15.01.2026 14:30:00", result);
        }

        [Fact]
        [DisplayName("FormatCell formats a DateTime without a time part as the culture's short date")]
        public void FormatCell_DateTimeWithNoTime_FormatsDateOnly()
        {
            using var culture = new CultureScope("en-US");
            var dt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var table = new DataTable("T");
            table.Columns.Add("d", typeof(DateTime));
            table.Rows.Add(dt);

            var result = InvokeFormatCell(table.DefaultView[0], "d", string.Empty, string.Empty);

            Assert.Equal("6/1/2026", result);
        }

        [Fact]
        [DisplayName("FormatCell applies displayFormat when it is given")]
        public void FormatCell_WithDisplayFormat_AppliesDisplayFormat()
        {
            var table = new DataTable("T");
            table.Columns.Add("amount", typeof(decimal));
            table.Rows.Add(100.5m);

            var result = InvokeFormatCell(table.DefaultView[0], "amount", "F2", string.Empty);

            Assert.Equal("100.50", result);
        }

        [Fact]
        [DisplayName("FormatCell applies numberFormat when it is given")]
        public void FormatCell_WithNumberFormat_AppliesNumberFormat()
        {
            var table = new DataTable("T");
            table.Columns.Add("qty", typeof(int));
            table.Rows.Add(42);

            var result = InvokeFormatCell(table.DefaultView[0], "qty", string.Empty, "D5");

            Assert.Equal("00042", result);
        }

        [Fact]
        [DisplayName("TryConvertCellValue returns true for a valid integer string")]
        public void TryConvertCellValue_ValidIntString_ReturnsTrue()
        {
            var column = new DataColumn("qty", typeof(int));

            var ok = InvokeTryConvertCellValue("5", column);

            Assert.True(ok);
        }

        [Fact]
        [DisplayName("TryConvertCellValue returns false without throwing for an unparsable string")]
        public void TryConvertCellValue_InvalidString_ReturnsFalse()
        {
            var column = new DataColumn("qty", typeof(int));

            var ok = InvokeTryConvertCellValue("not-a-number", column);

            Assert.False(ok);
        }

        [Fact]
        [DisplayName("TryGetRowId returns false when there is no sys_rowid column")]
        public void TryGetRowId_NoRowIdColumn_ReturnsFalse()
        {
            var table = new DataTable("T");
            table.Columns.Add("name", typeof(string));
            table.Rows.Add("Alice");

            var result = InvokeTryGetRowId(table.Rows[0], out _);

            Assert.False(result);
        }

        [Fact]
        [DisplayName("TryGetRowId returns false when sys_rowid is DBNull")]
        public void TryGetRowId_DBNullValue_ReturnsFalse()
        {
            var table = new DataTable("T");
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Rows.Add(DBNull.Value);

            var result = InvokeTryGetRowId(table.Rows[0], out _);

            Assert.False(result);
        }

        [Fact]
        [DisplayName("TryGetRowId returns true and outputs the Guid when sys_rowid is of type Guid")]
        public void TryGetRowId_GuidValue_ReturnsTrueWithCorrectGuid()
        {
            var expected = Guid.NewGuid();
            var table = new DataTable("T");
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Rows.Add(expected);

            var result = InvokeTryGetRowId(table.Rows[0], out var rowId);

            Assert.True(result);
            Assert.Equal(expected, rowId);
        }

        [Fact]
        [DisplayName("TryGetRowId returns true and outputs the matching Guid when sys_rowid is a parsable string")]
        public void TryGetRowId_StringGuidValue_ReturnsTrueWithCorrectGuid()
        {
            var expected = Guid.NewGuid();
            var table = new DataTable("T");
            table.Columns.Add(SysFields.RowId, typeof(string));
            table.Rows.Add(expected.ToString());

            var result = InvokeTryGetRowId(table.Rows[0], out var rowId);

            Assert.True(result);
            Assert.Equal(expected, rowId);
        }

        [Fact]
        [DisplayName("SetControlState in View mode sets AllowEdit to false")]
        public void SetControlState_ViewMode_SetsAllowEditFalse()
        {
            var layout = new LayoutGrid("T", "T");
            layout.AllowEditModes = FormEditModes.All;
            layout.Columns!.Add(new LayoutColumn { FieldName = "name", Caption = "Name", Visible = true });
            var grid = new GridControl();
            grid.Bind(layout, null);
            grid.AllowEdit = true;

            grid.SetControlState(SingleFormMode.View);

            Assert.False(grid.AllowEdit);
        }

        [Fact]
        [DisplayName("SetControlState in Edit mode with a layout allowing All edit modes sets AllowEdit to true")]
        public void SetControlState_EditModeWithAllowingLayout_SetsAllowEditTrue()
        {
            var layout = new LayoutGrid("T", "T");
            layout.AllowEditModes = FormEditModes.All;
            layout.Columns!.Add(new LayoutColumn { FieldName = "name", Caption = "Name", Visible = true });
            var grid = new GridControl();
            grid.Bind(layout, null);

            grid.SetControlState(SingleFormMode.Edit);

            Assert.True(grid.AllowEdit);
        }

        [Fact]
        [DisplayName("AddRow adds a row when there is a DataTable")]
        public void AddRow_WithNonNullableColumn_AddsRow()
        {
            var (grid, table) = BindSimpleGrid("name", typeof(string));
            table.Columns["name"]!.AllowDBNull = false;

            grid.AddRow();

            Assert.Equal(1, table.Rows.Count);
        }

        [Fact]
        [DisplayName("BuildCellEditor returns a CheckBox for a CheckEdit field")]
        public void BuildCellEditor_CheckEdit_ReturnsCheckBox()
        {
            var (grid, table) = BindSimpleGrid("active", typeof(bool));
            table.Rows.Add(true);
            var rowView = table.DefaultView[0];

            var result = InvokeBuildCellEditor(grid, rowView,
                new LayoutColumn("active", "Active", ControlType.CheckEdit));

            Assert.IsType<CheckBox>(result);
        }

        [Fact]
        [DisplayName("BuildCellEditor returns a DatePicker for a DateEdit field")]
        public void BuildCellEditor_DateEdit_ReturnsDatePicker()
        {
            var (grid, table) = BindSimpleGrid("hire_date", typeof(DateTime));
            table.Rows.Add(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified));
            var rowView = table.DefaultView[0];

            var result = InvokeBuildCellEditor(grid, rowView,
                new LayoutColumn("hire_date", "Date", ControlType.DateEdit));

            Assert.IsType<DatePicker>(result);
        }

        [Fact]
        [DisplayName("BuildInteractiveCell returns a disabled CheckBox for a read-only CheckEdit")]
        public void BuildInteractiveCell_CheckEditReadOnly_ReturnsDisabledCheckBox()
        {
            var (grid, table) = BindSimpleGrid("active", typeof(bool));
            table.Rows.Add(false);
            var rowView = table.DefaultView[0];

            var result = InvokeBuildInteractiveCell(grid, rowView,
                new LayoutColumn("active", "Active", ControlType.CheckEdit));

            var checkBox = Assert.IsType<CheckBox>(result);
            Assert.False(checkBox.IsEnabled);
        }

        [Fact]
        [DisplayName("BuildInteractiveCell returns a TextBlock for a read-only non-CheckEdit field")]
        public void BuildInteractiveCell_DateEditReadOnly_ReturnsTextBlock()
        {
            var (grid, table) = BindSimpleGrid("hire_date", typeof(DateTime));
            table.Rows.Add(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified));
            var rowView = table.DefaultView[0];

            var result = InvokeBuildInteractiveCell(grid, rowView,
                new LayoutColumn("hire_date", "Date", ControlType.DateEdit));

            Assert.IsType<TextBlock>(result);
        }
    }
}
