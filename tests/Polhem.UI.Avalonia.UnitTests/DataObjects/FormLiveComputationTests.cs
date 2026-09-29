using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.DataObjects
{
    /// <summary>
    /// <see cref="FormLiveComputation"/> tests: live recompute and write-back, dependency graph gating (fields without dependents and the computed field itself do not trigger),
    /// the re-entrancy guard, and applying defaults. The client uses the same <see cref="FormExpressionCalculator"/> as the server, so the same
    /// input produces the same values (compare the assertions of the server-side <c>FormRuleProcessorTests</c>).
    /// </summary>
    public class FormLiveComputationTests
    {
        private static FormSchema BuildOrderSchema()
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add(new FormField("sys_rowid", "RowId", FieldDbType.Guid));
            table.Fields!.Add(new FormField("price", "Price", FieldDbType.Currency) { NumberKind = NumberKind.UnitPrice });
            table.Fields!.Add(new FormField("qty", "Qty", FieldDbType.Decimal) { NumberKind = NumberKind.Quantity });
            table.Fields!.Add(new FormField("amount", "Amount", FieldDbType.Currency)
            {
                NumberKind = NumberKind.Amount,
                ValueExpression = "price * qty",
                ReadOnly = true,
            });
            table.Fields!.Add(new FormField("order_date", "OrderDate", FieldDbType.DateTime)
            {
                DefaultValueExpression = "Today()",
            });
            table.Fields!.Add(new FormField("status", "Status", FieldDbType.String));
            return schema;
        }

        private static DataTable BuildOrderTable(decimal price, decimal qty, object? amount = null)
        {
            var table = new DataTable("Order");
            table.Columns.Add("sys_rowid", typeof(Guid));
            table.Columns.Add("price", typeof(decimal));
            table.Columns.Add("qty", typeof(decimal));
            table.Columns.Add("amount", typeof(decimal));
            table.Columns.Add("order_date", typeof(DateTime));
            table.Columns.Add("status", typeof(string));

            var row = table.NewRow();
            row["price"] = price;
            row["qty"] = qty;
            if (amount != null) { row["amount"] = amount; }
            row["status"] = "Draft";
            table.Rows.Add(row);
            return table;
        }

        [Fact]
        [DisplayName("Recompute: editing qty recomputes and writes back amount (the same value as the server's price*qty)")]
        public void Recompute_OnSourceChange_WritesComputedField()
        {
            var live = new FormLiveComputation(BuildOrderSchema());
            var table = BuildOrderTable(price: 10m, qty: 3m);

            var changed = live.Recompute("Order", "qty", table.Rows[0]);

            Assert.Equal(30m, table.Rows[0]["amount"]);
            Assert.Contains("amount", changed);
        }

        [Fact]
        [DisplayName("Recompute: an Amount kind rounds to 2 decimals by NumberKind (the Tier 1 framework default)")]
        public void Recompute_RoundsByFrameworkDefaultDecimals()
        {
            var live = new FormLiveComputation(BuildOrderSchema());
            var table = BuildOrderTable(price: 2.125m, qty: 1m);

            live.Recompute("Order", "price", table.Rows[0]);

            Assert.Equal(2.13m, table.Rows[0]["amount"]);
        }

        [Fact]
        [DisplayName("Recompute: changing a field with no dependents (status) does not recompute and reports nothing")]
        public void Recompute_NonSourceField_NoOp()
        {
            var live = new FormLiveComputation(BuildOrderSchema());
            var table = BuildOrderTable(price: 10m, qty: 3m);

            var changed = live.Recompute("Order", "status", table.Rows[0]);

            Assert.Empty(changed);
            Assert.Equal(DBNull.Value, table.Rows[0]["amount"]);
        }

        [Fact]
        [DisplayName("Recompute: a change to the computed field itself is not a trigger (reports nothing)")]
        public void Recompute_ComputedFieldChange_NotATrigger()
        {
            var live = new FormLiveComputation(BuildOrderSchema());
            var table = BuildOrderTable(price: 10m, qty: 3m);

            var changed = live.Recompute("Order", "amount", table.Rows[0]);

            Assert.Empty(changed);
        }

        [Fact]
        [DisplayName("Recompute: IsRecomputing is false before and after a recompute (the re-entrancy guard flag)")]
        public void Recompute_WhileRecomputing_IsGuarded()
        {
            var live = new FormLiveComputation(BuildOrderSchema());
            var table = BuildOrderTable(price: 10m, qty: 3m);

            Assert.False(live.IsRecomputing);
            var changed = live.Recompute("Order", "qty", table.Rows[0]);
            Assert.Contains("amount", changed);
            Assert.False(live.IsRecomputing);
        }

        [Fact]
        [DisplayName("ApplyDefaults: empty fields of a new row are filled from DefaultValueExpression")]
        public void ApplyDefaults_FillsEmptyDefaultExpression()
        {
            // Read before the act as well as after it, so a run that crosses midnight cannot fail.
            var dayBefore = DateTime.UtcNow.Date;
            var live = new FormLiveComputation(BuildOrderSchema());
            var table = BuildOrderTable(price: 1m, qty: 1m);

            var changed = live.ApplyDefaults("Order", table.Rows[0]);

            // UTC, not `DateTime.Today`: the framework's date default is `UtcNow.Date` (ADR-032 D12).
            // Asserting the local date always fails locally between 00:00 and 08:00 at UTC+8, which CI running in UTC never sees.
            Assert.InRange((DateTime)table.Rows[0]["order_date"], dayBefore, DateTime.UtcNow.Date);
            Assert.Contains("order_date", changed);
        }

        [Fact]
        [DisplayName("ApplyDefaults: a DefaultValueExpression replaces the value FormRowDefaults seeded on a new row")]
        public void ApplyDefaults_SeededRow_ExpressionReplacesSeed()
        {
            var schema = BuildOrderSchema();
            schema.MasterTable!.Fields!.Add(new FormField("priority", "Priority", FieldDbType.Integer)
            {
                DefaultValueExpression = "3",
            });
            var live = new FormLiveComputation(schema);
            var table = BuildOrderTable(price: 1m, qty: 1m);
            table.Columns.Add("priority", typeof(int));
            var row = table.NewRow();
            FormRowDefaults.Apply(schema.MasterTable!, row);
            table.Rows.Add(row);
            Assert.Equal(0, row["priority"]);

            var changed = live.ApplyDefaults("Order", row);

            Assert.Equal(3, row["priority"]);
            Assert.Contains("priority", changed);
        }

        [Fact]
        [DisplayName("Northwind repro: a string-typed Guid key column does not crash the numeric computed field (the wire and SQLite store GUIDs as TEXT)")]
        public void Recompute_StringTypedGuidKeyColumn_DoesNotThrow()
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var detail = schema.Tables!.Add("OrderDetail", "Order Details");
            detail.Fields!.Add(new FormField("product_rowid", "Product", FieldDbType.Guid));
            detail.Fields.Add(new FormField("quantity", "Quantity", FieldDbType.Decimal) { NumberKind = NumberKind.Quantity });
            detail.Fields.Add(new FormField("unit_price", "Unit Price", FieldDbType.Currency) { NumberKind = NumberKind.UnitPrice });
            detail.Fields.Add(new FormField("discount", "Discount", FieldDbType.Decimal));
            detail.Fields.Add(new FormField("amount", "Amount", FieldDbType.Currency)
            {
                NumberKind = NumberKind.Amount,
                ValueExpression = "quantity * unit_price * (1 - discount)",
                ReadOnly = true,
            });
            var live = new FormLiveComputation(schema);

            // product_rowid arrives as a *string* column (SQLite stores GUIDs as TEXT and the wire keeps
            // that), even though the schema field is Guid — the exact shape that crashed the demo. An
            // *empty* product_rowid (a line with no product selected yet) must coerce to Guid.Empty, not
            // throw from Guid.Parse("") — otherwise the recompute degrades and stops previewing.
            var data = new DataTable("OrderDetail");
            data.Columns.Add("product_rowid", typeof(string));
            data.Columns.Add("quantity", typeof(decimal));
            data.Columns.Add("unit_price", typeof(decimal));
            data.Columns.Add("discount", typeof(decimal));
            data.Columns.Add("amount", typeof(decimal));
            data.Rows.Add(string.Empty, 10m, 14m, 0.05m, 0m);

            var exception = Record.Exception(() => live.Recompute("OrderDetail", "quantity", data.Rows[0]));

            Assert.Null(exception);
            Assert.False(live.IsDegraded);
            // 10 * 14 * (1 - 0.05) = 133.00
            Assert.Equal(133m, data.Rows[0]["amount"]);
        }

        [Fact]
        [DisplayName("Column names cased differently from the declared field names still recompute without degrading (identifier casing)")]
        public void Recompute_UppercaseColumnNames_StillRecomputes()
        {
            var live = new FormLiveComputation(BuildOrderSchema());
            // `AddColumn` stores lowercase names (ADR-029), which happen to equal the declared field names. Uppercase
            // column names are used here so the test still proves the variable table is keyed by
            // `FormField.FieldName` and not by whatever casing the DataSet holds; the change event carries that casing too.
            var table = new DataTable("Order");
            table.Columns.Add("SYS_ROWID", typeof(Guid));
            table.Columns.Add("PRICE", typeof(decimal));
            table.Columns.Add("QTY", typeof(decimal));
            table.Columns.Add("AMOUNT", typeof(decimal));
            table.Columns.Add("ORDER_DATE", typeof(DateTime));
            table.Columns.Add("STATUS", typeof(string));
            var row = table.NewRow();
            row["PRICE"] = 10m;
            row["QTY"] = 3m;
            row["STATUS"] = "Draft";
            table.Rows.Add(row);

            var changed = live.Recompute("Order", "QTY", table.Rows[0]);

            Assert.False(live.IsDegraded);
            Assert.Equal(30m, table.Rows[0]["amount"]);
            Assert.Contains("amount", changed);
        }

        [Fact]
        [DisplayName("Graceful degrade: a failed expression evaluation does not throw, disables the preview, and later recomputes are no-ops")]
        public void Recompute_EvaluationFailure_DegradesGracefully()
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add(new FormField("qty", "Qty", FieldDbType.Decimal));
            // A reference to a missing field makes evaluation fail to parse (unknown identifier).
            table.Fields!.Add(new FormField("amount", "Amount", FieldDbType.Currency)
            {
                ValueExpression = "qty * nonexistent_field",
                ReadOnly = true,
            });
            var live = new FormLiveComputation(schema);

            var data = new DataTable("Order");
            data.Columns.Add("qty", typeof(decimal));
            data.Columns.Add("amount", typeof(decimal));
            data.Rows.Add(3m, 0m);

            // A failed evaluation must not throw: the exception would spread into the ADO.NET events and break the form.
            var exception = Record.Exception(() => live.Recompute("Order", "qty", data.Rows[0]));
            Assert.Null(exception);
            Assert.True(live.IsDegraded);

            var again = live.Recompute("Order", "qty", data.Rows[0]);
            Assert.Empty(again);
        }

        [Fact]
        [DisplayName("Recompute: field names are case-insensitive (the event's column name casing may differ from the schema)")]
        public void Recompute_FieldNameCaseInsensitive()
        {
            var live = new FormLiveComputation(BuildOrderSchema());
            var table = BuildOrderTable(price: 10m, qty: 3m);

            var changed = live.Recompute("Order", "QTY", table.Rows[0]);

            Assert.Equal(30m, table.Rows[0]["amount"]);
            Assert.Contains("amount", changed);
        }
    }
}
