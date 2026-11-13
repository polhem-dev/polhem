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
        /// <param name="values">The sample values by field name; a field without one is empty.</param>
        /// <param name="timeZoneId">
        /// The IANA time zone id the <c>Today()</c> and <c>Now()</c> helpers see; blank means UTC.
        /// </param>
        /// <returns>The value of the expression.</returns>
        /// <remarks>
        /// Each value is coerced to its field's type as a cell read from the database is
        /// (<see cref="ExpressionPolicy.CoerceValue"/>). <c>Now()</c> uses the user's zone, as a client preview does;
        /// the server's pass uses UTC.
        /// </remarks>
        /// <exception cref="ExpressionEvaluationException">The expression cannot be parsed or evaluated.</exception>
        public bool EvaluateRuleSample(string expression, FormTable formTable, IReadOnlyDictionary<string, object?> values,
            string timeZoneId = "")
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expression);
            ArgumentNullException.ThrowIfNull(formTable);
            ArgumentNullException.ThrowIfNull(values);

            var variables = SampleVariables(formTable, values);
            return _evaluator.Evaluate<bool>(expression, NarrowVariables(expression, variables), timeZoneId,
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
        /// <param name="values">The sample values by field name; a field without one is empty.</param>
        /// <param name="timeZoneId">
        /// The IANA time zone id the <c>Today()</c> and <c>Now()</c> helpers see; blank means UTC.
        /// </param>
        /// <returns>
        /// The value coerced to the field's type; <c>null</c> when a default-value expression yields nothing and the
        /// field is left empty.
        /// </returns>
        /// <remarks>
        /// A computed numeric value is not rounded: <see cref="ApplyComputedRow"/> rounds it by the company's number
        /// settings (<see cref="RoundingContext"/>), which a sample has no company to read.
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="propertyName"/> names no field expression.</exception>
        /// <exception cref="ExpressionEvaluationException">The expression cannot be parsed or evaluated.</exception>
        public object? EvaluateFieldSample(FormField field, string propertyName, string expression,
            IReadOnlyDictionary<string, object?> values, string timeZoneId = "")
        {
            ArgumentNullException.ThrowIfNull(field);
            ArgumentException.ThrowIfNullOrWhiteSpace(expression);
            ArgumentNullException.ThrowIfNull(values);
            if (propertyName is not (nameof(FormField.ValueExpression) or nameof(FormField.DefaultValueExpression)))
                throw new ArgumentException($"'{propertyName}' is not a field expression property.", nameof(propertyName));
            var formTable = field.Table
                ?? throw new ArgumentException($"Field '{field.FieldName}' belongs to no table.", nameof(field));

            var variables = SampleVariables(formTable, values);
            var value = _evaluator.Evaluate<object?>(expression, NarrowVariables(expression, variables), timeZoneId,
                DateTimeBasis.UserZone);
            // The two passes treat a null result differently: a default leaves the cell empty, while a computed
            // value is coerced like any other result.
            if (value is null && propertyName == nameof(FormField.DefaultValueExpression)) { return null; }
            return ExpressionPolicy.CoerceValue(value, field.DbType);
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
