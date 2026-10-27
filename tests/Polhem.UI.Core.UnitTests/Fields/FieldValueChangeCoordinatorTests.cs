using System.ComponentModel;
using System.Data;
using Polhem.Api.Contracts.Form;
using Polhem.Core.Data;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Core.UnitTests.Fields
{
    /// <summary>
    /// <see cref="FieldValueChangeCoordinator"/> with a fake connector the test answers: which changes call back, how the
    /// answer is written into the row, and which answers are discarded.
    /// </summary>
    public class FieldValueChangeCoordinatorTests
    {
        private const string Table = "Order";

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema(Table, "Order");
            var table = schema.Tables!.Add(Table, "Order");
            var customer = table.Fields!.Add("cust_rowid", "Customer", FieldDbType.Guid);
            customer.ValueChangeCallback = true;
            var part = table.Fields!.Add("part_no", "Part", FieldDbType.String);
            part.ValueChangeCallback = true;
            table.Fields!.Add("qty", "Quantity", FieldDbType.Decimal);
            table.Fields!.Add(new FormField("ref_cust_name", "Customer Name", FieldDbType.String, FieldType.RelationField));
            var price = table.Fields!.Add("unit_price", "Unit Price", FieldDbType.Decimal);
            price.ValueChangeCallback = true;
            return schema;
        }

        private static DataRow BuildRow()
        {
            var table = new DataTable(Table);
            table.Columns.Add("cust_rowid", typeof(Guid));
            table.Columns.Add("part_no", typeof(string));
            table.Columns.Add("qty", typeof(decimal));
            table.Columns.Add("ref_cust_name", typeof(string));
            table.Columns.Add("unit_price", typeof(decimal));
            var row = table.NewRow();
            row["cust_rowid"] = Guid.Empty;
            row["part_no"] = string.Empty;
            row["qty"] = 0m;
            row["ref_cust_name"] = string.Empty;
            row["unit_price"] = 0m;
            table.Rows.Add(row);
            return row;
        }

        private static FakeResponse Values(params (string Field, object Value)[] values)
        {
            var table = new DataTable("Values");
            foreach (var (field, value) in values) { table.Columns.Add(field, value.GetType()); }
            table.Rows.Add(values.Select(v => v.Value).ToArray());
            return new FakeResponse { Values = table };
        }

        /// <summary>A coordinator whose writer records each write and marks it as a callback result, as a data object does.</summary>
        private sealed class Harness
        {
            public Harness()
            {
                FieldValueChangeCoordinator? coordinator = null;
                coordinator = new FieldValueChangeCoordinator(BuildSchema(), Connector, (row, field, value) =>
                {
                    Writes.Add(field);
                    row[field] = FormValueBinding.ToColumnValue(value, row.Table.Columns[field]!);
                    // The data object reports its own write back to the coordinator, as its event bridge does.
                    _ = coordinator!.OnFieldValueChangedAsync(Table, field, row, FieldValueOrigin.ValueChangeResult);
                });
                Coordinator = coordinator;
            }

            public FakeConnector Connector { get; } = new();
            public FieldValueChangeCoordinator Coordinator { get; }
            public List<string> Writes { get; } = [];
        }

        [Fact]
        [DisplayName("A user change of a marked field calls back with the table, field, value and a copy of the row")]
        public async Task UserChange_MarkedField_CallsBack()
        {
            var harness = new Harness();
            var row = BuildRow();
            row["part_no"] = "P-1";
            row["qty"] = 3m;

            await harness.Coordinator.OnFieldValueChangedAsync(Table, "part_no", row, FieldValueOrigin.User);

            var call = Assert.Single(harness.Connector.Calls);
            Assert.Equal((Table, "part_no", "P-1"), (call.Table, call.Field, call.Value));
            Assert.NotNull(call.Row);
            Assert.NotSame(row.Table, call.Row);
            Assert.Equal(3m, call.Row!.Rows[0]["qty"]);
        }

        [Theory]
        [InlineData(FieldValueOrigin.LookupSelection)]
        [InlineData(FieldValueOrigin.ValueChangeResult)]
        [InlineData(FieldValueOrigin.RowEditCommit)]
        [DisplayName("A change that did not come from the user never calls back")]
        public async Task NonUserChange_DoesNotCallBack(FieldValueOrigin origin)
        {
            var harness = new Harness();

            await harness.Coordinator.OnFieldValueChangedAsync(Table, "part_no", BuildRow(), origin);

            Assert.Empty(harness.Connector.Calls);
        }

        [Fact]
        [DisplayName("A user change of a field that is not marked does not call back")]
        public async Task UnmarkedField_DoesNotCallBack()
        {
            var harness = new Harness();

            await harness.Coordinator.OnFieldValueChangedAsync(Table, "qty", BuildRow(), FieldValueOrigin.User);

            Assert.Empty(harness.Connector.Calls);
        }

        [Fact]
        [DisplayName("The answer is written into the row, and fields it writes never call back even when they are marked")]
        public async Task Answer_IsWritten_WithoutChainedCallbacks()
        {
            var harness = new Harness();
            harness.Connector.Respond = _ => Values(("ref_cust_name", "Alfreds"), ("unit_price", 9.5m), ("part_no", "P-9"));
            var row = BuildRow();
            var customer = Guid.NewGuid();
            row["cust_rowid"] = customer;

            await harness.Coordinator.OnFieldValueChangedAsync(Table, "cust_rowid", row, FieldValueOrigin.User);

            Assert.Single(harness.Connector.Calls);
            Assert.Equal(["ref_cust_name", "unit_price", "part_no"], harness.Writes);
            Assert.Equal("Alfreds", row["ref_cust_name"]);
            Assert.Equal(9.5m, row["unit_price"]);
            Assert.Equal("P-9", row["part_no"]);
        }

        [Fact]
        [DisplayName("Applied carries the server's errors and warnings, formatted with the field captions")]
        public async Task Applied_CarriesMessages()
        {
            var harness = new Harness();
            var response = new FakeResponse();
            response.ErrorsMap["part_no"] = "No such part.";
            response.WarningsMap["qty"] = "Below safety stock.";
            harness.Connector.Respond = _ => response;
            FieldValueChangeAppliedEventArgs? applied = null;
            harness.Coordinator.Applied += (_, e) => applied = e;
            var row = BuildRow();
            row["part_no"] = "P-X";

            await harness.Coordinator.OnFieldValueChangedAsync(Table, "part_no", row, FieldValueOrigin.User);

            Assert.NotNull(applied);
            Assert.True(applied!.HasMessages);
            Assert.Equal($"Part: No such part.{Environment.NewLine}Quantity: Below safety stock.",
                applied.FormatMessages(BuildSchema().MasterTable));
            Assert.Equal("P-X", row["part_no"]);
        }

        [Fact]
        [DisplayName("An answer to an earlier value of the field is discarded once the field has changed again")]
        public async Task StaleAnswer_IsDiscarded()
        {
            var harness = new Harness();
            var first = new TaskCompletionSource<IFieldValueChangeResponse>();
            var second = new TaskCompletionSource<IFieldValueChangeResponse>();
            var pending = new Queue<TaskCompletionSource<IFieldValueChangeResponse>>([first, second]);
            harness.Connector.RespondAsync = _ => pending.Dequeue().Task;
            var row = BuildRow();

            row["part_no"] = "P-1";
            var firstCall = harness.Coordinator.OnFieldValueChangedAsync(Table, "part_no", row, FieldValueOrigin.User);
            row["part_no"] = "P-2";
            var secondCall = harness.Coordinator.OnFieldValueChangedAsync(Table, "part_no", row, FieldValueOrigin.User);
            second.SetResult(Values(("unit_price", 2m)));
            await secondCall;
            first.SetResult(Values(("unit_price", 1m)));
            await firstCall;

            Assert.Equal(2m, row["unit_price"]);
            Assert.Equal(["unit_price"], harness.Writes);
        }

        [Fact]
        [DisplayName("An answer that arrives after the value was edited back by hand, or after the row was deleted, is discarded")]
        public async Task AnswerForChangedOrDeletedRow_IsDiscarded()
        {
            var harness = new Harness();
            var gate = new TaskCompletionSource<IFieldValueChangeResponse>();
            harness.Connector.RespondAsync = _ => gate.Task;
            var edited = BuildRow();
            edited["part_no"] = "P-1";
            var editedCall = harness.Coordinator.OnFieldValueChangedAsync(Table, "part_no", edited, FieldValueOrigin.User);
            var deleted = BuildRow();
            deleted["part_no"] = "P-1";
            var deletedCall = harness.Coordinator.OnFieldValueChangedAsync(Table, "part_no", deleted, FieldValueOrigin.User);

            edited["part_no"] = "P-other";
            deleted.Delete();
            gate.SetResult(Values(("unit_price", 1m)));
            await Task.WhenAll(editedCall, deletedCall);

            Assert.Empty(harness.Writes);
        }

        [Fact]
        [DisplayName("A failed call is reported through Failed and writes nothing")]
        public async Task FailedCall_IsReported()
        {
            var harness = new Harness();
            harness.Connector.RespondAsync = _ => Task.FromException<IFieldValueChangeResponse>(new InvalidOperationException("down"));
            FieldValueChangeFailedEventArgs? failed = null;
            harness.Coordinator.Failed += (_, e) => failed = e;
            var row = BuildRow();
            row["part_no"] = "P-1";

            await harness.Coordinator.OnFieldValueChangedAsync(Table, "part_no", row, FieldValueOrigin.User);

            Assert.Equal("down", failed!.Exception.Message);
            Assert.Empty(harness.Writes);
        }

        [Fact]
        [DisplayName("A finished lookup pick calls back for a marked relation field, and an unmarked one does not")]
        public async Task LookupSelected_CallsBackForMarkedFieldOnly()
        {
            var harness = new Harness();
            var row = BuildRow();

            await harness.Coordinator.OnLookupSelectedAsync(Table, "cust_rowid", row);
            await harness.Coordinator.OnLookupSelectedAsync(Table, "qty", row);

            Assert.Equal("cust_rowid", Assert.Single(harness.Connector.Calls).Field);
        }

        private sealed record Call(string Table, string Field, string Value, DataTable? Row);

        private sealed class FakeConnector : IFieldValueChangeConnector
        {
            public List<Call> Calls { get; } = [];

            public Func<Call, IFieldValueChangeResponse> Respond { get; set; } = _ => new FakeResponse();

            public Func<Call, Task<IFieldValueChangeResponse>>? RespondAsync { get; set; }

            public Task<IFieldValueChangeResponse> FieldValueChangeAsync(string tableName, string fieldName, string value,
                DataTable? row, CancellationToken cancellationToken = default)
            {
                var call = new Call(tableName, fieldName, value, row);
                Calls.Add(call);
                return RespondAsync?.Invoke(call) ?? Task.FromResult(Respond(call));
            }
        }

        private sealed class FakeResponse : IFieldValueChangeResponse
        {
            public DataTable? Values { get; init; }
            public Dictionary<string, string> ErrorsMap { get; } = [];
            public Dictionary<string, string> WarningsMap { get; } = [];
            public IReadOnlyDictionary<string, string> Errors => ErrorsMap;
            public IReadOnlyDictionary<string, string> Warnings => WarningsMap;
        }
    }
}
