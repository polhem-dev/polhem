using System.ComponentModel;
using System.Data;
using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Expressions;

namespace Polhem.Definition.UnitTests.Forms
{
    /// <summary>
    /// Tests for the precedence of <see cref="FormField.DefaultValueExpression"/> on a new row: the expression's
    /// value replaces the per-type seed of <see cref="FormRowDefaults"/> on the server
    /// (<see cref="FormExpressionCalculator.ApplyNewRowDefaults"/>) and on a client
    /// (<see cref="FormExpressionCalculator.ApplyDefaultRow"/>), while the save pass
    /// (<see cref="FormExpressionCalculator.ApplyFieldExpressions"/>) keeps a value already present.
    /// Numeric, date and Guid fields are covered because their seeds (0, today, <see cref="Guid.Empty"/>) are
    /// never empty, which is how the expression used to lose to them.
    /// </summary>
    public class FormExpressionCalculatorNewRowTests
    {
        private const string ExpectedGuidText = "6f9619ff-8b86-d011-b42d-00c04fc964ff";
        private static readonly Guid s_expectedGuid = Guid.Parse(ExpectedGuidText);

        private readonly FormExpressionCalculator _calculator = new(new DynamicExpressoEvaluator());

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Ticket", "Ticket") { CategoryId = "company" };
            var table = schema.Tables!.Add("Ticket", "Ticket");
            table.Fields!.Add(new FormField("sys_rowid", "RowId", FieldDbType.Guid));
            table.Fields!.Add(new FormField("priority", "Priority", FieldDbType.Integer)
            {
                DefaultValue = "1",
                DefaultValueExpression = "3",
            });
            table.Fields!.Add(new FormField("due_date", "DueDate", FieldDbType.Date)
            {
                DefaultValueExpression = "Today().AddDays(7)",
            });
            table.Fields!.Add(new FormField("owner_rowid", "Owner", FieldDbType.Guid)
            {
                DefaultValueExpression = $"Guid.Parse(\"{ExpectedGuidText}\")",
            });
            return schema;
        }

        private static DataSet BuildSeededDataSet(FormSchema schema)
        {
            var table = new DataTable("Ticket");
            table.Columns.Add("sys_rowid", typeof(Guid));
            table.Columns.Add("priority", typeof(int));
            table.Columns.Add("due_date", typeof(DateTime));
            table.Columns.Add("owner_rowid", typeof(Guid));

            var row = table.NewRow();
            FormRowDefaults.Apply(schema.MasterTable!, row, null, string.Empty, DateTimeBasis.Utc);
            // A literal default already applied, as `GetNewData` does before the expressions run.
            row["priority"] = 1;
            table.Rows.Add(row);

            var dataSet = new DataSet("Ticket");
            dataSet.Tables.Add(table);
            return dataSet;
        }

        [Fact]
        [DisplayName("ApplyNewRowDefaults: an integer expression replaces both the literal DefaultValue and the 0 seed")]
        public void ApplyNewRowDefaults_Numeric_ExpressionWins()
        {
            var schema = BuildSchema();
            var dataSet = BuildSeededDataSet(schema);

            _calculator.ApplyNewRowDefaults(schema, dataSet);

            Assert.Equal(3, dataSet.Tables["Ticket"]!.Rows[0]["priority"]);
        }

        [Fact]
        [DisplayName("ApplyNewRowDefaults: a date expression replaces the today seed")]
        public void ApplyNewRowDefaults_Date_ExpressionWins()
        {
            var schema = BuildSchema();
            var dataSet = BuildSeededDataSet(schema);
            var dayBefore = DateTime.UtcNow.Date;

            _calculator.ApplyNewRowDefaults(schema, dataSet);

            var dueDate = (DateTime)dataSet.Tables["Ticket"]!.Rows[0]["due_date"];
            Assert.InRange(dueDate, dayBefore.AddDays(7), DateTime.UtcNow.Date.AddDays(7));
        }

        [Fact]
        [DisplayName("ApplyNewRowDefaults: a Guid expression replaces the Guid.Empty seed")]
        public void ApplyNewRowDefaults_Guid_ExpressionWins()
        {
            var schema = BuildSchema();
            var dataSet = BuildSeededDataSet(schema);

            _calculator.ApplyNewRowDefaults(schema, dataSet);

            Assert.Equal(s_expectedGuid, dataSet.Tables["Ticket"]!.Rows[0]["owner_rowid"]);
        }

        [Fact]
        [DisplayName("ApplyNewRowDefaults leaves rows that are not Added untouched")]
        public void ApplyNewRowDefaults_UnchangedRow_NotTouched()
        {
            var schema = BuildSchema();
            var dataSet = BuildSeededDataSet(schema);
            dataSet.AcceptChanges();

            _calculator.ApplyNewRowDefaults(schema, dataSet);

            Assert.Equal(1, dataSet.Tables["Ticket"]!.Rows[0]["priority"]);
        }

        [Fact]
        [DisplayName("ApplyDefaultRow (client new row): numeric, date and Guid expressions replace the seeded values and are reported")]
        public void ApplyDefaultRow_SeededRow_ExpressionsWin()
        {
            var schema = BuildSchema();
            var row = BuildSeededDataSet(schema).Tables["Ticket"]!.Rows[0];
            var dayBefore = DateTime.UtcNow.Date;

            var changed = _calculator.ApplyDefaultRow(schema.MasterTable!, row);

            Assert.Equal(3, row["priority"]);
            Assert.InRange((DateTime)row["due_date"], dayBefore.AddDays(7), DateTime.UtcNow.Date.AddDays(7));
            Assert.Equal(s_expectedGuid, row["owner_rowid"]);
            Assert.Equal(["priority", "due_date", "owner_rowid"], changed);
        }

        [Fact]
        [DisplayName("ApplyFieldExpressions (save pass) keeps a value already present on an Added row instead of re-evaluating the default")]
        public void ApplyFieldExpressions_AddedRowWithValue_KeepsValue()
        {
            var schema = BuildSchema();
            var dataSet = BuildSeededDataSet(schema);
            dataSet.Tables["Ticket"]!.Rows[0]["priority"] = 9;

            _calculator.ApplyFieldExpressions(schema, dataSet, new RoundingContext());

            Assert.Equal(9, dataSet.Tables["Ticket"]!.Rows[0]["priority"]);
        }
    }
}
