using System.ComponentModel;
using System.Data;
using Polhem.Base;
using Polhem.Base.Data;
using Polhem.Base.Exceptions;
using Polhem.Definition.Forms;
using Polhem.Definition.Settings;
using Polhem.Expressions;

namespace Polhem.Definition.UnitTests.Forms
{
    /// <summary>
    /// Tests for <see cref="FormExpressionCalculator"/>: row-level computation (including RoundByKind rounding and declaration-order chains),
    /// default values that fill only empty columns, reporting only the columns that actually changed, and the "source column → computed column"
    /// dependency map. Uses the real <see cref="DynamicExpressoEvaluator"/>, pure logic with no database.
    /// </summary>
    public class FormExpressionCalculatorTests
    {
        private readonly FormExpressionCalculator _calculator = new(new DynamicExpressoEvaluator());

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
            table.Fields!.Add(new FormField("tax", "Tax", FieldDbType.Currency)
            {
                NumberKind = NumberKind.Amount,
                ValueExpression = "amount * 0.05m",
                ReadOnly = true,
            });
            table.Fields!.Add(new FormField("order_date", "OrderDate", FieldDbType.DateTime)
            {
                DefaultValueExpression = "Today()",
            });
            table.Fields!.Add(new FormField("status", "Status", FieldDbType.String));
            return schema;
        }

        private static DataTable BuildOrderTable(decimal price, decimal qty,
            object? amount = null, object? tax = null, object? orderDate = null)
        {
            var table = new DataTable("Order");
            table.Columns.Add("sys_rowid", typeof(Guid));
            table.Columns.Add("price", typeof(decimal));
            table.Columns.Add("qty", typeof(decimal));
            table.Columns.Add("amount", typeof(decimal));
            table.Columns.Add("tax", typeof(decimal));
            table.Columns.Add("order_date", typeof(DateTime));
            table.Columns.Add("status", typeof(string));

            var row = table.NewRow();
            row["price"] = price;
            row["qty"] = qty;
            if (amount != null) { row["amount"] = amount; }
            if (tax != null) { row["tax"] = tax; }
            if (orderDate != null) { row["order_date"] = orderDate; }
            row["status"] = "Draft";
            table.Rows.Add(row);
            return table;
        }

        // UTC+14: the current time in the user's time zone is always 14 hours from the current UTC time, so assertions on the two bases cannot coincide.
        private const string Kiritimati = "Pacific/Kiritimati";

        private static FormSchema BuildStampSchema()
        {
            var schema = new FormSchema("Stamp", "Stamp") { CategoryId = "company" };
            var table = schema.Tables!.Add("Stamp", "Stamp");
            table.Fields!.Add(new FormField("created_at", "CreatedAt", FieldDbType.DateTime)
            {
                DefaultValueExpression = "Now()",
            });
            table.Fields!.Add(new FormField("touched_at", "TouchedAt", FieldDbType.DateTime)
            {
                ValueExpression = "Now()",
                ReadOnly = true,
            });
            table.Fields!.Add(new FormField("stamp_date", "StampDate", FieldDbType.Date)
            {
                DefaultValueExpression = "Today()",
            });
            return schema;
        }

        private static DataSet BuildStampDataSet()
        {
            var table = new DataTable("Stamp");
            table.Columns.Add("created_at", typeof(DateTime));
            table.Columns.Add("touched_at", typeof(DateTime));
            table.Columns.Add("stamp_date", typeof(DateTime));
            table.Rows.Add(table.NewRow());   // RowState = Added
            var dataSet = new DataSet("Stamp");
            dataSet.Tables.Add(table);
            return dataSet;
        }

        [Fact]
        [DisplayName("ApplyFieldExpressions (server save): Now() uses UTC as its basis while Today() still takes today in the user's time zone")]
        public void ApplyFieldExpressions_NowIsUtcBasis_TodayIsUserDay()
        {
            // On a server save the DataSet is already UTC (ADR-032 D3). If `Now()` took the wall-clock time of the user's time zone,
            // writing it into a DateTime column would store user-zone time in a column that by convention holds UTC.
            var dataSet = BuildStampDataSet();
            var utcBefore = DateTime.UtcNow;

            _calculator.ApplyFieldExpressions(BuildStampSchema(), dataSet, new RoundingContext(), Kiritimati);

            var utcAfter = DateTime.UtcNow;
            var row = dataSet.Tables["Stamp"]!.Rows[0];
            Assert.InRange((DateTime)row["created_at"], utcBefore, utcAfter);
            Assert.InRange((DateTime)row["touched_at"], utcBefore, utcAfter);
            Assert.Equal(FrameworkClock.Today(Kiritimati).ToDateTime(TimeOnly.MinValue), (DateTime)row["stamp_date"]);
        }

