using System.ComponentModel;
using Polhem.Api.Contracts.Form;
using System.Data;
using System.Globalization;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.Form;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Views;
using Polhem.UI.Core.Connectors;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    /// <summary>
    /// Behaviour tests for <see cref="ListView"/>. Drives the view through
    /// <see cref="ListView.InitializeAsync"/> + a <see cref="FakeFormApiConnector"/> so the
    /// unit-test environment never needs an Avalonia visual tree nor a live JSON-RPC
    /// backend. A <see cref="TestListView"/> subclass overrides the <c>Resolve*</c> hooks so
    /// the services registered in <c>UIServices</c> are never reached.
    /// </summary>
    public class ListViewTests
    {
        private const string TestProgId = "Category";

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema(TestProgId, TestProgId);
            schema.ListFields = "sys_id,sys_name";
            var master = schema.Tables!.Add(TestProgId, TestProgId);
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            master.Fields.Add("sys_id", "Category Code", FieldDbType.String);
            master.Fields.Add("sys_name", "Category Name", FieldDbType.String);
            return schema;
        }

        private static DataTable BuildListTable(Guid rowId, string name)
        {
            var table = new DataTable(TestProgId);
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Columns.Add("sys_id", typeof(string));
            table.Columns.Add("sys_name", typeof(string));
            table.Rows.Add(rowId, "C001", name);
            return table;
        }

        private static TestListView BuildInitializableView(FakeFormApiConnector connector)
            => new() { Schema = BuildSchema(), FormConnector = connector };

        private static void InvokePrivate(ListView view, string methodName, params object[] args)
        {
            var method = typeof(ListView).GetMethod(
                methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            method!.Invoke(view, args);
        }

        private static async Task InvokePrivateAsync(ListView view, string methodName, params object[] args)
        {
            var method = typeof(ListView).GetMethod(
                methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            await (Task)method!.Invoke(view, args)!;
        }

        private static T InvokePrivateFunc<T>(ListView view, string methodName, params object[] args)
        {
            var method = typeof(ListView).GetMethod(
                methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            return (T)method!.Invoke(view, args)!;
        }

        [Fact]
        [DisplayName("The first load asks for the first page and the row count")]
        public async Task InitializeAsync_RequestsFirstPageWithCount()
        {
            var connector = PagedConnector(totalRows: 120);

            await BuildInitializableView(connector).InitializeAsync();

            var request = Assert.Single(connector.Requests);
            Assert.Equal(1, request.Page);
            Assert.Equal(ListPagingState.DefaultPageSize, request.PageSize);
            Assert.True(request.IncludeTotalCount);
        }

        [Fact]
        [DisplayName("Moving to another page does not count the rows again")]
        public async Task LoadPage_AfterFirstLoad_DoesNotCountAgain()
        {
            var connector = PagedConnector(totalRows: 120);
            var view = BuildInitializableView(connector);
            await view.InitializeAsync();

            await InvokePrivateAsync(view, "LoadPageAsync", 2, CancellationToken.None);

            Assert.Equal(2, connector.Requests[^1].Page);
            Assert.False(connector.Requests[^1].IncludeTotalCount);
        }

        [Fact]
        [DisplayName("ReloadAsync stays on the page on screen and counts the rows again")]
        public async Task ReloadAsync_OnLaterPage_KeepsPageAndCounts()
        {
            var connector = PagedConnector(totalRows: 120);
            var view = BuildInitializableView(connector);
            await view.InitializeAsync();
            await InvokePrivateAsync(view, "LoadPageAsync", 2, CancellationToken.None);

            await view.ReloadAsync();

            Assert.Equal(2, connector.Requests[^1].Page);
            Assert.True(connector.Requests[^1].IncludeTotalCount);
        }

        [Fact]
        [DisplayName("A page that came back empty loads the last page that still has rows")]
        public async Task ReloadAsync_PageEmptied_StepsBack()
        {
            var connector = PagedConnector(totalRows: 51);
            var view = BuildInitializableView(connector);
            await view.InitializeAsync();
            await InvokePrivateAsync(view, "LoadPageAsync", 2, CancellationToken.None);

            connector.PagedHandler = PagedResponder(totalRows: 50);
            await view.ReloadAsync();

            Assert.Equal(1, connector.Requests[^1].Page);
        }

        [Fact]
        [DisplayName("A failed page request leaves the view on the page on screen")]
        public async Task LoadPage_Failure_StaysOnPage()
        {
            var connector = PagedConnector(totalRows: 120);
            var view = BuildInitializableView(connector);
            await view.InitializeAsync();
            connector.PagedHandler = _ => throw new InvalidOperationException("boom");

            await InvokePrivateAsync(view, "LoadPageAsync", 2, CancellationToken.None);

            connector.PagedHandler = PagedResponder(totalRows: 120);
            await view.ReloadAsync();
            Assert.Equal(1, connector.Requests[^1].Page);
        }

        [Fact]
        [DisplayName("A sort request reads the first page in the new order and a reload keeps it")]
        public async Task SortRequest_ReadsFirstPageInNewOrder()
        {
            var connector = PagedConnector(totalRows: 120);
            var view = BuildInitializableView(connector);
            await view.InitializeAsync();
            await InvokePrivateAsync(view, "LoadPageAsync", 2, CancellationToken.None);

            await InvokePrivateAsync(view, "OnSortRequestedAsync", "sys_name");

            Assert.Equal(1, connector.Requests[^1].Page);
            Assert.Equal("sys_name", Assert.Single(connector.LastSort!).FieldName);

            await view.ReloadAsync();
            Assert.Equal("sys_name", Assert.Single(connector.LastSort!).FieldName);
        }

        [Fact]
        [DisplayName("The list columns are sortable except a Text column, and the sorted header carries a mark")]
        public void EnableServerSort_MarksSortableColumnsAndHeader()
        {
            var schema = new FormSchema(TestProgId, TestProgId);
            var master = schema.Tables!.Add(TestProgId, TestProgId);
            master.Fields!.Add("sys_name", "Name", FieldDbType.String);
            master.Fields.Add("memo", "Memo", FieldDbType.Text);
            var layout = new Polhem.Definition.Layouts.LayoutGrid { TableName = TestProgId };
            layout.Columns!.Add("sys_name", "Name", ControlType.TextEdit);
            layout.Columns.Add("memo", "Memo", ControlType.TextEdit);
            var grid = new Polhem.UI.Avalonia.Controls.GridControl();

            grid.EnableServerSort();
            grid.Bind(layout, null, master);
            grid.SetSortIndicator("sys_name", Polhem.Definition.Sorting.SortDirection.Desc);

            Assert.True(grid.InnerGrid.Columns[0].CanUserSort);
            Assert.False(grid.InnerGrid.Columns[1].CanUserSort);
            Assert.Equal("Name \u25BC", grid.InnerGrid.Columns[0].Header);
            Assert.Equal("Memo", grid.InnerGrid.Columns[1].Header);
        }

        private static FakeFormApiConnector PagedConnector(int totalRows)
            => new() { PagedHandler = PagedResponder(totalRows) };

        private static Func<Polhem.Definition.Paging.PagingOptions, GetListResponse> PagedResponder(int totalRows)
            => options =>
            {
                var skipped = (options.Page - 1) * options.PageSize;
                var table = new DataTable(TestProgId);
                table.Columns.Add(SysFields.RowId, typeof(Guid));
                table.Columns.Add("sys_id", typeof(string));
                table.Columns.Add("sys_name", typeof(string));
                for (var i = skipped; i < Math.Min(skipped + options.PageSize, totalRows); i++)
                {
                    table.Rows.Add(Guid.NewGuid(), $"C{i}", $"Name {i}");
                }
                return new GetListResponse
                {
                    Table = table,
                    Paging = new Polhem.Definition.Paging.PagingInfo
                    {
                        Page = options.Page,
                        PageSize = options.PageSize,
                        TotalCount = options.IncludeTotalCount ? totalRows : null,
                        HasMore = skipped + table.Rows.Count < totalRows,
                    },
                };
            };

        [Fact]
        [DisplayName("ListView is a subclass of UserControl")]
        public void Type_IsUserControlSubclass()
        {
            Assert.True(typeof(UserControl).IsAssignableFrom(typeof(ListView)));
        }

        [Theory]
        [InlineData(nameof(ListView.ProgId), "ProgIdProperty")]
        [InlineData(nameof(ListView.Schema), "SchemaProperty")]
        [InlineData(nameof(ListView.FormConnector), "FormConnectorProperty")]
        [DisplayName("Every public property has a matching StyledProperty registration")]
        public void PublicProperties_HaveMatchingStyledProperty(string propertyName, string styledPropertyFieldName)
        {
            var property = typeof(ListView).GetProperty(
                propertyName, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property);

            var styled = typeof(ListView).GetField(
                styledPropertyFieldName, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(styled);
        }

        [Fact]
        [DisplayName("InitializeAsync loads the list through GetList with SelectFields prefixed by sys_rowid")]
        public async Task InitializeAsync_LoadsListWithRowIdPrefixedSelectFields()
        {
            string? requestedSelectFields = null;
            var connector = new FakeFormApiConnector
            {
                GetListHandler = select =>
                {
                    requestedSelectFields = select;
                    return new GetListResponse { Table = BuildListTable(Guid.NewGuid(), "Beverages") };
                },
            };
            var view = BuildInitializableView(connector);

            await view.InitializeAsync();

            Assert.NotNull(requestedSelectFields);
            var fields = requestedSelectFields!.Split(',');
            Assert.Equal(SysFields.RowId, fields[0]);
            Assert.Contains("sys_id", fields);
            Assert.Contains("sys_name", fields);
        }

        [Fact]
        [DisplayName("ComputeSelectFields does not duplicate sys_rowid")]
        public void ComputeSelectFields_DoesNotDuplicateRowId()
        {
            var schema = BuildSchema();
            schema.ListFields = "sys_rowid,sys_id,sys_name";
            var view = new TestListView { Schema = schema, FormConnector = new FakeFormApiConnector() };

            var result = InvokePrivateFunc<string>(view, "ComputeSelectFields");

            var occurrences = result.Split(',').Count(f => f.Equals(SysFields.RowId, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(1, occurrences);
        }

        [Fact]
        [DisplayName("View after selecting a row raises ViewRequested with the correct rowId")]
        public async Task View_AfterRowSelected_RaisesViewRequested()
        {
            var rowId = Guid.NewGuid();
            var connector = new FakeFormApiConnector
            {
                GetListHandler = _ => new GetListResponse { Table = BuildListTable(rowId, "Beverages") },
            };
            var view = BuildInitializableView(connector);
            await view.InitializeAsync();

            Guid requested = Guid.Empty;
            view.ViewRequested += (_, id) => requested = id;

            InvokePrivate(view, "OnRowSelected", rowId);
            InvokePrivate(view, "OnViewClicked");

            Assert.Equal(rowId, requested);
        }

        [Fact]
        [DisplayName("Edit after selecting a row raises EditRequested with the correct rowId")]
        public async Task Edit_AfterRowSelected_RaisesEditRequested()
        {
            var rowId = Guid.NewGuid();
            var connector = new FakeFormApiConnector
            {
                GetListHandler = _ => new GetListResponse { Table = BuildListTable(rowId, "Beverages") },
            };
            var view = BuildInitializableView(connector);
            await view.InitializeAsync();

            Guid requested = Guid.Empty;
            view.EditRequested += (_, id) => requested = id;

            InvokePrivate(view, "OnRowSelected", rowId);
            InvokePrivate(view, "OnEditClicked");

            Assert.Equal(rowId, requested);
        }

        [Fact]
        [DisplayName("Double-tapping a selected row raises ViewRequested (opens the read-only view, not edit)")]
        public async Task DoubleTap_AfterRowSelected_RaisesViewRequested()
        {
            var rowId = Guid.NewGuid();
            var connector = new FakeFormApiConnector
            {
                GetListHandler = _ => new GetListResponse { Table = BuildListTable(rowId, "Beverages") },
            };
            var view = BuildInitializableView(connector);
            await view.InitializeAsync();

            Guid viewRequested = Guid.Empty;
            Guid editRequested = Guid.Empty;
            view.ViewRequested += (_, id) => viewRequested = id;
            view.EditRequested += (_, id) => editRequested = id;

            InvokePrivate(view, "OnRowSelected", rowId);
            InvokePrivate(view, "OnGridDoubleTapped");

            Assert.Equal(rowId, viewRequested);
            Assert.Equal(Guid.Empty, editRequested);
        }

        [Fact]
        [DisplayName("Edit without a selected row does not raise EditRequested")]
        public async Task Edit_WithoutSelection_DoesNotRaiseEditRequested()
        {
            var connector = new FakeFormApiConnector
            {
                GetListHandler = _ => new GetListResponse { Table = BuildListTable(Guid.NewGuid(), "Beverages") },
            };
            var view = BuildInitializableView(connector);
            await view.InitializeAsync();

            var raised = false;
            view.EditRequested += (_, _) => raised = true;

            InvokePrivate(view, "OnEditClicked");

            Assert.False(raised);
        }

        [Fact]
        [DisplayName("New raises AddRequested")]
        public async Task New_RaisesAddRequested()
        {
            var connector = new FakeFormApiConnector
            {
                GetListHandler = _ => new GetListResponse { Table = BuildListTable(Guid.NewGuid(), "Beverages") },
            };
            var view = BuildInitializableView(connector);
            await view.InitializeAsync();

            var raised = false;
            view.AddRequested += (_, _) => raised = true;

            InvokePrivate(view, "OnNewClicked");

            Assert.True(raised);
        }

        [Fact]
        [DisplayName("Delete after selecting a row calls DeleteAsync and reloads the list")]
        public async Task Delete_AfterRowSelected_DeletesAndReloads()
        {
            var rowId = Guid.NewGuid();
            var getListCount = 0;
            Guid deletedRowId = Guid.Empty;
            var connector = new FakeFormApiConnector
            {
                GetListHandler = _ =>
                {
                    getListCount++;
                    return new GetListResponse { Table = BuildListTable(rowId, "Beverages") };
                },
                DeleteHandler = id =>
                {
                    deletedRowId = id;
                    return new DeleteResponse();
                },
            };
            var view = BuildInitializableView(connector);
            await view.InitializeAsync();
            Assert.Equal(1, getListCount);

            InvokePrivate(view, "OnRowSelected", rowId);
            await InvokePrivateAsync(view, "OnDeleteClickedAsync");

            Assert.Equal(rowId, deletedRowId);
            Assert.Equal(2, getListCount);
        }

        [Fact]
        [DisplayName("Declining the delete question deletes nothing and keeps the list and the selection")]
        public async Task Delete_Declined_DeletesNothing()
        {
            var rowId = Guid.NewGuid();
            var getListCount = 0;
            var deleteCount = 0;
            var connector = new FakeFormApiConnector
            {
                GetListHandler = _ =>
                {
                    getListCount++;
                    return new GetListResponse { Table = BuildListTable(rowId, "Beverages") };
                },
                DeleteHandler = _ =>
                {
                    deleteCount++;
                    return new DeleteResponse();
                },
            };
            var view = BuildInitializableView(connector);
            view.ConfirmAnswer = false;
            await view.InitializeAsync();

            InvokePrivate(view, "OnRowSelected", rowId);
            await InvokePrivateAsync(view, "OnDeleteClickedAsync");

            Assert.Equal([rowId], view.ConfirmedRowIds);
            Assert.Equal(0, deleteCount);
            Assert.Equal(1, getListCount);
        }

        [Fact]
        [DisplayName("With ConfirmDelete off, Delete deletes without asking")]
        public async Task Delete_ConfirmDeleteOff_DeletesWithoutAsking()
        {
            var rowId = Guid.NewGuid();
            Guid deletedRowId = Guid.Empty;
            var connector = new FakeFormApiConnector
            {
                GetListHandler = _ => new GetListResponse { Table = BuildListTable(rowId, "Beverages") },
                DeleteHandler = id =>
                {
                    deletedRowId = id;
                    return new DeleteResponse();
                },
            };
            var view = BuildInitializableView(connector);
            view.ConfirmDelete = false;
            await view.InitializeAsync();

            InvokePrivate(view, "OnRowSelected", rowId);
            await InvokePrivateAsync(view, "OnDeleteClickedAsync");

            Assert.Empty(view.ConfirmedRowIds);
            Assert.Equal(rowId, deletedRowId);
        }

        [Fact]
        [DisplayName("A failed list load raises ErrorOccurred")]
        public async Task ReloadAsync_OnFailure_RaisesErrorOccurred()
        {
            var connector = new FakeFormApiConnector
            {
                GetListHandler = _ => throw new InvalidOperationException("backend down"),
            };

            // Subscribe before the FormConnector setter triggers initialization: assigning
            // Schema + FormConnector raises the StyledProperty change handler, which kicks
            // off InitializeAsync — so the failing list load happens during the setter, not
            // the explicit InitializeAsync call below.
            var view = new TestListView { Schema = BuildSchema() };
            Exception? reported = null;
            view.ErrorOccurred += (_, ex) => reported = ex;
            view.FormConnector = connector;

            await view.InitializeAsync();

            Assert.IsType<InvalidOperationException>(reported);
        }

        /// <summary>
        /// Overrides the <c>Resolve*</c> hooks so tests never reach the process-wide
        /// <c>UIServices</c> provider; <c>ResolveFormConnector</c> throws to flag any
        /// unexpected ProgId-fallback path.
        /// </summary>
        [Fact]
        [DisplayName("The compact card list formats a date as a short date and a number by its format, as the wide grid does")]
        public async Task CardTemplate_DateAndNumberColumns_UseGridCellFormatting()
        {
            var orderDate = new DateTime(1996, 7, 4, 0, 0, 0, DateTimeKind.Unspecified);
            const decimal freight = 1234.5m;
            var schema = new FormSchema(TestProgId, TestProgId) { ListFields = "sys_id,order_date,freight" };
            var master = schema.Tables!.Add(TestProgId, TestProgId);
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            master.Fields.Add("sys_id", "Order No.", FieldDbType.String);
            master.Fields.Add("order_date", "Order Date", FieldDbType.Date);
            master.Fields.Add("freight", "Freight", FieldDbType.Decimal).NumberFormat = "N2";

            var table = new DataTable(TestProgId);
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Columns.Add("sys_id", typeof(string));
            table.Columns.Add("order_date", typeof(DateTime));
            table.Columns.Add("freight", typeof(decimal));
            table.Rows.Add(Guid.NewGuid(), "O001", orderDate, freight);

            var connector = new FakeFormApiConnector { GetListHandler = _ => new GetListResponse { Table = table } };
            var view = new TestListView { Schema = schema, FormConnector = connector };
            await view.InitializeAsync();

            var cardList = (ListBox)typeof(ListView)
                .GetField("_cardList", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(view)!;
            var template = Assert.IsType<FuncDataTemplate<DataRowView>>(cardList.ItemTemplate);
            var card = Assert.IsType<Border>(template.Build(table.DefaultView[0]));
            var values = ((StackPanel)card.Child!).Children
                .Cast<StackPanel>()
                .ToDictionary(line => ((TextBlock)line.Children[0]).Text!, line => ((TextBlock)line.Children[1]).Text);

            Assert.Equal(orderDate.ToString("d", CultureInfo.CurrentCulture), values["Order Date"]);
            Assert.Equal(freight.ToString("N2", CultureInfo.CurrentCulture), values["Freight"]);
        }

        [Fact]
        [DisplayName("The compact card list shows a drop-down's list item text, a check box for a Boolean and the time of an instant")]
        public async Task CardTemplate_DropDownBooleanAndInstant_ShowReadableValues()
        {
            var createdAt = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Unspecified);
            var schema = new FormSchema(TestProgId, TestProgId) { ListFields = "sys_id,change_mode,is_sensitive,created_at" };
            var master = schema.Tables!.Add(TestProgId, TestProgId);
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            master.Fields.Add("sys_id", "Program Id", FieldDbType.String);
            var mode = master.Fields.Add("change_mode", "Change Log", FieldDbType.Integer);
            mode.ControlType = ControlType.DropDownEdit;
            mode.ListItems!.Add("0", "Inherit");
            mode.ListItems.Add("1", "On");
            master.Fields.Add("is_sensitive", "Sensitive", FieldDbType.Boolean);
            master.Fields.Add("created_at", "Created At", FieldDbType.DateTime);

            var table = new DataTable(TestProgId);
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Columns.Add("sys_id", typeof(string));
            table.Columns.Add("change_mode", typeof(int));
            table.Columns.Add("is_sensitive", typeof(bool));
            table.Columns.Add("created_at", typeof(DateTime));
            table.Rows.Add(Guid.NewGuid(), "Order", 1, true, createdAt);

            var connector = new FakeFormApiConnector { GetListHandler = _ => new GetListResponse { Table = table } };
            var view = new TestListView { Schema = schema, FormConnector = connector };
            await view.InitializeAsync();

            var cardList = (ListBox)typeof(ListView)
                .GetField("_cardList", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(view)!;
            var template = Assert.IsType<FuncDataTemplate<DataRowView>>(cardList.ItemTemplate);
            var card = Assert.IsType<Border>(template.Build(table.DefaultView[0]));
            var values = ((StackPanel)card.Child!).Children
                .Cast<StackPanel>()
                .ToDictionary(line => ((TextBlock)line.Children[0]).Text!, line => line.Children[1]);

            Assert.Equal("On", Assert.IsType<TextBlock>(values["Change Log"]).Text);
            var check = Assert.IsType<CheckBox>(values["Sensitive"]);
            Assert.True(check.IsChecked);
            Assert.False(check.IsEnabled);
            Assert.Equal(createdAt.ToString("G", CultureInfo.CurrentCulture), Assert.IsType<TextBlock>(values["Created At"]).Text);
        }

        private sealed class TestListView : ListView
        {
            public bool ConfirmAnswer { get; set; } = true;

            public List<Guid> ConfirmedRowIds { get; } = [];

            protected override Task<bool> ConfirmDeleteAsync(Guid rowId, CancellationToken cancellationToken = default)
            {
                ConfirmedRowIds.Add(rowId);
                return Task.FromResult(ConfirmAnswer);
            }

            protected override Task<FormSchema?> ResolveSchemaAsync(string progId, CancellationToken cancellationToken)
                => Task.FromResult<FormSchema?>(null);

            protected override IFormApiConnector? ResolveFormConnector(string progId)
                => throw new InvalidOperationException("The registered connector factory must not be reached in unit tests.");
        }

        /// <summary>
        /// Test double overriding every virtual round-trip on <see cref="FormApiConnector"/>
        /// so the in-process transport of its client is never reached.
        /// </summary>
        private sealed class FakeFormApiConnector : FormApiConnector
        {
            public FakeFormApiConnector() : base(Polhem.Api.Client.PolhemApiClient.CreateLocal(Polhem.Tests.Shared.EmptyServiceProvider.Instance), TestProgId) { }

            public Func<string, GetListResponse>? GetListHandler { get; set; }
            public Func<Polhem.Definition.Paging.PagingOptions, GetListResponse>? PagedHandler { get; set; }
            public List<Polhem.Definition.Paging.PagingOptions> Requests { get; } = [];
            public Polhem.Definition.Sorting.SortFieldCollection? LastSort { get; private set; }
            public Func<Guid, DeleteResponse>? DeleteHandler { get; set; }

            public override Task<GetListResponse> GetListAsync(
                string selectFields = "",
                Polhem.Definition.Filters.FilterNode? filter = null,
                Polhem.Definition.Sorting.SortFieldCollection? sortFields = null,
                Polhem.Definition.Paging.PagingOptions? paging = null, CancellationToken cancellationToken = default)
            {
                if (paging is not null) { Requests.Add(paging); }
                LastSort = sortFields;
                return Task.FromResult(PagedHandler is not null && paging is not null
                    ? PagedHandler(paging)
                    : (GetListHandler ?? (_ => new GetListResponse()))(selectFields));
            }

            public override Task<DeleteResponse> DeleteAsync(Guid rowId, CancellationToken cancellationToken = default)
                => Task.FromResult((DeleteHandler ?? (_ => new DeleteResponse()))(rowId));
        }
    }
}
