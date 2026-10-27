using System.ComponentModel;
using System.Data;
using Polhem.Api.Contracts.Form;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Paging;
using Polhem.Definition.Sorting;
using Polhem.UI.Avalonia.DataObjects;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Avalonia.UnitTests.DataObjects
{
    /// <summary>
    /// How the Avalonia <see cref="FormDataObject"/> hands field changes to the server callback: the origin it stamps on
    /// each write, the single call one edit makes, and the values it writes back. The callback rules themselves are
    /// covered by <c>FieldValueChangeCoordinatorTests</c>.
    /// </summary>
    public class FormDataObjectValueChangeTests
    {
        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add(new FormField(SysFields.RowId, "Row ID", FieldDbType.Guid));
            var customer = new FormField("customer_rowid", "Customer", FieldDbType.Guid)
            {
                RelationProgId = "Customer",
                ValueChangeCallback = true,
            };
            customer.RelationFieldMappings!.Add(SysFields.Id, "ref_customer_id");
            customer.RelationFieldMappings!.Add(SysFields.Name, "ref_customer_name");
            table.Fields!.Add(customer);
            table.Fields!.Add(new FormField("ref_customer_id", "Customer Code", FieldDbType.String, FieldType.RelationField));
            table.Fields!.Add(new FormField("ref_customer_name", "Customer Name", FieldDbType.String, FieldType.RelationField));
            // Marked as well: a value the callback writes here must not call back again.
            table.Fields!.Add(new FormField("unit_price", "Unit Price", FieldDbType.Decimal) { ValueChangeCallback = true });
            return schema;
        }

        private static DataRow BuildPickedRow(Guid rowId)
        {
            // The lookup returns only the id; the name comes from the callback.
            var table = new DataTable("Customer");
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Columns.Add(SysFields.Id, typeof(string));
            table.Rows.Add(rowId, "C001");
            return table.Rows[0];
        }

        private static (FormDataObject DataObject, FakeConnector Connector) Create()
        {
            var connector = new FakeConnector();
            connector.Respond = call =>
            {
                var values = new DataTable("Values");
                values.Columns.Add("ref_customer_name", typeof(string));
                values.Columns.Add("unit_price", typeof(decimal));
                values.Rows.Add(call.Value.Length > 0 ? "Alfreds" : string.Empty, 9.5m);
                return new FakeResponse { Values = values };
            };
            var dataObject = new FormDataObject(BuildSchema(), connector);
            dataObject.InitializeNewMaster();
            return (dataObject, connector);
        }

        [Fact]
        [DisplayName("A lookup pick makes one call after the pick is written, and the returned values never call back")]
        public async Task LookupPick_CallsBackOnce_AndWritesValues()
        {
            var (dataObject, connector) = Create();
            var origins = new List<(string Field, FieldValueOrigin Origin)>();
            dataObject.FieldValueChanged += (_, e) => origins.Add((e.FieldName, e.Origin));
            var customer = Guid.NewGuid();

            dataObject.ApplyLookupSelection(dataObject.GetFormField("customer_rowid")!, BuildPickedRow(customer));
            await dataObject.LastFieldValueChange;

            var call = Assert.Single(connector.Calls);
            Assert.Equal(("Order", "customer_rowid", customer.ToString()), (call.Table, call.Field, call.Value));
            // The call saw the whole pick: the code the lookup carried was already in the row.
            Assert.Equal("C001", call.Row!.Rows[0]["ref_customer_id"]);
            Assert.Equal("Alfreds", dataObject.GetField("ref_customer_name"));
            Assert.Equal(9.5m, dataObject.MasterRow!["unit_price"]);
            Assert.Contains(("customer_rowid", FieldValueOrigin.LookupSelection), origins);
            Assert.Contains(("ref_customer_name", FieldValueOrigin.ValueChangeResult), origins);
            Assert.Contains(("unit_price", FieldValueOrigin.ValueChangeResult), origins);
        }

        [Fact]
        [DisplayName("A user edit of a marked field calls back once; clearing a lookup does not call back")]
        public async Task UserEdit_CallsBack_LookupClear_DoesNot()
        {
            var (dataObject, connector) = Create();

            dataObject.SetField("unit_price", "3");
            await dataObject.LastFieldValueChange;
            Assert.Equal("unit_price", Assert.Single(connector.Calls).Field);

            dataObject.ClearLookupSelection(dataObject.GetFormField("customer_rowid")!);
            await dataObject.LastFieldValueChange;
            Assert.Single(connector.Calls);
        }

        [Fact]
        [DisplayName("A row edit session calls back while editing, and its commit republishes without calling back again")]
        public async Task RowEditSession_CallsBackOnceAcrossCommit()
        {
            var (dataObject, connector) = Create();
            var row = dataObject.MasterRow!;
            var commitOrigins = new List<FieldValueOrigin>();

            dataObject.BeginRowEdit(row);
            dataObject.SetField(row, "customer_rowid", Guid.NewGuid().ToString());
            await dataObject.LastFieldValueChange;
            dataObject.FieldValueChanged += (_, e) => commitOrigins.Add(e.Origin);
            dataObject.CommitRowEdit(row);
            await dataObject.LastFieldValueChange;

            Assert.Single(connector.Calls);
            Assert.Equal("Alfreds", dataObject.GetField("ref_customer_name"));
            Assert.NotEmpty(commitOrigins);
            Assert.All(commitOrigins, origin => Assert.Equal(FieldValueOrigin.RowEditCommit, origin));
        }

        [Fact]
        [DisplayName("The server's messages and a failed call reach the data object's events")]
        public async Task MessagesAndFailures_AreRaised()
        {
            var (dataObject, connector) = Create();
            var response = new FakeResponse();
            response.ErrorsMap["customer_rowid"] = "No such customer.";
            connector.Respond = _ => response;
            FieldValueChangeAppliedEventArgs? applied = null;
            FieldValueChangeFailedEventArgs? failed = null;
            dataObject.FieldValueChangeApplied += (_, e) => applied = e;
            dataObject.FieldValueChangeFailed += (_, e) => failed = e;

            dataObject.SetField("customer_rowid", Guid.NewGuid().ToString());
            await dataObject.LastFieldValueChange;
            Assert.Equal("No such customer.", applied!.Errors["customer_rowid"]);

            connector.Respond = _ => throw new InvalidOperationException("down");
            dataObject.SetField("customer_rowid", Guid.NewGuid().ToString());
            await dataObject.LastFieldValueChange;
            Assert.Equal("down", failed!.Exception.Message);
        }

        [Fact]
        [DisplayName("Without a connector that supports callbacks, a change makes no call and a pick keeps its old rule")]
        public void NoCallbackConnector_LeavesBehaviourAsBefore()
        {
            var dataObject = new FormDataObject(BuildSchema());
            dataObject.InitializeNewMaster();

            dataObject.ApplyLookupSelection(dataObject.GetFormField("customer_rowid")!, BuildPickedRow(Guid.NewGuid()));

            Assert.Equal("C001", dataObject.GetField("ref_customer_id"));
            Assert.Same(Task.CompletedTask, dataObject.LastFieldValueChange);
        }

        private sealed record Call(string Table, string Field, string Value, DataTable? Row);

        private sealed class FakeConnector : IFormApiConnector, IFieldValueChangeConnector
        {
            public List<Call> Calls { get; } = [];

            public Func<Call, IFieldValueChangeResponse> Respond { get; set; } = _ => new FakeResponse();

            public string ProgId => "Order";

            public Task<IFieldValueChangeResponse> FieldValueChangeAsync(string tableName, string fieldName, string value,
                DataTable? row, CancellationToken cancellationToken = default)
            {
                var call = new Call(tableName, fieldName, value, row);
                Calls.Add(call);
                return Task.FromResult(Respond(call));
            }

            public Task<IGetListResponse> GetListAsync(string selectFields = "", FilterNode? filter = null,
                SortFieldCollection? sortFields = null, PagingOptions? paging = null,
                CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<IGetLookupResponse> GetLookupAsync(string searchText = "", PagingOptions? paging = null,
                CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<IGetNewDataResponse> GetNewDataAsync(CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IGetDataResponse> GetDataAsync(Guid rowId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<ISaveResponse> SaveAsync(DataSet dataSet, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IDeleteResponse> DeleteAsync(Guid rowId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        private sealed class FakeResponse : IFieldValueChangeResponse
        {
            public DataTable? Values { get; init; }
            public Dictionary<string, string> ErrorsMap { get; } = [];
            public IReadOnlyDictionary<string, string> Errors => ErrorsMap;
            public IReadOnlyDictionary<string, string> Warnings { get; } = new Dictionary<string, string>();
        }
    }
}