        [Fact]
        [DisplayName("ValidateRules (server save): Now() in a rule is compared with UTC cells on the same basis")]
        public void ValidateRules_NowComparesAgainstUtcCells()
        {
            // The cell is one hour after UTC now, so a UTC comparison judges it later than now and blocks it.
            // If `Now()` took the UTC+14 wall-clock time, this row would be misjudged as earlier than now and let through.
            var schema = BuildStampSchema();
            schema.Rules!.Add("created_not_future", "created_at <= Now()", "建立時間不得晚於現在");
            var dataSet = BuildStampDataSet();
            dataSet.Tables["Stamp"]!.Rows[0]["created_at"] =
                DateTime.SpecifyKind(DateTime.UtcNow.AddHours(1), DateTimeKind.Unspecified);

            Assert.Throws<UserMessageException>(() =>
                _calculator.ValidateRules(schema, dataSet, FormRuleTrigger.BeforeSave, Kiritimati));
        }

        [Fact]
        [DisplayName("ApplyDefaultRow (client preview): Now() uses the user's time zone as its basis, because the client DataSet is shown in the user's time zone")]
        public void ApplyDefaultRow_NowIsUserZoneBasis()
        {
            var schema = BuildStampSchema();
            var row = BuildStampDataSet().Tables["Stamp"]!.Rows[0];
            var before = FrameworkClock.Now(Kiritimati);

            _calculator.ApplyDefaultRow(schema.MasterTable!, row, Kiritimati);

            var after = FrameworkClock.Now(Kiritimati);
            Assert.InRange((DateTime)row["created_at"], before, after);
        }

        [Fact]
        [DisplayName("ApplyComputedRow fills amount = price * qty and reports the changed column")]
        public void ApplyComputedRow_ComputesAndReportsChanged()
        {
            var schema = BuildOrderSchema();
            var table = BuildOrderTable(price: 10m, qty: 3m);

            var changed = _calculator.ApplyComputedRow(schema, schema.MasterTable!, table.Rows[0], new RoundingContext());

            Assert.Equal(30m, table.Rows[0]["amount"]);
            Assert.Contains("amount", changed);
        }

        [Fact]
        [DisplayName("ApplyComputedRow chains in declaration order: tax depends on amount and sees the updated amount in the same evaluation")]
        public void ApplyComputedRow_ChainsInDeclarationOrder()
        {
            var schema = BuildOrderSchema();
            var table = BuildOrderTable(price: 100m, qty: 2m);

            _calculator.ApplyComputedRow(schema, schema.MasterTable!, table.Rows[0], new RoundingContext());

            // The amount is 200 and the tax is 200 * 0.05 = 10, because tax sees the amount computed in the same pass, not the old value.
            Assert.Equal(200m, table.Rows[0]["amount"]);
            Assert.Equal(10m, table.Rows[0]["tax"]);
        }

        [Fact]
        [DisplayName("ApplyComputedRow rounds the Amount kind to 2 places by NumberKind (away from zero)")]
        public void ApplyComputedRow_RoundsByNumberKind()
        {
            var schema = BuildOrderSchema();
            // 2.125 * 1 = 2.125, and Amount rounds to 2 places away from zero, giving 2.13.
            var table = BuildOrderTable(price: 2.125m, qty: 1m);

            _calculator.ApplyComputedRow(schema, schema.MasterTable!, table.Rows[0], new RoundingContext());

            Assert.Equal(2.13m, table.Rows[0]["amount"]);
        }

        [Fact]
        [DisplayName("ApplyComputedRow does not report unchanged values (compare first, to avoid event noise)")]
        public void ApplyComputedRow_NoChange_ReturnsEmpty()
        {
            var schema = BuildOrderSchema();
            // Pre-fill `amount` and `tax` with the computed results, so computing again changes nothing.
            var table = BuildOrderTable(price: 10m, qty: 3m, amount: 30m, tax: 1.5m);

            var changed = _calculator.ApplyComputedRow(schema, schema.MasterTable!, table.Rows[0], new RoundingContext());

            Assert.Empty(changed);
        }

