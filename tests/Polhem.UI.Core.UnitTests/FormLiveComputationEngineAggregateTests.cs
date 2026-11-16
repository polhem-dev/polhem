using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Numbers;
using Polhem.Expressions;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// <see cref="FormLiveComputationEngine"/> keeps a master total over detail rows (<c>Sum(Table.field)</c>)
    /// current while the detail rows are edited, added, deleted or put back.
    /// </summary>
    public class FormLiveComputationEngineAggregateTests
    {
        private static readonly DynamicExpressoEvaluator s_evaluator = new();

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Order", "Order");
            var master = schema.Tables!.Add("Order", "Order").Fields!;
            master.Add(new FormField("note", "Note", FieldDbType.String));
            master.Add(new FormField("total", "Total", FieldDbType.Currency)
            {
                NumberKind = NumberKind.Amount,
                ValueExpression = "Sum(OrderLine.amount)",
            });
            master.Add(new FormField("lines", "Lines", FieldDbType.Integer) { ValueExpression = "Count(OrderLine)" });

            var detail = schema.Tables!.Add("OrderLine", "Lines").Fields!;
            detail.Add(new FormField("price", "Price", FieldDbType.Currency) { NumberKind = NumberKind.UnitPrice });
            detail.Add(new FormField("qty", "Qty", FieldDbType.Integer));
            detail.Add(new FormField("remark", "Remark", FieldDbType.String));
            detail.Add(new FormField("amount", "Amount", FieldDbType.Currency)
            {
                NumberKind = NumberKind.Amount,
                ValueExpression = "price * qty",
            });
            return schema;
        }

        private static DataSet BuildDataSet()
        {
            var dataSet = new DataSet();
            var master = dataSet.Tables.Add("Order");
            master.Columns.Add("note", typeof(string));
            master.Columns.Add("total", typeof(decimal));
            master.Columns.Add("lines", typeof(int));
            master.Rows.Add("n", 0m, 0);

            var detail = dataSet.Tables.Add("OrderLine");
            detail.Columns.Add("price", typeof(decimal));
            detail.Columns.Add("qty", typeof(int));
            detail.Columns.Add("remark", typeof(string));
            detail.Columns.Add("amount", typeof(decimal));
            detail.Rows.Add(10m, 2, "", 20m);
            detail.Rows.Add(5m, 1, "", 5m);
            dataSet.AcceptChanges();
            return dataSet;
        }

        private static FormLiveComputationEngine NewEngine() => new(BuildSchema(), s_evaluator, () => string.Empty);

        private static DataRow Master(DataSet dataSet) => dataSet.Tables["Order"]!.Rows[0];

        [Fact]
        [DisplayName("Editing a detail field the total depends on recomputes the line and then the master")]
        public void Recompute_DetailSourceEdited_RecomputesMaster()
        {
            var dataSet = BuildDataSet();
            var line = dataSet.Tables["OrderLine"]!.Rows[0];
            line["qty"] = 4;

            var changed = NewEngine().Recompute("OrderLine", "qty", line);

            Assert.Equal(["amount"], changed);
            Assert.Equal(45m, Master(dataSet)["total"]);
        }

        [Fact]
        [DisplayName("Editing a detail field no total depends on leaves the master alone")]
        public void Recompute_UnrelatedDetailField_MasterUntouched()
        {
            var dataSet = BuildDataSet();
            var line = dataSet.Tables["OrderLine"]!.Rows[0];
            line["remark"] = "x";

            NewEngine().Recompute("OrderLine", "remark", line);

            Assert.Equal(DataRowState.Unchanged, Master(dataSet).RowState);
        }

        [Fact]
        [DisplayName("A value written into the aggregated computed field, as by a callback, recomputes the master")]
        public void Recompute_AggregatedComputedFieldWritten_RecomputesMaster()
        {
            var dataSet = BuildDataSet();
            var line = dataSet.Tables["OrderLine"]!.Rows[1];
            line["amount"] = 50m;

            NewEngine().Recompute("OrderLine", "amount", line);

            Assert.Equal(70m, Master(dataSet)["total"]);
        }

        [Fact]
        [DisplayName("A new detail row counts towards the master at once")]
        public void InitializeNewRow_DetailRow_RecomputesMaster()
        {
            var dataSet = BuildDataSet();
            var row = dataSet.Tables["OrderLine"]!.Rows.Add(3m, 3, "", 0m);

            NewEngine().InitializeNewRow("OrderLine", row);

            Assert.Equal(9m, row["amount"]);
            Assert.Equal(34m, Master(dataSet)["total"]);
            Assert.Equal(3, Master(dataSet)["lines"]);
        }

        [Fact]
        [DisplayName("After a detail row is deleted, RecomputeAggregates leaves it out of the master")]
        public void RecomputeAggregates_RowDeleted_ExcludesIt()
        {
            var dataSet = BuildDataSet();
            dataSet.Tables["OrderLine"]!.Rows[0].Delete();

            var changed = NewEngine().RecomputeAggregates("OrderLine", dataSet);

            Assert.Contains("total", changed);
            Assert.Equal(5m, Master(dataSet)["total"]);
            Assert.Equal(1, Master(dataSet)["lines"]);
        }

        [Fact]
        [DisplayName("A cancelled row edit puts the master back to the stored values")]
        public void RecomputeAggregates_EditCancelled_RestoresTotal()
        {
            var dataSet = BuildDataSet();
            var engine = NewEngine();
            engine.RecomputeAggregates("OrderLine", dataSet);
            var line = dataSet.Tables["OrderLine"]!.Rows[0];

            line.BeginEdit();
            line["qty"] = 10;
            engine.Recompute("OrderLine", "qty", line);
            Assert.Equal(105m, Master(dataSet)["total"]);

            line.CancelEdit();
            engine.RecomputeAggregates("OrderLine", dataSet);

            Assert.Equal(25m, Master(dataSet)["total"]);
        }

        [Fact]
        [DisplayName("RecomputeAggregates does nothing for a table the master does not add up")]
        public void RecomputeAggregates_NotAggregated_Empty()
        {
            var dataSet = BuildDataSet();

            Assert.Empty(NewEngine().RecomputeAggregates("Order", dataSet));
            Assert.Equal(DataRowState.Unchanged, Master(dataSet).RowState);
        }
    }
}
