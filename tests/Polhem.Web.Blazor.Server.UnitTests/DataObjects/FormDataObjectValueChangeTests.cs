using System.ComponentModel;
using System.Data;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.Form;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.UI.Core.Fields;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.UnitTests.DataObjects
{
    /// <summary>
    /// How the Blazor <see cref="FormDataObject"/> hands field changes to the server callback. The connector answers at
    /// once, so each callback has been applied by the time the edit returns. The callback rules themselves are covered
    /// by <c>FieldValueChangeCoordinatorTests</c>.
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
            table.Fields!.Add(new FormField("unit_price", "Unit Price", FieldDbType.Decimal) { ValueChangeCallback = true });
            return schema;
        }

        private static DataRow BuildPickedRow(Guid rowId)
        {
            var table = new DataTable("Customer");
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Columns.Add(SysFields.Id, typeof(string));
            table.Rows.Add(rowId, "C001");
            return table.Rows[0];
        }

        [Fact]
        [DisplayName("A lookup pick makes one call after the pick is written, and the returned values never call back")]
        public void LookupPick_CallsBackOnce_AndWritesValues()
        {
            var connector = new FakeConnector();
            var dataObject = new FormDataObject(BuildSchema(), connector);
            dataObject.InitializeNewMaster();
            var origins = new List<(string Field, FieldValueOrigin Origin)>();
            dataObject.FieldValueChanged += (_, e) => origins.Add((e.FieldName, e.Origin));
            var customer = Guid.NewGuid();

            dataObject.ApplyLookupSelection(dataObject.GetFormField("customer_rowid")!, BuildPickedRow(customer));

            var call = Assert.Single(connector.Calls);
            Assert.Equal(("customer_rowid", customer.ToString()), (call.FieldName, call.Value));
            Assert.Equal("C001", call.Row!.Rows[0]["ref_customer_id"]);
            Assert.Equal("Alfreds", dataObject.GetField("ref_customer_name"));
            Assert.Equal(9.5m, dataObject.MasterRow!["unit_price"]);
            Assert.Contains(("customer_rowid", FieldValueOrigin.LookupSelection), origins);
            Assert.Contains(("unit_price", FieldValueOrigin.ValueChangeResult), origins);
        }

        [Fact]
        [DisplayName("A row edit session calls back while editing, and its commit republishes without calling back again")]
        public void RowEditSession_CallsBackOnceAcrossCommit()
        {
            var connector = new FakeConnector();
            var dataObject = new FormDataObject(BuildSchema(), connector);
            dataObject.InitializeNewMaster();
            var row = dataObject.MasterRow!;
            var commitOrigins = new List<FieldValueOrigin>();

            dataObject.BeginRowEdit(row);
            dataObject.SetField(row, "customer_rowid", Guid.NewGuid().ToString());
            dataObject.FieldValueChanged += (_, e) => commitOrigins.Add(e.Origin);
            dataObject.CommitRowEdit(row);

            Assert.Single(connector.Calls);
            Assert.Equal("Alfreds", dataObject.GetField("ref_customer_name"));
            Assert.All(commitOrigins, origin => Assert.Equal(FieldValueOrigin.RowEditCommit, origin));
        }

        [Fact]
        [DisplayName("Clearing a lookup does not call back, and the server's messages reach FieldValueChangeApplied")]
        public void LookupClear_DoesNotCallBack_MessagesAreRaised()
        {
            var connector = new FakeConnector();
            var dataObject = new FormDataObject(BuildSchema(), connector);
            dataObject.InitializeNewMaster();
            FieldValueChangeAppliedEventArgs? applied = null;
            dataObject.FieldValueChangeApplied += (_, e) => applied = e;

            dataObject.ClearLookupSelection(dataObject.GetFormField("customer_rowid")!);
            Assert.Empty(connector.Calls);

            connector.Error = "Check the price list.";
            dataObject.SetField("unit_price", "3");
            Assert.Equal("Check the price list.", applied!.Errors["unit_price"]);
        }

        private sealed class FakeConnector : FormApiConnector
        {
            public FakeConnector()
                : base(Polhem.Api.Client.PolhemApiClient.CreateLocal(Polhem.Tests.Shared.EmptyServiceProvider.Instance), "Order") { }

            public List<FieldValueChangeRequest> Calls { get; } = [];

            public string? Error { get; set; }

            public override Task<FieldValueChangeResponse> FieldValueChangeAsync(string tableName, string fieldName,
                string value, DataTable? row, CancellationToken cancellationToken = default)
            {
                Calls.Add(new FieldValueChangeRequest { TableName = tableName, FieldName = fieldName, Value = value, Row = row });
                var values = new DataTable("Values");
                values.Columns.Add("ref_customer_name", typeof(string));
                values.Columns.Add("unit_price", typeof(decimal));
                values.Rows.Add("Alfreds", 9.5m);
                var response = new FieldValueChangeResponse { Values = values };
                if (Error is not null) { response.Errors[fieldName] = Error; }
                return Task.FromResult(response);
            }
        }
    }
}