        [Fact]
        [DisplayName("ApplyDefaultRow fills empty columns from expressions and reports them, without overwriting existing values")]
        public void ApplyDefaultRow_FillsOnlyEmpty()
        {
            var schema = BuildOrderSchema();
            var table = BuildOrderTable(price: 1m, qty: 1m);

            var changed = _calculator.ApplyDefaultRow(schema.MasterTable!, table.Rows[0]);

            // UTC, not `DateTime.Today`: the framework's date default is `UtcNow.Date` (ADR-032 D12).
            // Asserting the local date would always fail locally between 00:00 and 08:00 in UTC+8, and CI running in UTC would never see it.
            Assert.Equal(DateTime.UtcNow.Date, table.Rows[0]["order_date"]);
            Assert.Contains("order_date", changed);

            // The second call finds `order_date` already set, so it neither overwrites nor reports it.
            var again = _calculator.ApplyDefaultRow(schema.MasterTable!, table.Rows[0]);
            Assert.Empty(again);
        }

        [Fact]
        [DisplayName("Expressions still resolve when column casing differs from the declaration: variables are keyed by the declared field name, not the DataColumn name")]
        public void ApplyComputedRow_UppercaseColumnNames_StillResolvesIdentifiers()
        {
            // Columns are deliberately cased differently from the declared (lower-case) field names.
            // DynamicExpresso identifiers are case-sensitive, so this proves the variable map is keyed by
            // the schema field rather than the column. `AddColumn` lowercases column names since ADR-029,
            // which makes the two casings agree — testing with lower-case columns would therefore pass
            // either way and prove nothing.
            var schema = BuildOrderSchema();
            var table = new DataTable("Order");
            table.Columns.Add("PRICE", typeof(decimal));
            table.Columns.Add("QTY", typeof(decimal));
            table.Columns.Add("AMOUNT", typeof(decimal));
            table.Columns.Add("TAX", typeof(decimal));
            table.Columns.Add("STATUS", typeof(string));
            var row = table.NewRow();
            row["PRICE"] = 10m;
            row["QTY"] = 3m;
            row["STATUS"] = "Draft";
            table.Rows.Add(row);

            var ex = Record.Exception(() =>
                _calculator.ApplyComputedRow(schema, schema.MasterTable!, table.Rows[0], new RoundingContext()));

            Assert.Null(ex);
            Assert.Equal(30m, table.Rows[0]["amount"]);   // DataRow lookup is case-insensitive
            Assert.Equal(1.5m, table.Rows[0]["tax"]);
        }

        [Fact]
        [DisplayName("ValidateRules still resolves rule identifiers when column casing differs from the declaration")]
        public void ValidateRules_UppercaseColumnNames_StillResolvesIdentifiers()
        {
            var schema = BuildOrderSchema();
            schema.Rules!.Add("amount_positive", "amount > 0", "金額必須大於 0");
            var table = new DataTable("Order");
            table.Columns.Add("PRICE", typeof(decimal));
            table.Columns.Add("QTY", typeof(decimal));
            table.Columns.Add("AMOUNT", typeof(decimal));
            table.Columns.Add("TAX", typeof(decimal));
            table.Columns.Add("STATUS", typeof(string));
            table.Rows.Add(10m, 2m, 20m, 1m, "Draft");
            var dataSet = new DataSet("Order");
            dataSet.Tables.Add(table);

            var ex = Record.Exception(() =>
                _calculator.ValidateRules(schema, dataSet, FormRuleTrigger.BeforeSave));

            Assert.Null(ex);   // amount=20 > 0 → passes; must not throw UnknownIdentifier
        }

        [Fact]
        [DisplayName("BuildDependencyMap maps price / qty to amount and amount to tax (source column to the computed columns it affects)")]
        public void BuildDependencyMap_MapsSourceToComputed()
        {
            var schema = BuildOrderSchema();

            var map = _calculator.BuildDependencyMap(schema.MasterTable!);

            Assert.Contains("amount", map["price"]);
            Assert.Contains("amount", map["qty"]);
            Assert.Contains("tax", map["amount"]);
            // `status` is not a source of any computed field.
            Assert.False(map.ContainsKey("status"));
        }

