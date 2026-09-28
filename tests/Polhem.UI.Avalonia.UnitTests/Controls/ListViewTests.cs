using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.Form;
using Polhem.Base.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.UI.Avalonia.Views;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    /// <summary>
    /// Behaviour tests for <see cref="ListView"/>. Drives the view through
    /// <see cref="ListView.InitializeAsync"/> + a <see cref="FakeFormApiConnector"/> so the
    /// unit-test environment never needs an Avalonia visual tree nor a live JSON-RPC
    /// backend. A <see cref="TestListView"/> subclass overrides the <c>Resolve*</c> hooks so
    /// the static <c>ClientInfo</c> is never touched.
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
        [DisplayName("ListView is a subclass of UserControl")]
        public void Type_IsUserControlSubclass()
        {
            Assert.True(typeof(UserControl).IsAssignableFrom(typeof(ListView)));
        }

        [Theory]
        [InlineData(nameof(ListView.ProgId), "ProgIdProperty")]
        [InlineData(nameof(ListView.AccessToken), "AccessTokenProperty")]
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
        /// Overrides the <c>Resolve*</c> hooks so tests never read the process-wide
        /// <c>ClientInfo</c> statics; <c>ResolveFormConnector</c> throws to flag any
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

        private sealed class TestListView : ListView
        {
            protected override Task<FormSchema?> ResolveSchemaAsync(string progId, CancellationToken cancellationToken)
                => Task.FromResult<FormSchema?>(null);

            protected override FormApiConnector ResolveFormConnector(string progId)
                => throw new InvalidOperationException("ClientInfo fallback must not be reached in unit tests.");

            protected override Guid ResolveAccessToken() => Guid.Empty;
        }

        /// <summary>
        /// Test double overriding every virtual round-trip on <see cref="FormApiConnector"/>
        /// so the base <c>LocalApiProvider</c> is never reached.
        /// </summary>
        private sealed class FakeFormApiConnector : FormApiConnector
        {
            public FakeFormApiConnector() : base(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid(), TestProgId) { }

            public Func<string, GetListResponse>? GetListHandler { get; set; }
            public Func<Guid, DeleteResponse>? DeleteHandler { get; set; }

            public override Task<GetListResponse> GetListAsync(
                string selectFields = "",
                Polhem.Definition.Filters.FilterNode? filter = null,
                Polhem.Definition.Sorting.SortFieldCollection? sortFields = null,
                Polhem.Definition.Paging.PagingOptions? paging = null, CancellationToken cancellationToken = default)
                => Task.FromResult((GetListHandler ?? (_ => new GetListResponse()))(selectFields));

            public override Task<DeleteResponse> DeleteAsync(Guid rowId, CancellationToken cancellationToken = default)
                => Task.FromResult((DeleteHandler ?? (_ => new DeleteResponse()))(rowId));
        }
    }
}
