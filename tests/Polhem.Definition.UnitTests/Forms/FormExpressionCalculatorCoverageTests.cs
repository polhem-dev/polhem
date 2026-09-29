using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Core.Exceptions;
using Polhem.Definition.Forms;
using Polhem.Expressions;

namespace Polhem.Definition.UnitTests.Forms
{
    /// <summary>
    /// Additional tests for <see cref="FormExpressionCalculator"/>: the per-RowState branches of the whole-table <c>ApplyFieldExpressions</c>,
    /// the rule filtering, When guard and <see cref="UserMessageException"/> on violation in <c>ValidateRules</c>,
    /// target table resolution, rounding reference code (currency / unit) resolution, and the early exits for null Fields. Uses the real
    /// <see cref="DynamicExpressoEvaluator"/>, entirely in memory with no database.
    /// </summary>
    public class FormExpressionCalculatorCoverageTests
    {
        private readonly FormExpressionCalculator _calculator = new(new DynamicExpressoEvaluator());

        private static FormSchema BuildComputeSchema()
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add(new FormField("sys_rowid", "RowId", FieldDbType.Guid));
            table.Fields!.Add(new FormField("price", "Price", FieldDbType.Currency));
            table.Fields!.Add(new FormField("qty", "Qty", FieldDbType.Decimal));
            table.Fields!.Add(new FormField("amount", "Amount", FieldDbType.Currency)
            {
                NumberKind = NumberKind.Amount,
                ValueExpression = "price * qty",
            });
            table.Fields!.Add(new FormField("status", "Status", FieldDbType.String)
            {
                DefaultValueExpression = "\"Draft\"",
            });
            return schema;
        }

        private static DataTable BuildComputeTable()
        {
            var table = new DataTable("Order");
            table.Columns.Add("sys_rowid", typeof(Guid));
            table.Columns.Add("price", typeof(decimal));
            table.Columns.Add("qty", typeof(decimal));
            table.Columns.Add("amount", typeof(decimal));
            table.Columns.Add("status", typeof(string));
            table.Columns.Add("extra_col", typeof(string));   // No matching schema field, so `BuildVariables` falls back.
            return table;
        }

