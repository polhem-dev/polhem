using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Expressions;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// <see cref="FormLiveComputationEngine"/> tests for what the engine takes from its caller: the evaluator
    /// and the time zone. The recompute rules themselves are covered through the heads that drive the engine.
    /// </summary>
    public class FormLiveComputationEngineTests
    {
        private static readonly DynamicExpressoEvaluator s_evaluator = new();

        private static FormSchema BuildSchema(string amountExpression = "price * qty")
        {
            var schema = new FormSchema("Order", "Order");
            var fields = schema.Tables!.Add("Order", "Order").Fields!;
            fields.Add(new FormField("price", "Price", FieldDbType.Currency) { NumberKind = NumberKind.UnitPrice });
            fields.Add(new FormField("qty", "Qty", FieldDbType.Decimal) { NumberKind = NumberKind.Quantity });
            fields.Add(new FormField("amount", "Amount", FieldDbType.Currency)
            {
                NumberKind = NumberKind.Amount,
                ValueExpression = amountExpression,
            });
            fields.Add(new FormField("order_date", "Order Date", FieldDbType.Date) { DefaultValueExpression = "Today()" });
            return schema;
        }

        private static DataRow BuildRow()
        {
            var table = new DataTable("Order");
            table.Columns.Add("price", typeof(decimal));
            table.Columns.Add("qty", typeof(decimal));
            table.Columns.Add("amount", typeof(decimal));
            table.Columns.Add("order_date", typeof(DateTime));
            var row = table.NewRow();
            row["price"] = 10m;
            row["qty"] = 3m;
            table.Rows.Add(row);
            return row;
        }

        [Fact]
        [DisplayName("Recompute evaluates the computed field with the evaluator the caller supplied")]
        public void Recompute_UsesSuppliedEvaluator()
        {
            var engine = new FormLiveComputationEngine(BuildSchema(), s_evaluator, () => string.Empty);
            var row = BuildRow();

            var changed = engine.Recompute("Order", "qty", row);

            Assert.Equal(30m, row["amount"]);
            Assert.Contains("amount", changed);
        }

        [Fact]
        [DisplayName("The time zone provider is asked on every evaluation, so a later sign-in takes effect")]
        public void TimeZoneProvider_IsReadPerEvaluation()
        {
            var calls = 0;
            var engine = new FormLiveComputationEngine(BuildSchema(), s_evaluator, () => { calls++; return "Asia/Taipei"; });

            engine.ApplyDefaults("Order", BuildRow());
            engine.ApplyDefaults("Order", BuildRow());

            Assert.True(calls >= 2, $"The provider was called {calls} time(s).");
        }

        [Fact]
        [DisplayName("A failing expression degrades the engine to a no-op instead of throwing")]
        public void Recompute_BadExpression_Degrades()
        {
            var engine = new FormLiveComputationEngine(BuildSchema("price * missing_field"), s_evaluator, () => string.Empty);

            var changed = engine.Recompute("Order", "price", BuildRow());

            Assert.Empty(changed);
            Assert.True(engine.IsDegraded);
        }
    }
}
