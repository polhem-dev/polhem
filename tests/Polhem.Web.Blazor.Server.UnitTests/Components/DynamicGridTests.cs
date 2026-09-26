using System.ComponentModel;
using System.Data;
using System.Reflection;
using Polhem.Definition;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.Components;
using Microsoft.AspNetCore.Components;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Structural and pure-logic tests covering the static helper methods of <see cref="DynamicGrid"/>
    /// (<c>TryGetRowId</c>, <c>FormatCell</c>, <c>BuildColumnStyle</c>) and
    /// the private computed property <c>VisibleColumns</c>.
    /// Render cycle tests that need the Blazor renderer are left to bUnit integration tests.
    /// </summary>
    public class DynamicGridTests
    {
        private static (bool Success, Guid RowId) InvokeTryGetRowId(DataRow row)
        {
            var method = typeof(DynamicGrid).GetMethod(
                "TryGetRowId", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            var args = new object?[] { row, Guid.Empty };
            var success = (bool)method!.Invoke(null, args)!;
            return (success, (Guid)args[1]!);
        }

        private static string InvokeFormatCell(DataRow row, LayoutColumn column)
        {
            var method = typeof(DynamicGrid).GetMethod(
                "FormatCell", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (string)method!.Invoke(null, new object[] { row, column })!;
        }

        private static string InvokeBuildColumnStyle(LayoutColumn column)
        {
            var method = typeof(DynamicGrid).GetMethod(
                "BuildColumnStyle", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (string)method!.Invoke(null, new object[] { column })!;
        }

        // ──────────────────────────────────────────────────────────────
        // TryGetRowId
        // ──────────────────────────────────────────────────────────────

        [Fact]
        [DisplayName("TryGetRowId returns false when the table has no sys_rowid column")]
        public void TryGetRowId_NoRowIdColumn_ReturnsFalse()
        {
            var table = new DataTable();
            table.Columns.Add("other_col", typeof(string));
            var row = table.NewRow();
            row["other_col"] = "value";
            table.Rows.Add(row);
            var (success, _) = InvokeTryGetRowId(row);
            Assert.False(success);
        }

        [Fact]
        [DisplayName("TryGetRowId returns false when the sys_rowid value is DBNull")]
        public void TryGetRowId_DbNullValue_ReturnsFalse()
        {
            var table = new DataTable();
            table.Columns.Add(SysFields.RowId, typeof(object));
            var row = table.NewRow();
            row[SysFields.RowId] = DBNull.Value;
            table.Rows.Add(row);
            var (success, _) = InvokeTryGetRowId(row);
            Assert.False(success);
        }

        [Fact]
        [DisplayName("TryGetRowId returns true and outputs the value when the sys_rowid column is of type Guid")]
        public void TryGetRowId_GuidValue_ReturnsTrueAndOutputsGuid()
        {
            var expected = Guid.NewGuid();
            var table = new DataTable();
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            var row = table.NewRow();
            row[SysFields.RowId] = expected;
            table.Rows.Add(row);
            var (success, actual) = InvokeTryGetRowId(row);
            Assert.True(success);
            Assert.Equal(expected, actual);
        }

        [Fact]
        [DisplayName("TryGetRowId returns true and outputs the value when sys_rowid is a valid Guid string")]
        public void TryGetRowId_ValidGuidString_ReturnsTrueAndOutputsGuid()
        {
            var expected = Guid.NewGuid();
            var table = new DataTable();
            table.Columns.Add(SysFields.RowId, typeof(string));
            var row = table.NewRow();
            row[SysFields.RowId] = expected.ToString();
            table.Rows.Add(row);
            var (success, actual) = InvokeTryGetRowId(row);
            Assert.True(success);
            Assert.Equal(expected, actual);
        }

        [Fact]
        [DisplayName("TryGetRowId returns false when sys_rowid is an invalid Guid string")]
        public void TryGetRowId_InvalidGuidString_ReturnsFalse()
        {
            var table = new DataTable();
            table.Columns.Add(SysFields.RowId, typeof(string));
            var row = table.NewRow();
            row[SysFields.RowId] = "not-a-guid";
            table.Rows.Add(row);
            var (success, rowId) = InvokeTryGetRowId(row);
            Assert.False(success);
            Assert.Equal(Guid.Empty, rowId);
        }

        // ──────────────────────────────────────────────────────────────
        // FormatCell
        // ──────────────────────────────────────────────────────────────

        [Fact]
        [DisplayName("FormatCell returns an empty string when the table has no matching column")]
        public void FormatCell_MissingColumn_ReturnsEmpty()
        {
            var table = new DataTable();
            var row = table.NewRow();
            table.Rows.Add(row);
            var column = new LayoutColumn { FieldName = "missing_col" };
            var result = InvokeFormatCell(row, column);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("FormatCell returns an empty string when the value is DBNull")]
        public void FormatCell_DbNullValue_ReturnsEmpty()
        {
            var table = new DataTable();
            table.Columns.Add("name", typeof(string));
            var row = table.NewRow();
            row["name"] = DBNull.Value;
            table.Rows.Add(row);
            var column = new LayoutColumn { FieldName = "name" };
            var result = InvokeFormatCell(row, column);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("FormatCell formats a DateTime without a time part as yyyy-MM-dd")]
        public void FormatCell_DateTimeWithNoTime_ReturnsDateOnly()
        {
            var table = new DataTable();
            table.Columns.Add("hire_date", typeof(DateTime));
            var row = table.NewRow();
            row["hire_date"] = new DateTime(2024, 3, 15, 0, 0, 0, DateTimeKind.Utc);
            table.Rows.Add(row);
            var column = new LayoutColumn { FieldName = "hire_date" };
            var result = InvokeFormatCell(row, column);
            Assert.Equal("2024-03-15", result);
        }

        [Fact]
        [DisplayName("FormatCell formats a DateTime with a time part as yyyy-MM-dd HH:mm:ss")]
        public void FormatCell_DateTimeWithTime_ReturnsDateTimeFormat()
        {
            var table = new DataTable();
            table.Columns.Add("created_at", typeof(DateTime));
            var row = table.NewRow();
            row["created_at"] = new DateTime(2024, 3, 15, 14, 30, 45, DateTimeKind.Utc);
            table.Rows.Add(row);
            var column = new LayoutColumn { FieldName = "created_at" };
            var result = InvokeFormatCell(row, column);
            Assert.Equal("2024-03-15 14:30:45", result);
        }

        [Fact]
        [DisplayName("FormatCell returns the original string value for a string column")]
        public void FormatCell_StringValue_ReturnsRawString()
        {
            var table = new DataTable();
            table.Columns.Add("name", typeof(string));
            var row = table.NewRow();
            row["name"] = "Alice";
            table.Rows.Add(row);
            var column = new LayoutColumn { FieldName = "name" };
            var result = InvokeFormatCell(row, column);
            Assert.Equal("Alice", result);
        }

        [Fact]
        [DisplayName("FormatCell formats the value with DisplayFormat first when it is set")]
        public void FormatCell_WithDisplayFormat_UsesDisplayFormat()
        {
            var table = new DataTable();
            table.Columns.Add("amount", typeof(double));
            var row = table.NewRow();
            row["amount"] = 3.14;
            table.Rows.Add(row);
            var column = new LayoutColumn { FieldName = "amount", DisplayFormat = "F2" };
            var result = InvokeFormatCell(row, column);
            Assert.Equal("3.14", result);
        }

        [Fact]
        [DisplayName("FormatCell formats a number with NumberFormat when it is set")]
        public void FormatCell_WithNumberFormat_UsesNumberFormat()
        {
            var table = new DataTable();
            table.Columns.Add("price", typeof(double));
            var row = table.NewRow();
            row["price"] = 0.5;
            table.Rows.Add(row);
            var column = new LayoutColumn { FieldName = "price", NumberFormat = "F1" };
            var result = InvokeFormatCell(row, column);
            Assert.Equal("0.5", result);
        }

        // ──────────────────────────────────────────────────────────────
        // BuildColumnStyle
        // ──────────────────────────────────────────────────────────────

        [Fact]
        [DisplayName("BuildColumnStyle returns an empty string when Width is 0")]
        public void BuildColumnStyle_ZeroWidth_ReturnsEmpty()
        {
            var column = new LayoutColumn { Width = 0 };
            var result = InvokeBuildColumnStyle(column);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("BuildColumnStyle returns a CSS style string with the width when Width is positive")]
        public void BuildColumnStyle_PositiveWidth_ReturnsWidthStyle()
        {
            var column = new LayoutColumn { Width = 120 };
            var result = InvokeBuildColumnStyle(column);
            Assert.Equal("width:120px", result);
        }

        // ──────────────────────────────────────────────────────────────
        // VisibleColumns (private computed property)
        // ──────────────────────────────────────────────────────────────

        [Fact]
        [DisplayName("VisibleColumns returns an empty sequence when Layout is null")]
        public void VisibleColumns_NullLayout_ReturnsEmpty()
        {
            var component = new DynamicGrid();
            var prop = typeof(DynamicGrid).GetProperty(
                "VisibleColumns", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(prop);
            var result = prop!.GetValue(component) as IEnumerable<LayoutColumn>;
            Assert.NotNull(result);
            Assert.Empty(result!);
        }

        [Fact]
        [DisplayName("VisibleColumns returns only columns whose Visible is true")]
        public void VisibleColumns_MixedVisibility_ReturnsOnlyVisible()
        {
            var component = new DynamicGrid();
            var layout = new LayoutGrid();
            layout.Columns!.Add(new LayoutColumn { FieldName = "col_visible", Visible = true });
            layout.Columns.Add(new LayoutColumn { FieldName = "col_hidden", Visible = false });
            typeof(DynamicGrid)
                .GetProperty("Layout", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(component, layout);
            var prop = typeof(DynamicGrid).GetProperty(
                "VisibleColumns", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(prop);
            var result = prop!.GetValue(component) as IEnumerable<LayoutColumn>;
            Assert.NotNull(result);
            var list = result!.ToList();
            Assert.Single(list);
            Assert.Equal("col_visible", list[0].FieldName);
        }

        // ──────────────────────────────────────────────────────────────
        // Component surface
        // ──────────────────────────────────────────────────────────────

        [Fact]
        [DisplayName("DynamicGrid is a subclass of Blazor ComponentBase")]
        public void Type_IsComponentBaseSubclass()
        {
            Assert.True(typeof(ComponentBase).IsAssignableFrom(typeof(DynamicGrid)));
        }

        [Fact]
        [DisplayName("EmptyText defaults to 'No data.'")]
        public void EmptyText_Default_IsNoData()
        {
            var component = new DynamicGrid();
            Assert.Equal("No data.", component.EmptyText);
        }

        [Theory]
        [InlineData(nameof(DynamicGrid.Layout))]
        [InlineData(nameof(DynamicGrid.Rows))]
        [InlineData(nameof(DynamicGrid.OnRowSelected))]
        [InlineData(nameof(DynamicGrid.EmptyText))]
        [DisplayName("Public properties are all marked with [Parameter]")]
        public void PublicProperties_AreMarkedAsParameters(string name)
        {
            var property = typeof(DynamicGrid).GetProperty(
                name, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property);
            Assert.NotNull(property!.GetCustomAttribute<ParameterAttribute>());
        }

        // ──────────────────────────────────────────────────────────────
        // OnRowClickAsync
        // ──────────────────────────────────────────────────────────────

        private static Task InvokeOnRowClickAsync(DynamicGrid component, DataRow row)
        {
            var method = typeof(DynamicGrid).GetMethod(
                "OnRowClickAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            return (Task)method!.Invoke(component, new object[] { row })!;
        }

        [Fact]
        [DisplayName("OnRowClickAsync returns without throwing when OnRowSelected has no delegate")]
        public async Task OnRowClickAsync_NoDelegate_ReturnsWithoutError()
        {
            var component = new DynamicGrid();
            var table = new DataTable();
            table.Columns.Add("col", typeof(string));
            var row = table.NewRow();
            row["col"] = "value";
            table.Rows.Add(row);
            var exception = await Record.ExceptionAsync(() => InvokeOnRowClickAsync(component, row));
            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("OnRowClickAsync does not invoke the callback when the row has no sys_rowid column, even with a delegate")]
        public async Task OnRowClickAsync_HasDelegateButNoRowId_DoesNotInvokeCallback()
        {
            var invoked = false;
            var component = new DynamicGrid();
            typeof(DynamicGrid)
                .GetProperty("OnRowSelected", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(component, EventCallback.Factory.Create<Guid>(
                    new SyncEventHandler(),
                    (Guid _) => { invoked = true; }));
            var table = new DataTable();
            table.Columns.Add("name", typeof(string));
            var row = table.NewRow();
            row["name"] = "test";
            table.Rows.Add(row);
            await InvokeOnRowClickAsync(component, row);
            Assert.False(invoked);
        }

        [Fact]
        [DisplayName("OnRowClickAsync invokes the callback with the correct Guid when the row has a valid sys_rowid and a delegate")]
        public async Task OnRowClickAsync_ValidRowWithDelegate_InvokesCallbackWithRowId()
        {
            var expectedGuid = Guid.NewGuid();
            var capturedGuid = Guid.Empty;
            var component = new DynamicGrid();
            typeof(DynamicGrid)
                .GetProperty("OnRowSelected", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(component, EventCallback.Factory.Create<Guid>(
                    new SyncEventHandler(),
                    (Guid g) => { capturedGuid = g; }));
            var table = new DataTable();
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            var row = table.NewRow();
            row[SysFields.RowId] = expectedGuid;
            table.Rows.Add(row);
            await InvokeOnRowClickAsync(component, row);
            Assert.Equal(expectedGuid, capturedGuid);
        }

        // ──────────────────────────────────────────────────────────────
        // FormatCell — IFormattable switch arm
        // ──────────────────────────────────────────────────────────────

        [Fact]
        [DisplayName("FormatCell formats an integer without format settings through IFormattable with InvariantCulture")]
        public void FormatCell_IntegerWithNoFormat_UsesIFormattableToString()
        {
            var table = new DataTable();
            table.Columns.Add("count", typeof(int));
            var row = table.NewRow();
            row["count"] = 42;
            table.Rows.Add(row);
            var column = new LayoutColumn { FieldName = "count" };
            var result = InvokeFormatCell(row, column);
            Assert.Equal("42", result);
        }

        private sealed class SyncEventHandler : IHandleEvent
        {
            public Task HandleEventAsync(EventCallbackWorkItem callback, object? arg)
                => callback.InvokeAsync(arg);
        }
    }
}
