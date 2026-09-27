using System.ComponentModel;
using System.Data;
using System.Reflection;
using Avalonia.Controls;
using Polhem.Base.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Additional coverage for <see cref="GridControl"/>: RefreshFromDataObject without a DataObject,
    /// the public Unbind method, RefreshRows, DeleteSelectedRow without a selection,
    /// and BuildCellEditor falling back to a TextBox for a DropDownEdit without list items.
    /// </summary>
    public class GridControlCoverageTests
    {
        private static FormDataObject BuildDataObjectWithDetail()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add("emp_name", "Name", FieldDbType.String);
            var detail = schema.Tables.Add("EmployeePhone", "Phones");
            detail.Fields!.Add("phone", "Phone", FieldDbType.String);
            detail.Fields.Add("status", "Status", FieldDbType.String);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        private static void InvokeRefreshFromDataObject(GridControl grid)
        {
            var method = typeof(GridControl).GetMethod(
                "RefreshFromDataObject", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            method!.Invoke(grid, null);
        }

        [Fact]
        [DisplayName("RefreshFromDataObject is a no-op without a DataObject, and DataTable stays null")]
        public void RefreshFromDataObject_NullDataObject_IsNoOp()
        {
            var grid = new GridControl();

            var exception = Record.Exception(() => InvokeRefreshFromDataObject(grid));

            Assert.Null(exception);
            Assert.Null(grid.DataTable);
        }

        [Fact]
        [DisplayName("Unbind does not throw when nothing is bound")]
        public void Unbind_WhenNotBound_DoesNotThrow()
        {
            var grid = new GridControl();

            var exception = Record.Exception(grid.Unbind);

            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("Unbind releases a detail binding, so a later DataSetReplaced no longer updates the table")]
        public async Task Unbind_AfterExplicitBind_StopsRefreshOnDataSetReplaced()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add("emp_name", "Name", FieldDbType.String);
            var detail = schema.Tables.Add("EmployeePhone", "Phones");
            detail.Fields!.Add("phone", "Phone", FieldDbType.String);

            var refreshed = new DataSet("Employee");
            refreshed.Tables.Add(new DataTable("Employee"));
            var refreshedDetail = new DataTable("EmployeePhone");
            refreshedDetail.Columns.Add("phone", typeof(string));
            refreshedDetail.Rows.Add("new-phone");
            refreshed.Tables.Add(refreshedDetail);

            var connector = new FakeFormApiConnector
            {
                GetNewDataHandler = () => new Polhem.Api.Core.Messages.Form.GetNewDataResponse { DataSet = refreshed },
            };
            var dataObject = new FormDataObject(schema, connector);

            var layout = new LayoutGrid("EmployeePhone", "Phones");
            layout.Columns!.Add(new LayoutColumn { FieldName = "phone", Caption = "Phone", Visible = true });

            var grid = new GridControl();
            grid.Bind(dataObject, layout);
            var tableBeforeUnbind = grid.DataTable;

            grid.Unbind();
            await dataObject.NewAsync();

            Assert.Same(tableBeforeUnbind, grid.DataTable);
        }

        [Fact]
        [DisplayName("RefreshRows clears ItemsSource without throwing when DataTable is null")]
        public void RefreshRows_NullDataTable_ClearsItemsSourceSafely()
        {
            var layout = new LayoutGrid("Items", "Items");
            layout.Columns!.Add(new LayoutColumn { FieldName = "name", Caption = "Name", Visible = true });
            var grid = new GridControl();
            grid.Bind(layout, null);

            var exception = Record.Exception(grid.RefreshRows);

            Assert.Null(exception);
            Assert.Null(grid.InnerGrid.ItemsSource);
        }

        [Fact]
        [DisplayName("DeleteSelectedRow is a no-op without a selected row, and the row count is unchanged")]
        public void DeleteSelectedRow_NothingSelected_IsNoOp()
        {
            var table = new DataTable("Items");
            table.Columns.Add("name", typeof(string));
            table.Rows.Add("Widget");
            var layout = new LayoutGrid("Items", "Items");
            layout.Columns!.Add(new LayoutColumn { FieldName = "name", Caption = "Name", Visible = true });

            var grid = new GridControl();
            grid.Bind(layout, table);

            var exception = Record.Exception(grid.DeleteSelectedRow);

            Assert.Null(exception);
            Assert.Equal(1, table.Rows.Count);
        }

        [Fact]
        [DisplayName("BuildCellEditor returns a TextBox for a DropDownEdit field without list items")]
        public void BuildCellEditor_DropDownEditWithoutListItems_ReturnsTextBox()
        {
            var dataObject = BuildDataObjectWithDetail();
            var layout = new LayoutGrid("EmployeePhone", "Phones");
            layout.Columns!.Add(new LayoutColumn("status", "Status", ControlType.DropDownEdit));

            var grid = new GridControl();
            grid.Bind(dataObject, layout);
            grid.DataTable!.Rows.Add("02-1234", "active");
            var rowView = grid.DataTable.DefaultView[0];

            var method = typeof(GridControl).GetMethod(
                "BuildCellEditor", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var result = method!.Invoke(
                grid,
                new object?[] { rowView, new LayoutColumn("status", "Status", ControlType.DropDownEdit) });

            Assert.IsType<TextBox>(result);
        }

        private sealed class FakeFormApiConnector : Polhem.Api.Client.Connectors.FormApiConnector
        {
            public FakeFormApiConnector() : base(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid(), "Employee") { }

            public Func<Polhem.Api.Core.Messages.Form.GetNewDataResponse>? GetNewDataHandler { get; set; }

            public override Task<Polhem.Api.Core.Messages.Form.GetNewDataResponse> GetNewDataAsync(CancellationToken cancellationToken = default)
                => Task.FromResult((GetNewDataHandler ?? (() => new Polhem.Api.Core.Messages.Form.GetNewDataResponse()))());
        }
    }
}
