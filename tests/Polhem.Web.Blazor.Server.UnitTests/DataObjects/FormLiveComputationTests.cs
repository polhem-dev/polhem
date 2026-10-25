using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.UnitTests.DataObjects
{
    /// <summary>
    /// The Blazor <see cref="FormLiveComputation"/>: its defaults over the shared engine.
    /// </summary>
    public class FormLiveComputationTests
    {
        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Order", "Order");
            var fields = schema.Tables!.Add("Order", "Order").Fields!;
            fields.Add(new FormField("price", "Price", FieldDbType.Decimal) { NumberKind = NumberKind.UnitPrice });
            fields.Add(new FormField("qty", "Qty", FieldDbType.Decimal) { NumberKind = NumberKind.Quantity });
            fields.Add(new FormField("amount", "Amount", FieldDbType.Decimal)
            {
                NumberKind = NumberKind.Amount,
                ValueExpression = "price * qty",
            });
            return schema;
        }

        [Fact]
        [DisplayName("With no evaluator or time zone given, Recompute uses the shared default evaluator")]
        public void Recompute_Defaults_ComputesField()
        {
            var live = new FormLiveComputation(BuildSchema());
            var table = new DataTable("Order");
            table.Columns.Add("price", typeof(decimal));
            table.Columns.Add("qty", typeof(decimal));
            table.Columns.Add("amount", typeof(decimal));
            var row = table.Rows.Add(2.125m, 1m, 0m);

            var changed = live.Recompute("Order", "price", row);

            Assert.Equal(2.13m, row["amount"]);
            Assert.Equal(["amount"], changed);
        }
    }
}