        [Fact]
        [DisplayName("BuildDependencyMap source column keys are case-insensitive (matching DataTable column lookup)")]
        public void BuildDependencyMap_KeysAreCaseInsensitive()
        {
            var schema = BuildOrderSchema();

            var map = _calculator.BuildDependencyMap(schema.MasterTable!);

            Assert.True(map.ContainsKey("PRICE"));
            Assert.True(map.ContainsKey("Qty"));
        }

        private static FormSchema BuildPackingSchema(NumberKind kind, string unitField)
        {
            var schema = new FormSchema("Packing", "Packing") { CategoryId = "company" };
            var table = schema.Tables!.Add("Packing", "Packing");
            table.Fields!.Add(new FormField("qty", "Qty", FieldDbType.Decimal));
            table.Fields!.Add(new FormField("uom", "Uom", FieldDbType.String));
            table.Fields!.Add(new FormField("packed", "Packed", FieldDbType.Decimal)
            {
                NumberKind = kind,
                UnitField = unitField,
                ValueExpression = "qty * 1.23456m",
                ReadOnly = true,
            });
            return schema;
        }

        private static DataTable BuildPackingTable(decimal qty, string uom)
        {
            var table = new DataTable("Packing");
            table.Columns.Add("qty", typeof(decimal));
            table.Columns.Add("uom", typeof(string));
            table.Columns.Add("packed", typeof(decimal));
            var row = table.NewRow();
            row["qty"] = qty;
            row["uom"] = uom;
            table.Rows.Add(row);
            return table;
        }

        private static UnitSettings KilogramOnly() => [new UnitItem("KG", 3, "weight", "Kilogram")];

        [Theory]
        [InlineData(NumberKind.Quantity)]
        [InlineData(NumberKind.Weight)]
        [DisplayName("ApplyComputedRow throws InvalidOperationException for a quantity/weight computed field without a UnitField")]
        public void ApplyComputedRow_UnitKindWithoutUnitField_Throws(NumberKind kind)
        {
            var schema = BuildPackingSchema(kind, unitField: string.Empty);
            var table = BuildPackingTable(qty: 2m, uom: "KG");
            var ctx = new RoundingContext { UnitSettings = KilogramOnly() };

            Assert.Throws<InvalidOperationException>(() =>
                _calculator.ApplyComputedRow(schema, schema.MasterTable!, table.Rows[0], ctx));
        }

        [Fact]
        [DisplayName("ApplyComputedRow does not throw for a quantity input field (not computed) without a UnitField")]
        public void ApplyComputedRow_UnboundQuantityInputField_DoesNotThrow()
        {
            // qty is a Quantity input field with no UnitField; only computed fields are checked.
            var schema = BuildOrderSchema();
            var table = BuildOrderTable(price: 10m, qty: 3m);

            var ex = Record.Exception(() =>
                _calculator.ApplyComputedRow(schema, schema.MasterTable!, table.Rows[0], new RoundingContext()));

            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("ApplyComputedRow does not round the result when a unit is bound but the row's unit is empty")]
        public void ApplyComputedRow_EmptyRowUnit_NotRounded()
        {
            var schema = BuildPackingSchema(NumberKind.Weight, unitField: "uom");
            var table = BuildPackingTable(qty: 2m, uom: string.Empty);
            var ctx = new RoundingContext { UnitSettings = KilogramOnly() };

            _calculator.ApplyComputedRow(schema, schema.MasterTable!, table.Rows[0], ctx);

            Assert.Equal(2.46912m, table.Rows[0]["packed"]);
        }

        [Fact]
        [DisplayName("ApplyComputedRow rounds to 3 places by unit when a unit is bound and the row's unit is KG")]
        public void ApplyComputedRow_RowUnit_RoundsByUnit()
        {
            var schema = BuildPackingSchema(NumberKind.Weight, unitField: "uom");
            var table = BuildPackingTable(qty: 2m, uom: "KG");
            var ctx = new RoundingContext { UnitSettings = KilogramOnly() };

            _calculator.ApplyComputedRow(schema, schema.MasterTable!, table.Rows[0], ctx);

            // 2 * 1.23456 = 2.46912, and KG rounds to 3 places, giving 2.469.
            Assert.Equal(2.469m, table.Rows[0]["packed"]);
        }
    }
}
