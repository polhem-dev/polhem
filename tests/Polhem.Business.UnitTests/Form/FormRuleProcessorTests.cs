using System.ComponentModel;
using System.Data;
using Polhem.Base.Data;
using Polhem.Base.Exceptions;
using Polhem.Business.Form;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Expressions;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// Tests for <see cref="FormRuleProcessor"/>: computed fields (including RoundByKind rounding), default value expressions,
    /// BeforeSave validation (including When applicability) and BeforeDelete validation. Uses the real DynamicExpressoEvaluator,
    /// pure logic with no database.
    /// </summary>
    public class FormRuleProcessorTests
    {
        private readonly FormRuleProcessor _processor = new(new DynamicExpressoEvaluator());

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

        private static DataSet BuildOrderDataSet(decimal price, decimal qty, string status,
            object? orderDate = null, object? amount = null)
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
            row["status"] = status;
            if (amount != null) { row["amount"] = amount; }
            if (orderDate != null) { row["order_date"] = orderDate; }
            table.Rows.Add(row);   // RowState = Added

            var dataSet = new DataSet("Order");
            dataSet.Tables.Add(table);
            return dataSet;
        }

        [Fact]
        [DisplayName("Computed field: amount = price * qty is filled in on an Added row before saving")]
        public void ApplyBeforeSave_ComputesAmount_OnAddedRow()
        {
            var schema = BuildOrderSchema();
            var dataSet = BuildOrderDataSet(price: 10m, qty: 3m, status: "Draft");

            _processor.ApplyBeforeSave(schema, dataSet, new RoundingContext());

            Assert.Equal(30m, dataSet.Tables["Order"]!.Rows[0]["amount"]);
        }

        [Fact]
        [DisplayName("Computed field: the Amount kind rounds to 2 decimals by NumberKind (away from zero)")]
        public void ApplyBeforeSave_RoundsAmountByNumberKind()
        {
            var schema = BuildOrderSchema();
            // 2.125 * 1 = 2.125 → Amount rounds to 2 decimals away from zero → 2.13
            var dataSet = BuildOrderDataSet(price: 2.125m, qty: 1m, status: "Draft");

            _processor.ApplyBeforeSave(schema, dataSet, new RoundingContext());

            Assert.Equal(2.13m, dataSet.Tables["Order"]!.Rows[0]["amount"]);
        }

        [Fact]
        [DisplayName("Default value expression: an empty column on an Added row is filled with Today()")]
        public void ApplyBeforeSave_FillsDefaultValueExpression_WhenEmpty()
        {
            var schema = BuildOrderSchema();
            var dataSet = BuildOrderDataSet(price: 1m, qty: 1m, status: "Draft");

            _processor.ApplyBeforeSave(schema, dataSet, new RoundingContext());

            // UTC, not `DateTime.Today`: the framework's date default is `UtcNow.Date` (ADR-032 D12).
            // Asserting the local date would always fail locally at 00:00–08:00 in UTC+8, and CI running in UTC would never see it.
            Assert.Equal(DateTime.UtcNow.Date, dataSet.Tables["Order"]!.Rows[0]["order_date"]);
        }

        [Fact]
        [DisplayName("Default value expression: a column that already has a value is not overwritten")]
        public void ApplyBeforeSave_DoesNotOverwriteExistingDefault()
        {
            var schema = BuildOrderSchema();
            var existing = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var dataSet = BuildOrderDataSet(price: 1m, qty: 1m, status: "Draft", orderDate: existing);

            _processor.ApplyBeforeSave(schema, dataSet, new RoundingContext());

            Assert.Equal(existing, dataSet.Tables["Order"]!.Rows[0]["order_date"]);
        }

        [Fact]
        [DisplayName("Validation before save: a failing Condition throws UserMessageException with the message")]
        public void ApplyBeforeSave_FailingRule_ThrowsUserMessage()
        {
            var schema = BuildOrderSchema();
            schema.Rules!.Add("amount_positive", "amount > 0", "金額必須大於 0");
            var dataSet = BuildOrderDataSet(price: 0m, qty: 5m, status: "Draft");

            var ex = Assert.Throws<UserMessageException>(() =>
                _processor.ApplyBeforeSave(schema, dataSet, new RoundingContext()));

            Assert.Equal("金額必須大於 0", ex.Message);
        }

        [Fact]
        [DisplayName("Validation before save: a passing Condition does not throw")]
        public void ApplyBeforeSave_PassingRule_DoesNotThrow()
        {
            var schema = BuildOrderSchema();
            schema.Rules!.Add("amount_positive", "amount > 0", "金額必須大於 0");
            var dataSet = BuildOrderDataSet(price: 10m, qty: 2m, status: "Draft");

            var ex = Record.Exception(() =>
                _processor.ApplyBeforeSave(schema, dataSet, new RoundingContext()));

            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("Validation before save: the whole rule is skipped when When is false (Condition is not checked)")]
        public void ApplyBeforeSave_WhenFalse_SkipsRule()
        {
            var schema = BuildOrderSchema();
            schema.Rules!.Add(new FormRule("approved_amount", "amount > 0", "已核准金額必須大於 0")
            {
                When = "status == \"Approved\"",
            });
            // status=Draft and amount=0: When is false → skipped → no exception.
            var dataSet = BuildOrderDataSet(price: 0m, qty: 1m, status: "Draft");

            var ex = Record.Exception(() =>
                _processor.ApplyBeforeSave(schema, dataSet, new RoundingContext()));

            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("Validation before save: stops when When is true and Condition fails")]
        public void ApplyBeforeSave_WhenTrueAndConditionFails_Throws()
        {
            var schema = BuildOrderSchema();
            schema.Rules!.Add(new FormRule("approved_amount", "amount > 0", "已核准金額必須大於 0")
            {
                When = "status == \"Approved\"",
            });
            var dataSet = BuildOrderDataSet(price: 0m, qty: 1m, status: "Approved");

            var ex = Assert.Throws<UserMessageException>(() =>
                _processor.ApplyBeforeSave(schema, dataSet, new RoundingContext()));

            Assert.Equal("已核准金額必須大於 0", ex.Message);
        }

        [Fact]
        [DisplayName("Check before delete: a failing BeforeDelete rule throws UserMessageException")]
        public void ApplyBeforeDelete_FailingRule_Throws()
        {
            var schema = BuildOrderSchema();
            schema.Rules!.Add(new FormRule("no_delete_closed", "status != \"Closed\"", "已結案不可刪除")
            {
                Trigger = FormRuleTrigger.BeforeDelete,
            });
            var snapshot = BuildOrderDataSet(price: 1m, qty: 1m, status: "Closed");
            snapshot.AcceptChanges();   // The snapshot rows are Unchanged.

            var ex = Assert.Throws<UserMessageException>(() =>
                _processor.ApplyBeforeDelete(schema, snapshot));

            Assert.Equal("已結案不可刪除", ex.Message);
        }

        [Fact]
        [DisplayName("Check before delete: BeforeDelete rules do not run before save (trigger isolation)")]
        public void ApplyBeforeSave_DoesNotRunBeforeDeleteRules()
        {
            var schema = BuildOrderSchema();
            schema.Rules!.Add(new FormRule("no_delete_closed", "status != \"Closed\"", "已結案不可刪除")
            {
                Trigger = FormRuleTrigger.BeforeDelete,
            });
            var dataSet = BuildOrderDataSet(price: 1m, qty: 1m, status: "Closed");

            var ex = Record.Exception(() =>
                _processor.ApplyBeforeSave(schema, dataSet, new RoundingContext()));

            Assert.Null(ex);
        }
    }
}
