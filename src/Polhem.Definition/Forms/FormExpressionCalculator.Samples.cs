using Polhem.Core;
using Polhem.Core.Expressions;
using Polhem.Definition.Numbers;

namespace Polhem.Definition.Forms
{
    public sealed partial class FormExpressionCalculator
    {
        /// <summary>
        /// Evaluates a rule expression (<see cref="FormRule.When"/> or <see cref="FormRule.Condition"/>) against one
        /// row of sample values, the way <see cref="ValidateRules"/> evaluates it against a row of a data set.
        /// </summary>
        /// <param name="expression">The expression, which need not be the one the schema holds yet.</param>
        /// <param name="formTable">The table the rule targets (<see cref="ResolveRuleTable"/>).</param>
        /// <param name="sample">The row's values and the time zone; <see cref="ExpressionSample.DetailRows"/> is not read.</param>
        /// <returns>The value of the expression.</returns>
        /// <remarks>
        /// Each value is coerced to its field's type as a cell read from the database is
        /// (<see cref="ExpressionPolicy.CoerceValue"/>). <c>Now()</c> uses the user's zone, as a client preview does;
        /// the server's pass uses UTC.
        /// </remarks>
        /// <exception cref="ExpressionEvaluationException">The expression cannot be parsed or evaluated.</exception>
        public bool EvaluateRuleSample(string expression, FormTable formTable, ExpressionSample sample)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expression);
            ArgumentNullException.ThrowIfNull(formTable);
            ArgumentNullException.ThrowIfNull(sample);