        [Fact]
        [DisplayName("ApplyFieldExpressions returns without throwing when schema.Tables is empty")]
        public void ApplyFieldExpressions_EmptyTables_ReturnsEarly()
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };

            var ex = Record.Exception(() =>
                _calculator.ApplyFieldExpressions(schema, new DataSet(), new RoundingContext()));

            Assert.Null(ex);
            Assert.Empty(schema.Tables!);
        }

        [Fact]
        [DisplayName("ApplyFieldExpressions skips a table missing from the data set without throwing")]
        public void ApplyFieldExpressions_MissingDataTable_Skips()
        {
            var schema = BuildComputeSchema();

            var ex = Record.Exception(() =>
                _calculator.ApplyFieldExpressions(schema, new DataSet(), new RoundingContext()));

            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("ApplyFieldExpressions recomputes only Added / Modified rows, skips Unchanged and does not touch Deleted")]
        public void ApplyFieldExpressions_RowStates_AppliesToAddedAndModifiedOnly()
        {
            var schema = BuildComputeSchema();
            var table = BuildComputeTable();

            var unchanged = table.Rows.Add(Guid.NewGuid(), 1m, 1m, 99m, "Keep", "x");
            var modified = table.Rows.Add(Guid.NewGuid(), 5m, 2m, 0m, "Keep", "y");
            var deleted = table.Rows.Add(Guid.NewGuid(), 9m, 9m, 7m, "Keep", "z");
            table.AcceptChanges();                       // Everything becomes Unchanged.
            modified["price"] = 5m; modified["qty"] = 4m; // Now Modified, with a stale amount.
            deleted.Delete();                            // → Deleted
            var added = table.Rows.Add(Guid.NewGuid(), 3m, 3m, 0m, DBNull.Value, "w"); // → Added

            var dataSet = new DataSet();
            dataSet.Tables.Add(table);

            _calculator.ApplyFieldExpressions(schema, dataSet, new RoundingContext());

            Assert.Equal(20m, modified["amount"]);   // Recomputed as 5 * 4.
            Assert.Equal(9m, added["amount"]);       // Recomputed as 3 * 3.
            Assert.Equal("Draft", added["status"]);  // Added rows get default values.
            Assert.Equal(99m, unchanged["amount"]);  // Unchanged rows are not recomputed (the false branch).
        }

        [Fact]
        [DisplayName("ValidateRules returns without throwing when schema.Rules is empty")]
        public void ValidateRules_EmptyRules_ReturnsEarly()
        {
            var schema = BuildComputeSchema();

            var ex = Record.Exception(() =>
                _calculator.ValidateRules(schema, new DataSet(), FormRuleTrigger.BeforeSave));

            Assert.Null(ex);
            Assert.Empty(schema.Rules!);
        }

        [Fact]
        [DisplayName("ValidateRules evaluates only enabled rules with a matching trigger and skips Deleted rows")]
        public void ValidateRules_FiltersEnabledMatchingTrigger_AndSkipsDeletedRow()
        {
            var schema = BuildComputeSchema();
            var r1 = schema.Rules!.Add("r_price", "price >= 0", "price");
            r1.Order = 2;
            var r1b = schema.Rules!.Add("r_qty", "qty >= 0", "qty");
            r1b.Order = 1;
            var disabled = schema.Rules!.Add("r_dis", "price > 1000000", "disabled");
            disabled.Enabled = false;                        // Disabled, so not evaluated.
            var delTrigger = schema.Rules!.Add("r_del", "false", "wrong-trigger");
            delTrigger.Trigger = FormRuleTrigger.BeforeDelete; // Trigger does not match, so not evaluated.

            var table = BuildComputeTable();
            table.Rows.Add(Guid.NewGuid(), 10m, 2m, 20m, "Draft", "a");
            var deleted = table.Rows.Add(Guid.NewGuid(), 1m, 1m, 1m, "Draft", "b");
            table.AcceptChanges();
            deleted.Delete();                                // Deleted, so `ValidateRuleRows` skips it.
            var dataSet = new DataSet();
            dataSet.Tables.Add(table);

            var ex = Record.Exception(() =>
                _calculator.ValidateRules(schema, dataSet, FormRuleTrigger.BeforeSave));

            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("ValidateRules throws UserMessageException with the rule message when the condition fails")]
        public void ValidateRules_FailingCondition_ThrowsUserMessageException()
        {
            var schema = BuildComputeSchema();
            schema.Rules!.Add("r_fail", "amount > 100", "金額太小");
            var table = BuildComputeTable();
            table.Rows.Add(Guid.NewGuid(), 1m, 1m, 10m, "Draft", "a");
            var dataSet = new DataSet();
            dataSet.Tables.Add(table);

            var ex = Assert.Throws<UserMessageException>(() =>
                _calculator.ValidateRules(schema, dataSet, FormRuleTrigger.BeforeSave));

            Assert.Equal("金額太小", ex.Message);
        }

        [Fact]
        [DisplayName("ValidateRules skips the whole rule when its When guard is false (Condition is not evaluated)")]
        public void ValidateRules_WhenGuardFalse_SkipsRule()
        {
            var schema = BuildComputeSchema();
            var rule = schema.Rules!.Add("r_when", "false", "should-not-throw");
            rule.When = "status == \"Confirmed\"";   // The row status is Draft, so When is false and the rule is skipped.
            var table = BuildComputeTable();
            table.Rows.Add(Guid.NewGuid(), 1m, 1m, 10m, "Draft", "a");
            var dataSet = new DataSet();
            dataSet.Tables.Add(table);

            var ex = Record.Exception(() =>
                _calculator.ValidateRules(schema, dataSet, FormRuleTrigger.BeforeSave));

            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("ValidateRules resolves the target table: empty TargetTable means the master, an existing name means that table, a missing name is skipped")]
        public void ValidateRules_TargetTableResolution_HandlesAllCases()
        {
            var schema = BuildComputeSchema();
            var detail = schema.Tables!.Add("OrderLine", "Lines");
            detail.Fields!.Add(new FormField("line_qty", "LineQty", FieldDbType.Integer));

            schema.Rules!.Add("r_master", "price >= 0", "master");           // Empty TargetTable means the master.
            var rDetail = schema.Rules!.Add("r_detail", "line_qty >= 0", "detail");
            rDetail.TargetTable = "OrderLine";                               // An existing name.
            var rAbsent = schema.Rules!.Add("r_absent", "false", "absent");
            rAbsent.TargetTable = "NoSuchTable";                            // A missing name, so skipped.

            var master = BuildComputeTable();
            master.Rows.Add(Guid.NewGuid(), 10m, 2m, 20m, "Draft", "a");
            var lineTable = new DataTable("OrderLine");
            lineTable.Columns.Add("line_qty", typeof(int));
            lineTable.Rows.Add(3);
            var dataSet = new DataSet();
            dataSet.Tables.Add(master);
            dataSet.Tables.Add(lineTable);

            var ex = Record.Exception(() =>
                _calculator.ValidateRules(schema, dataSet, FormRuleTrigger.BeforeSave));

            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("ApplyComputedRow resolves the rounding reference code: field currency, schema currency, missing currency, unit, no reference code")]
        public void ApplyComputedRow_ResolvesRefCodeAcrossNumberKinds()
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company", CurrencyField = "currency" };
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add(new FormField("sys_rowid", "RowId", FieldDbType.Guid));
            table.Fields!.Add(new FormField("currency", "Currency", FieldDbType.String));
            table.Fields!.Add(new FormField("unit", "Unit", FieldDbType.String));
            table.Fields!.Add(new FormField("price", "Price", FieldDbType.Currency));
            table.Fields!.Add(new FormField("qty", "Qty", FieldDbType.Decimal));
            // Amount whose field has its own `CurrencyField`, which is among the variables.
            table.Fields!.Add(new FormField("amt_field_ccy", "AmtFieldCcy", FieldDbType.Currency)
            {
                NumberKind = NumberKind.Amount,
                CurrencyField = "currency",
                ValueExpression = "price * qty",
            });
            // Amount without a `CurrencyField`, so it falls back to `schema.CurrencyField`.
            table.Fields!.Add(new FormField("amt_schema_ccy", "AmtSchemaCcy", FieldDbType.Currency)
            {
                NumberKind = NumberKind.Amount,
                ValueExpression = "price * qty",
            });
            // Amount whose `CurrencyField` points to a missing column, so the variable lookup fails and `refCode` is null.
            table.Fields!.Add(new FormField("amt_missing_ccy", "AmtMissingCcy", FieldDbType.Currency)
            {
                NumberKind = NumberKind.Amount,
                CurrencyField = "ghost",
                ValueExpression = "price * qty",
            });
            // Weight, where `UnitField` supplies the unit reference code.
            table.Fields!.Add(new FormField("wt_field", "Weight", FieldDbType.Decimal)
            {
                NumberKind = NumberKind.Weight,
                UnitField = "unit",
                ValueExpression = "qty * 2",
            });
            // UnitPrice is not Amount/Quantity/Weight, so `refCode` is null (the default branch).
            table.Fields!.Add(new FormField("up_field", "UnitPrice", FieldDbType.Currency)
            {
                NumberKind = NumberKind.UnitPrice,
                ValueExpression = "price + 1",
            });

            var dataTable = new DataTable("Order");
            dataTable.Columns.Add("sys_rowid", typeof(Guid));
            dataTable.Columns.Add("currency", typeof(string));
            dataTable.Columns.Add("unit", typeof(string));
            dataTable.Columns.Add("price", typeof(decimal));
            dataTable.Columns.Add("qty", typeof(decimal));
            dataTable.Columns.Add("amt_field_ccy", typeof(decimal));
            dataTable.Columns.Add("amt_schema_ccy", typeof(decimal));
            dataTable.Columns.Add("amt_missing_ccy", typeof(decimal));
            dataTable.Columns.Add("wt_field", typeof(decimal));
            dataTable.Columns.Add("up_field", typeof(decimal));
            var row = dataTable.NewRow();
            row["sys_rowid"] = Guid.NewGuid();
            row["currency"] = "USD";
            row["unit"] = "KG";
            row["price"] = 10m;
            row["qty"] = 3m;
            dataTable.Rows.Add(row);

            var changed = _calculator.ApplyComputedRow(schema, schema.MasterTable!, row, new RoundingContext());

            Assert.Equal(30m, row["amt_field_ccy"]);
            Assert.Equal(30m, row["amt_schema_ccy"]);
            Assert.Equal(30m, row["amt_missing_ccy"]);
            Assert.Equal(6m, row["wt_field"]);
            Assert.Equal(11m, row["up_field"]);
            Assert.Contains("amt_field_ccy", changed);
        }

        [Fact]
        [DisplayName("ApplyComputedRow returns an empty list when formTable.Fields is empty")]
        public void ApplyComputedRow_EmptyFields_ReturnsEmpty()
        {
            var schema = new FormSchema("Order", "Order");
            var formTable = schema.Tables!.Add("Order", "Order");
            var table = new DataTable("Order");
            table.Columns.Add("a", typeof(string));
            var row = table.NewRow();
            table.Rows.Add(row);

            var result = _calculator.ApplyComputedRow(schema, formTable, row, new RoundingContext());

            Assert.Empty(result);
        }

        [Fact]
        [DisplayName("ApplyDefaultRow fills an empty column from a string constant expression and reports it")]
        public void ApplyDefaultRow_FillsEmptyStringColumn()
        {
            var schema = new FormSchema("Order", "Order");
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add(new FormField("code", "Code", FieldDbType.String)
            {
                DefaultValueExpression = "\"AUTO\"",
            });

            var dataTable = new DataTable("Order");
            dataTable.Columns.Add("code", typeof(string));
            var row = dataTable.NewRow();
            row["code"] = string.Empty;   // An empty string counts as empty for `IsEmptyValue`.
            dataTable.Rows.Add(row);

            var changed = _calculator.ApplyDefaultRow(schema.MasterTable!, row);

            Assert.Equal("AUTO", row["code"]);
            Assert.Contains("code", changed);
        }

        [Fact]
        [DisplayName("ApplyDefaultRow returns an empty list when formTable.Fields is empty")]
        public void ApplyDefaultRow_EmptyFields_ReturnsEmpty()
        {
            var schema = new FormSchema("Order", "Order");
            var formTable = schema.Tables!.Add("Order", "Order");
            var table = new DataTable("Order");
            table.Columns.Add("a", typeof(string));
            var row = table.NewRow();
            table.Rows.Add(row);

            var result = _calculator.ApplyDefaultRow(formTable, row);

            Assert.Empty(result);
        }

        [Fact]
        [DisplayName("BuildDependencyMap returns an empty map when formTable.Fields is empty")]
        public void BuildDependencyMap_EmptyFields_ReturnsEmpty()
        {
            var schema = new FormSchema("Order", "Order");
            var formTable = schema.Tables!.Add("Order", "Order");

            var map = _calculator.BuildDependencyMap(formTable);

            Assert.Empty(map);
        }
    }
}
