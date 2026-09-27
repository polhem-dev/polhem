using System.ComponentModel;
using System.Data;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.Form;
using Polhem.Base.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.UI.Avalonia.Controls.Editors;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// LookupPanel unit tests: layout binding, ReloadAsync fetching through a stub connector and
    /// passing SearchText, the commit / cancel selection events, and a failed load not throwing.
    /// </summary>
    public class LookupPanelTests
    {
        private static FormSchema BuildCustomerSchema()
        {
            var schema = new FormSchema("Customer", "客戶") { CategoryId = "company" };
            var table = schema.Tables!.Add("Customer", "客戶");
            table.Fields!.Add(new FormField(SysFields.RowId, "唯一識別", FieldDbType.Guid));
            table.Fields!.Add(new FormField(SysFields.Id, "客戶代碼", FieldDbType.String));
            table.Fields!.Add(new FormField(SysFields.Name, "客戶名稱", FieldDbType.String));
            return schema;
        }

        private static DataTable BuildLookupTable(params (Guid rowId, string id, string name)[] rows)
        {
            var table = new DataTable("Customer");
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Columns.Add(SysFields.Id, typeof(string));
            table.Columns.Add(SysFields.Name, typeof(string));
            foreach (var (rowId, id, name) in rows)
                table.Rows.Add(rowId, id, name);
            return table;
        }

        [Fact]
        [DisplayName("Bind builds the grid columns from GetLookupLayout (including the hidden sys_rowid)")]
        public void Bind_BuildsLookupLayoutColumns()
        {
            var panel = new LookupPanel();

            panel.Bind(BuildCustomerSchema(), new StubConnector(BuildLookupTable()));

            Assert.NotNull(panel.Grid.Layout);
            Assert.Equal(3, panel.Grid.Layout!.Columns!.Count);
            Assert.Equal(SysFields.Id, panel.Grid.Layout.Columns[0].FieldName);
            Assert.False(panel.Grid.Layout.Columns[2].Visible);
        }

        [Fact]
        [DisplayName("ReloadAsync passes SearchText and fills the grid from the response")]
        public async Task ReloadAsync_PassesSearchTextAndPopulatesGrid()
        {
            var table = BuildLookupTable(
                (Guid.NewGuid(), "C001", "客戶甲"),
                (Guid.NewGuid(), "C002", "客戶乙"));
            var connector = new StubConnector(table);
            var panel = new LookupPanel();
            panel.Bind(BuildCustomerSchema(), connector);
            panel.SearchText = "甲";

            await panel.ReloadAsync();

            Assert.Equal("甲", connector.LastSearchText);
            Assert.NotNull(panel.Grid.DataTable);
            Assert.Equal(2, panel.Grid.DataTable!.Rows.Count);
        }

        [Fact]
        [DisplayName("Commit is a no-op without a selection and raises Committed with the selected row otherwise")]
        public async Task Commit_RaisesCommittedOnlyWithSelection()
        {
            var rowId = Guid.NewGuid();
            var table = BuildLookupTable((rowId, "C001", "客戶甲"));
            var panel = new LookupPanel();
            panel.Bind(BuildCustomerSchema(), new StubConnector(table));
            await panel.ReloadAsync();

            DataRow? committed = null;
            panel.Committed += (_, row) => committed = row;

            panel.Commit();
            Assert.Null(committed);

            panel.Grid.SelectedItem = panel.Grid.DataTable!.DefaultView[0];
            panel.Commit();

            Assert.NotNull(committed);
            Assert.Equal(rowId, committed![SysFields.RowId]);
        }

        [Fact]
        [DisplayName("Cancel raises Cancelled")]
        public void Cancel_RaisesCancelled()
        {
            var panel = new LookupPanel();
            var cancelled = false;
            panel.Cancelled += (_, _) => cancelled = true;

            panel.Cancel();

            Assert.True(cancelled);
        }

        [Fact]
        [DisplayName("ReloadAsync does not throw when the connection fails and leaves the grid without data")]
        public async Task ReloadAsync_ConnectorFailure_DoesNotThrow()
        {
            var panel = new LookupPanel();
            panel.Bind(BuildCustomerSchema(), new ThrowingConnector());

            var exception = await Record.ExceptionAsync(() => panel.ReloadAsync());

            Assert.Null(exception);
            Assert.Null(panel.Grid.DataTable);
        }

        private sealed class StubConnector : FormApiConnector
        {
            private readonly DataTable _table;

            public StubConnector(DataTable table) : base(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid(), "Customer")
            {
                _table = table;
            }

            public string? LastSearchText { get; private set; }

            public override Task<GetLookupResponse> GetLookupAsync(
                string searchText = "",
                Polhem.Definition.Paging.PagingOptions? paging = null)
            {
                LastSearchText = searchText;
                return Task.FromResult(new GetLookupResponse { Table = _table });
            }
        }

        private sealed class ThrowingConnector : FormApiConnector
        {
            public ThrowingConnector() : base(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid(), "Customer") { }

            public override Task<GetLookupResponse> GetLookupAsync(
                string searchText = "",
                Polhem.Definition.Paging.PagingOptions? paging = null)
                => throw new InvalidOperationException("boom");
        }
    }
}
