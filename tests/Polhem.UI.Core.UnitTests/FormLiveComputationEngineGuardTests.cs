using System.ComponentModel;
using System.Data;
using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Core.Expressions;
using Polhem.Definition.Forms;
using Polhem.Definition.Numbers;
using Polhem.Expressions;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// The guards of <see cref="FormLiveComputationEngine"/>: what it skips, what it rejects, and how it behaves once
    /// degraded or while a pass is running.
    /// </summary>
    public class FormLiveComputationEngineGuardTests
    {
        private static readonly DynamicExpressoEvaluator s_evaluator = new();

        private static FormSchema BuildSchema(string amountExpression = "price * qty")
        {
            var schema = new FormSchema("Order", "Order");
            var fields = schema.Tables!.Add("Order", "Order").Fields!;
            fields.Add(new FormField("price", "Price", FieldDbType.Currency) { NumberKind = NumberKind.UnitPrice });
            fields.Add(new FormField("qty", "Qty", FieldDbType.Decimal)
            {
                NumberKind = NumberKind.Quantity,
                DefaultValueExpression = "2m",
            });
            fields.Add(new FormField("note", "Note", FieldDbType.String));
            fields.Add(new FormField("amount", "Amount", FieldDbType.Currency)
            {
                NumberKind = NumberKind.Amount,
                ValueExpression = amountExpression,
            });
            return schema;
        }

        private static DataRow BuildRow()
        {
            var table = new DataTable("Order");
            table.Columns.Add("PRICE", typeof(decimal));
            table.Columns.Add("QTY", typeof(decimal));
            table.Columns.Add("NOTE", typeof(string));
            table.Columns.Add("AMOUNT", typeof(decimal));
            var row = table.NewRow();
            row["PRICE"] = 10m;
            row["QTY"] = 3m;
            row["NOTE"] = string.Empty;
            table.Rows.Add(row);
            return row;
        }

        private static FormLiveComputationEngine CreateEngine(string amountExpression = "price * qty")
            => new(BuildSchema(amountExpression), s_evaluator, () => string.Empty);

        private static FormLiveComputationEngine CreateDegradedEngine()
        {
            var engine = CreateEngine("price * missing_field");
            engine.Recompute("Order", "price", BuildRow());
            Assert.True(engine.IsDegraded);
            return engine;
        }

        [Fact]
        [DisplayName("The constructor rejects a missing schema, evaluator or time zone provider")]
        public void Constructor_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new FormLiveComputationEngine(null!, s_evaluator, () => string.Empty));
            Assert.Throws<ArgumentNullException>(() => new FormLiveComputationEngine(BuildSchema(), null!, () => string.Empty));
            Assert.Throws<ArgumentNullException>(() => new FormLiveComputationEngine(BuildSchema(), s_evaluator, null!));
        }

        [Fact]
        [DisplayName("A rounding context the caller supplies is used for the computed amount")]
        public void Constructor_SuppliedRoundingContext_ComputesWithIt()
        {
            var engine = new FormLiveComputationEngine(BuildSchema(), s_evaluator, () => string.Empty, new RoundingContext());
            var row = BuildRow();

            engine.Recompute("Order", "qty", row);

            Assert.Equal(30m, row["AMOUNT"]);
        }

        [Fact]
        [DisplayName("Recompute rejects a blank table name, a blank field name or a missing row")]
        public void Recompute_InvalidArguments_Throws()
        {
            var engine = CreateEngine();

            Assert.ThrowsAny<ArgumentException>(() => engine.Recompute(" ", "qty", BuildRow()));
            Assert.ThrowsAny<ArgumentException>(() => engine.Recompute("Order", " ", BuildRow()));
            Assert.Throws<ArgumentNullException>(() => engine.Recompute("Order", "qty", null!));
        }

        [Fact]
        [DisplayName("Recompute on a table the schema does not declare changes nothing")]
        public void Recompute_UnknownTable_ReturnsEmpty()
        {
            var row = BuildRow();

            var changed = CreateEngine().Recompute("Missing", "qty", row);

            Assert.Empty(changed);
            Assert.Equal(DBNull.Value, row["AMOUNT"]);
        }

        [Fact]
        [DisplayName("A change to a computed field does not trigger a recompute")]
        public void Recompute_ComputedFieldChanged_ReturnsEmpty()
        {
            var row = BuildRow();

            var changed = CreateEngine().Recompute("Order", "amount", row);

            Assert.Empty(changed);
            Assert.Equal(DBNull.Value, row["AMOUNT"]);
        }

        [Fact]
        [DisplayName("A change to a field no computed field references skips the pass")]
        public void Recompute_UnreferencedField_ReturnsEmpty()
        {
            var row = BuildRow();

            var changed = CreateEngine().Recompute("Order", "note", row);

            Assert.Empty(changed);
            Assert.Equal(DBNull.Value, row["AMOUNT"]);
        }

        [Fact]
        [DisplayName("A change to a field the schema does not declare skips the pass")]
        public void Recompute_UndeclaredField_ReturnsEmpty()
        {
            var changed = CreateEngine().Recompute("Order", "not_a_field", BuildRow());

            Assert.Empty(changed);
        }

        [Fact]
        [DisplayName("A second edit reuses the dependency map and recomputes again")]
        public void Recompute_SecondEdit_RecomputesAgain()
        {
            var engine = CreateEngine();
            var row = BuildRow();
            engine.Recompute("Order", "qty", row);
            row["QTY"] = 4m;

            var changed = engine.Recompute("Order", "qty", row);

            Assert.Contains("amount", changed);
            Assert.Equal(40m, row["AMOUNT"]);
        }

        [Fact]
        [DisplayName("Once degraded, Recompute leaves the row alone")]
        public void Recompute_Degraded_ReturnsEmpty()
        {
            var engine = CreateDegradedEngine();
            var row = BuildRow();

            var changed = engine.Recompute("Order", "qty", row);

            Assert.Empty(changed);
            Assert.Equal(DBNull.Value, row["AMOUNT"]);
        }

        [Fact]
        [DisplayName("A recompute started from inside a running pass is skipped")]
        public void Recompute_DuringPass_ReturnsEmpty()
        {
            FormLiveComputationEngine? engine = null;
            IReadOnlyList<string>? nested = null;
            var recomputingInside = false;
            var evaluator = new CallbackEvaluator(() =>
            {
                recomputingInside = engine!.IsRecomputing;
                nested = engine.Recompute("Order", "qty", BuildRow());
            });
            engine = new FormLiveComputationEngine(BuildSchema(), evaluator, () => string.Empty);

            engine.Recompute("Order", "qty", BuildRow());

            Assert.True(recomputingInside);
            Assert.NotNull(nested);
            Assert.Empty(nested);
            Assert.False(engine.IsRecomputing);
        }

        [Fact]
        [DisplayName("ApplyDefaults fills the default values of a row")]
        public void ApplyDefaults_DefaultExpression_FillsValue()
        {
            var row = BuildRow();
            row["QTY"] = DBNull.Value;

            var changed = CreateEngine().ApplyDefaults("Order", row);

            Assert.Contains("qty", changed);
            Assert.Equal(2m, row["QTY"]);
        }

        [Fact]
        [DisplayName("ApplyDefaults rejects a blank table name or a missing row")]
        public void ApplyDefaults_InvalidArguments_Throws()
        {
            var engine = CreateEngine();

            Assert.ThrowsAny<ArgumentException>(() => engine.ApplyDefaults(" ", BuildRow()));
            Assert.Throws<ArgumentNullException>(() => engine.ApplyDefaults("Order", null!));
        }

        [Fact]
        [DisplayName("ApplyDefaults on a table the schema does not declare changes nothing")]
        public void ApplyDefaults_UnknownTable_ReturnsEmpty()
        {
            Assert.Empty(CreateEngine().ApplyDefaults("Missing", BuildRow()));
        }

        [Fact]
        [DisplayName("Once degraded, ApplyDefaults leaves the row alone")]
        public void ApplyDefaults_Degraded_ReturnsEmpty()
        {
            var row = BuildRow();
            row["QTY"] = DBNull.Value;

            var changed = CreateDegradedEngine().ApplyDefaults("Order", row);

            Assert.Empty(changed);
            Assert.Equal(DBNull.Value, row["QTY"]);
        }

        [Fact]
        [DisplayName("InitializeNewRow fills the defaults and then the computed fields, each named once")]
        public void InitializeNewRow_DefaultsAndComputed_ReturnsBoth()
        {
            var row = BuildRow();
            row["QTY"] = DBNull.Value;

            var changed = CreateEngine().InitializeNewRow("Order", row);

            Assert.Equal(2m, row["QTY"]);
            Assert.Equal(20m, row["AMOUNT"]);
            Assert.Single(changed, f => string.Equals(f, "qty", StringComparison.OrdinalIgnoreCase));
            Assert.Single(changed, f => string.Equals(f, "amount", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        [DisplayName("InitializeNewRow rejects a blank table name or a missing row")]
        public void InitializeNewRow_InvalidArguments_Throws()
        {
            var engine = CreateEngine();

            Assert.ThrowsAny<ArgumentException>(() => engine.InitializeNewRow(" ", BuildRow()));
            Assert.Throws<ArgumentNullException>(() => engine.InitializeNewRow("Order", null!));
        }

        [Fact]
        [DisplayName("InitializeNewRow on a table the schema does not declare changes nothing")]
        public void InitializeNewRow_UnknownTable_ReturnsEmpty()
        {
            Assert.Empty(CreateEngine().InitializeNewRow("Missing", BuildRow()));
        }

        [Fact]
        [DisplayName("Once degraded, InitializeNewRow leaves the row alone")]
        public void InitializeNewRow_Degraded_ReturnsEmpty()
        {
            Assert.Empty(CreateDegradedEngine().InitializeNewRow("Order", BuildRow()));
        }

        /// <summary>
        /// Evaluates through the real evaluator, running a callback first, so a test can act while a pass runs.
        /// </summary>
        private sealed class CallbackEvaluator(Action onEvaluate) : IExpressionEvaluator
        {
            public object? Evaluate(string expression, IReadOnlyDictionary<string, object?> variables, Type returnType,
                string timeZoneId = "", DateTimeBasis basis = DateTimeBasis.UserZone)
            {
                onEvaluate();
                return s_evaluator.Evaluate(expression, variables, returnType, timeZoneId, basis);
            }

            public T Evaluate<T>(string expression, IReadOnlyDictionary<string, object?> variables, string timeZoneId = "",
                DateTimeBasis basis = DateTimeBasis.UserZone)
            {
                onEvaluate();
                return s_evaluator.Evaluate<T>(expression, variables, timeZoneId, basis);
            }

            public IReadOnlyList<string> GetReferencedVariables(string expression)
                => s_evaluator.GetReferencedVariables(expression);

            public void Validate(string expression, IReadOnlyDictionary<string, Type> variableTypes, Type returnType)
                => s_evaluator.Validate(expression, variableTypes, returnType);
        }
    }
}
