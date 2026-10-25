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

        // Column names deliberately differ in case from the declared field names, so the tests do not depend on
        // the casing the data set happens to store.
        private static DataRow BuildUpperCaseRow()
        {
            var table = new DataTable("Order");
            table.Columns.Add("PRICE", typeof(decimal));
            table.Columns.Add("QTY", typeof(decimal));
            table.Columns.Add("AMOUNT", typeof(decimal));
            table.Columns.Add("ORDER_DATE", typeof(DateTime));
            var row = table.NewRow();
            row["PRICE"] = 10m;
            row["QTY"] = 3m;
            table.Rows.Add(row);
            return row;
        }

        [Fact]
        [DisplayName("A decimal field multiplied by a double literal degrades and names the field, the expression and the reason")]
        public void Recompute_TypeMismatchedExpression_ExposesDegradationWithFieldAndExpression()
        {
            var engine = new FormLiveComputationEngine(BuildSchema("price * 1.05"), s_evaluator, () => string.Empty);

            engine.Recompute("Order", "price", BuildUpperCaseRow());

            var degradation = Assert.IsType<LiveComputationDegradation>(engine.Degradation);
            Assert.True(engine.IsDegraded);
            Assert.Equal("amount", degradation.Field?.FieldName);
            Assert.Equal("Amount", degradation.DisplayText);
            Assert.Equal("price * 1.05", degradation.Expression);
            Assert.Equal("Order", degradation.Table.TableName);
            Assert.Contains("'System.Decimal' and 'System.Double'", degradation.Reason, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Degraded is raised once, after the failing pass has ended, however many edits follow")]
        public void Recompute_AfterDegrade_RaisesDegradedOnlyOnce()
        {
            var engine = new FormLiveComputationEngine(BuildSchema("price * 1.05"), s_evaluator, () => string.Empty);
            var raised = 0;
            var recomputingInHandler = true;
            engine.Degraded += (_, _) =>
            {
                raised++;
                recomputingInHandler = engine.IsRecomputing;
            };
            var row = BuildUpperCaseRow();

            engine.Recompute("Order", "price", row);
            engine.Recompute("Order", "qty", row);
            engine.InitializeNewRow("Order", BuildUpperCaseRow());

            Assert.Equal(1, raised);
            Assert.False(recomputingInHandler);
        }

        [Fact]
        [DisplayName("A failing default-value expression on a new row is traced to its field")]
        public void InitializeNewRow_BadDefaultValueExpression_ReportsDefaultField()
        {
            var schema = BuildSchema();
            schema.Tables!["Order"].Fields!["order_date"].DefaultValueExpression = "Today() * 2";
            var engine = new FormLiveComputationEngine(schema, s_evaluator, () => string.Empty);

            engine.InitializeNewRow("Order", BuildUpperCaseRow());

            Assert.Equal("order_date", engine.Degradation?.Field?.FieldName);
            Assert.Equal("Today() * 2", engine.Degradation?.Expression);
        }

        [Fact]
        [DisplayName("A cell value that cannot be converted degrades without naming a field, and the message names the table")]
        public void Recompute_MalformedGuidText_ReportsDegradationWithoutField()
        {
            var schema = new FormSchema("Order", "Order");
            var fields = schema.Tables!.Add("Order", "Orders").Fields!;
            fields.Add(new FormField("customer_rowid", "Customer", FieldDbType.Guid));
            fields.Add(new FormField("has_customer", "Has Customer", FieldDbType.Boolean)
            {
                ValueExpression = "customer_rowid != Guid.Empty",
            });
            var table = new DataTable("Order");
            table.Columns.Add("CUSTOMER_ROWID", typeof(string));
            table.Columns.Add("HAS_CUSTOMER", typeof(bool));
            var row = table.NewRow();
            row["CUSTOMER_ROWID"] = "not-a-guid";
            table.Rows.Add(row);
            var engine = new FormLiveComputationEngine(schema, s_evaluator, () => string.Empty);

            engine.Recompute("Order", "customer_rowid", row);

            var degradation = Assert.IsType<LiveComputationDegradation>(engine.Degradation);
            Assert.Null(degradation.Field);
            Assert.Null(degradation.Expression);
            Assert.IsType<FormatException>(degradation.Exception);
            Assert.Equal("Orders", degradation.DisplayText);
        }

        [Fact]
        [DisplayName("A decimal literal written with the m suffix multiplies a decimal field")]
        public void Recompute_DecimalLiteralSuffix_Computes()
        {
            var engine = new FormLiveComputationEngine(BuildSchema("price * 1.05m"), s_evaluator, () => string.Empty);
            var row = BuildUpperCaseRow();

            engine.Recompute("Order", "price", row);

            Assert.False(engine.IsDegraded);
            Assert.Equal(10.5m, row["AMOUNT"]);
        }

        [Fact]
        [DisplayName("The user message names the field and the reason, and leaves the expression out")]
        public void FormatMessage_NamesFieldAndReasonWithoutExpression()
        {
            var engine = new FormLiveComputationEngine(BuildSchema("price * 1.05"), s_evaluator, () => string.Empty);
            engine.Recompute("Order", "price", BuildUpperCaseRow());

            var message = engine.Degradation!.FormatMessage(new KeyEchoLocalizer());

            Assert.StartsWith("Amount is no longer calculated as you edit", message, StringComparison.Ordinal);
            Assert.Contains(engine.Degradation.Reason, message, StringComparison.Ordinal);
            Assert.DoesNotContain("1.05", message, StringComparison.Ordinal);
        }

        // Translates nothing, so the English default text is what the message is built from.
        private sealed class KeyEchoLocalizer : Microsoft.Extensions.Localization.IStringLocalizer
        {
            public Microsoft.Extensions.Localization.LocalizedString this[string name]
                => new(name, name, resourceNotFound: true);

            public Microsoft.Extensions.Localization.LocalizedString this[string name, params object[] arguments]
                => this[name];

            public IEnumerable<Microsoft.Extensions.Localization.LocalizedString> GetAllStrings(bool includeParentCultures)
                => [];
        }
    }
}