            var variables = SampleVariables(formTable, sample.Values);
            expression = BindSampleAggregates(expression, formTable, variables);
            return _evaluator.Evaluate<bool>(expression, NarrowVariables(expression, variables), sample.TimeZoneId,
                DateTimeBasis.UserZone);
        }

        /// <summary>
        /// Evaluates a rule expression of the master table against one row of sample values and sample rows of its detail
        /// tables, so its aggregates (<c>Count(Table)</c>, <c>All(Table, condition)</c>, …) read those rows.
        /// </summary>
        /// <param name="expression">The expression, which need not be the one the schema holds yet.</param>
        /// <param name="schema">The form the expression belongs to.</param>
        /// <param name="sample">
        /// The master row's values, the sample rows of the detail tables, and the time zone.
        /// </param>
        /// <returns>The value of the expression.</returns>
        /// <remarks>
        /// Each value is coerced to its field's type as a cell read from the database is. A detail value is aggregated
        /// as it is, without rounding, as <see cref="ValidateRules"/> aggregates it.
        /// </remarks>
        /// <exception cref="ArgumentException">A detail table name is not a table of <paramref name="schema"/>.</exception>
        /// <exception cref="ExpressionEvaluationException">The expression cannot be parsed or evaluated.</exception>
        public bool EvaluateMasterRuleSample(string expression, FormSchema schema, ExpressionSample sample)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expression);
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(sample);
            var master = schema.MasterTable
                ?? throw new ArgumentException("The form has no master table.", nameof(schema));

            var dataSet = new System.Data.DataSet();
            foreach (var (tableName, rows) in sample.DetailRows)
            {
                if (schema.Tables is null || !schema.Tables.Contains(tableName))
                    throw new ArgumentException($"'{tableName}' is not a table of the form.", nameof(sample));
                var formTable = schema.Tables[tableName];
                var table = dataSet.Tables.Add(formTable.TableName);
                foreach (var field in formTable.Fields ?? [])
                {
                    if (StringUtilities.IsEmpty(field.FieldName)) { continue; }
                    table.Columns.Add(field.FieldName, Polhem.Core.Data.DbTypeConverter.ToType(field.DbType));
                }
                foreach (var row in rows)
                {
                    var dataRow = table.NewRow();
                    foreach (var (name, value) in SampleVariables(formTable, row))
                        dataRow[name] = value ?? DBNull.Value;
                    table.Rows.Add(dataRow);
                }
            }

            var variables = SampleVariables(master, sample.Values);
            expression = BindAggregates(expression, schema, master, dataSet, variables, roundingContext: null,
                sample.TimeZoneId, DateTimeBasis.UserZone);
            return _evaluator.Evaluate<bool>(expression, NarrowVariables(expression, variables), sample.TimeZoneId,
                DateTimeBasis.UserZone);
        }

        /// <summary>
        /// Evaluates a field expression against one row of sample values and returns what would be written to the
        /// field, the way <see cref="ApplyComputedRow"/> (for <see cref="FormField.ValueExpression"/>) and
        /// <see cref="ApplyDefaultRow"/> (for <see cref="FormField.DefaultValueExpression"/>) write it.
        /// </summary>
        /// <param name="field">The field the expression computes; its table supplies the variables.</param>
        /// <param name="propertyName">
        /// <see cref="FormField.ValueExpression"/> or <see cref="FormField.DefaultValueExpression"/>, by name.
        /// </param>
        /// <param name="expression">The expression, which need not be the one the field holds yet.</param>
        /// <param name="sample">The row's values and the time zone; <see cref="ExpressionSample.DetailRows"/> is not read.</param>
        /// <returns>
        /// The value coerced to the field's type; <c>null</c> when a default-value expression yields nothing and the
        /// field is left empty.
        /// </returns>
        /// <remarks>
        /// A detail aggregate takes the value it has over no rows: zero for <c>Sum</c> and <c>Count</c>, the field's
        /// empty value for <c>Min</c> and <c>Max</c>.
        /// A computed numeric value is not rounded: <see cref="ApplyComputedRow"/> rounds it by the company's number
        /// settings (<see cref="RoundingContext"/>), which a sample has no company to read.
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="propertyName"/> names no field expression.</exception>
        /// <exception cref="ExpressionEvaluationException">The expression cannot be parsed or evaluated.</exception>
        public object? EvaluateFieldSample(FormField field, string propertyName, string expression, ExpressionSample sample)
        {
            ArgumentNullException.ThrowIfNull(field);
            ArgumentException.ThrowIfNullOrWhiteSpace(expression);
            ArgumentNullException.ThrowIfNull(sample);
            if (propertyName is not (nameof(FormField.ValueExpression) or nameof(FormField.DefaultValueExpression)))
                throw new ArgumentException($"'{propertyName}' is not a field expression property.", nameof(propertyName));
            var formTable = field.Table
                ?? throw new ArgumentException($"Field '{field.FieldName}' belongs to no table.", nameof(field));

            var variables = SampleVariables(formTable, sample.Values);
            if (propertyName == nameof(FormField.ValueExpression))
                expression = BindSampleAggregates(expression, formTable, variables);
            var value = _evaluator.Evaluate<object?>(expression, NarrowVariables(expression, variables), sample.TimeZoneId,
                DateTimeBasis.UserZone);
            // The two passes treat a null result differently: a default leaves the cell empty, while a computed
            // value is coerced like any other result.
            if (value is null && propertyName == nameof(FormField.DefaultValueExpression)) { return null; }
            return ExpressionPolicy.CoerceValue(value, field.DbType);
        }

        // A sample row has no detail rows, so each aggregate takes the value it has over none: zero for Sum and
        // Count, the field's empty value for Min and Max, false for Any and true for All.
        private string BindSampleAggregates(string expression, FormTable formTable, Dictionary<string, object?> variables)
        {
            var parsed = ParseAggregates(expression);
            if (!parsed.HasAggregates) { return expression; }
            var schema = (formTable.Collection as FormTableCollection)?.Owner as FormSchema
                ?? throw new ExpressionEvaluationException(
                    $"The table '{formTable.TableName}' belongs to no form, so its aggregates cannot be resolved.");
            // No rows, so no condition is evaluated and the zone does not matter.
            return BindAggregates(expression, schema, formTable, dataSet: null, variables, roundingContext: null,
                timeZoneId: string.Empty, DateTimeBasis.UserZone);
        }

        // The variables of a sample row: every field of the table by its declared name, coerced as
        // `BuildVariables` coerces a cell.
        private static Dictionary<string, object?> SampleVariables(FormTable formTable,
            IReadOnlyDictionary<string, object?> values)
        {
            var variables = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (formTable.Fields == null) { return variables; }
            foreach (var field in formTable.Fields)
            {
                if (StringUtilities.IsEmpty(field.FieldName)) { continue; }
                values.TryGetValue(field.FieldName, out var value);
                variables[field.FieldName] = ExpressionPolicy.CoerceValue(value, field.DbType);
            }
            return variables;
        }
    }
}
