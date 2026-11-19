using System.Globalization;
using Polhem.Core;
using Polhem.Core.Expressions;

namespace Polhem.Definition.Forms
{
    public sealed partial class FormExpressionCalculator
    {
        /// <summary>
        /// Parses every expression of <paramref name="schema"/> the way the run-time passes would, without
        /// evaluating any of them, and reports the ones that do not parse.
        /// </summary>
        /// <param name="schema">The form schema.</param>
        /// <returns>One issue per expression that does not parse; empty when every expression parses.</returns>
        /// <remarks>
        /// <para>
        /// Field expressions (<see cref="FormField.DefaultValueExpression"/>, <see cref="FormField.ValueExpression"/>)
        /// are parsed for an <see cref="object"/> result against the fields of their own table, as
        /// <see cref="ApplyFieldExpressions"/> evaluates them; rule expressions (<see cref="FormRule.When"/>,
        /// <see cref="FormRule.Condition"/>) are parsed for a <see cref="bool"/> result against the table the rule
        /// targets, as <see cref="ValidateRules"/> evaluates them. A rule whose target table does not exist is
        /// skipped, as it is at run time.
        /// </para>
        /// <para>
        /// A detail aggregate (<c>Sum(Table.field)</c>, <c>Count(Table)</c>, <c>Min</c>, <c>Max</c>, and the
        /// conditional <c>Count(Table, condition)</c>, <c>Any</c> and <c>All</c>) is checked as well: it is reported
        /// when it is used outside the master table's value expressions and rules, when it names a table or field the
        /// schema does not have, when the field's type cannot be aggregated, or when its condition does not parse as a
        /// bool over the aggregated table's fields.
        /// </para>
        /// <para>
        /// This is the check a definition editor runs before saving. On the client, an expression that fails
        /// to parse does not fail loudly: the live computation degrades and the computed field keeps its value.
        /// </para>
        /// </remarks>
        public IReadOnlyList<FormExpressionIssue> ValidateExpressions(FormSchema schema)
        {
            ArgumentNullException.ThrowIfNull(schema);

            var issues = new List<FormExpressionIssue>();
            if (schema.Tables == null) { return issues; }

            foreach (var formTable in schema.Tables)
            {
                if (formTable.Fields == null) { continue; }
                var variableTypes = GetVariableTypes(formTable);
                foreach (var field in formTable.Fields)
                {
                    ValidateFieldExpression(issues, schema, variableTypes, formTable, field,
                        field.DefaultValueExpression, nameof(FormField.DefaultValueExpression));
                    ValidateFieldExpression(issues, schema, variableTypes, formTable, field,
                        field.ValueExpression, nameof(FormField.ValueExpression));
                }
            }

            if (schema.Rules == null) { return issues; }
            foreach (var rule in schema.Rules)
            {
                var formTable = ResolveRuleTable(rule, schema);
                if (formTable?.Fields == null) { continue; }
                var variableTypes = GetVariableTypes(formTable);
                ValidateRuleExpression(issues, schema, variableTypes, formTable, rule, rule.When, nameof(FormRule.When));
                ValidateRuleExpression(issues, schema, variableTypes, formTable, rule,
                    rule.Condition, nameof(FormRule.Condition));
            }
            return issues;
        }

        /// <summary>
        /// Parses one expression of <paramref name="formTable"/> the way <see cref="ValidateExpressions"/> parses it,
        /// detail aggregates included, and throws when it does not parse.
        /// </summary>
        /// <param name="schema">The form schema that owns the table.</param>
        /// <param name="formTable">The table the expression is evaluated on: the field's table, or the table a rule
        /// targets (<see cref="ResolveRuleTable"/>).</param>
        /// <param name="propertyName">The property that holds the expression: <see cref="FormField.ValueExpression"/>,
        /// <see cref="FormField.DefaultValueExpression"/>, <see cref="FormRule.When"/> or <see cref="FormRule.Condition"/>.
        /// It decides the result type and whether aggregates are allowed.</param>
        /// <param name="expression">The expression; an empty one always passes.</param>
        /// <exception cref="ArgumentException"><paramref name="propertyName"/> names no expression property.</exception>
        /// <exception cref="ExpressionEvaluationException">
        /// The expression does not parse. <see cref="ExpressionEvaluationException.Position"/> is an offset in
        /// <paramref name="expression"/> as written, also when the expression holds aggregates; an aggregate that names
        /// no table or field, or that is not allowed where it is used, is reported at the start of the call.
        /// </exception>
        /// <remarks>
        /// A definition editor checks an expression with this as it is typed, so it reports exactly what
        /// <see cref="ValidateExpressions"/> reports when the form is validated or saved.
        /// </remarks>
        public void ValidateExpression(FormSchema schema, FormTable formTable, string propertyName, string expression)
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(formTable);
            ArgumentNullException.ThrowIfNull(expression);
            EnsureExpressionProperty(propertyName);

            if (FindExpressionError(schema, formTable, GetVariableTypes(formTable), propertyName, expression) is { } error)
                throw new ExpressionEvaluationException(error.Message) { Expression = expression, Position = error.Position };
        }

        private void ValidateFieldExpression(List<FormExpressionIssue> issues, FormSchema schema,
            IReadOnlyDictionary<string, Type> variableTypes, FormTable formTable, FormField field,
            string expression, string propertyName)
        {
            if (FindExpressionError(schema, formTable, variableTypes, propertyName, expression) is not { } error) { return; }
            issues.Add(new FormExpressionIssue(formTable.TableName, field.FieldName, null, propertyName,
                expression, error.Message));
        }

        private void ValidateRuleExpression(List<FormExpressionIssue> issues, FormSchema schema,
            IReadOnlyDictionary<string, Type> variableTypes, FormTable formTable, FormRule rule,
            string expression, string propertyName)
        {
            if (FindExpressionError(schema, formTable, variableTypes, propertyName, expression) is not { } error) { return; }
            issues.Add(new FormExpressionIssue(formTable.TableName, null, rule.RuleId, propertyName,
                expression, error.Message));
        }

        // The one check behind both `ValidateExpressions` and `ValidateExpression`: the aggregates of an expression
        // first, then a parse of the rewritten expression with one variable per aggregate, typed as the aggregate is
        // computed. A rule expression must produce a bool; a field expression may produce anything.
        private (string Message, int? Position)? FindExpressionError(FormSchema schema, FormTable formTable,
            IReadOnlyDictionary<string, Type> variableTypes, string propertyName, string expression)
        {
            if (StringUtilities.IsEmpty(expression)) { return null; }
            var returnType = ReturnTypeOf(propertyName);
            var parsed = ParseAggregates(expression);
            if (!parsed.HasAggregates) { return FindParseError(expression, variableTypes, returnType, parsed); }

            var firstCall = parsed.Spans[0].OriginalStart;
            if (propertyName == nameof(FormField.DefaultValueExpression))
                return ("An aggregate such as Sum(Table.field) cannot be used in a default value expression; use a value expression.",
                    firstCall);
            if (!IsMasterTable(schema, formTable)) { return (AggregateOutsideMasterMessage(formTable), firstCall); }

            var types = new Dictionary<string, Type>(variableTypes, StringComparer.Ordinal);
            foreach (var aggregate in parsed.Aggregates)
            {
                var (detailTable, field, error) = ResolveAggregate(aggregate, schema);
                if (error is not null) { return (error, parsed.OriginalStartOf(aggregate)); }
                if (aggregate.HasCondition
                    && FindConditionError(aggregate, detailTable!, parsed.ConditionStartOf(aggregate)) is { } conditionError)
                    return conditionError;
                types[aggregate.VariableName] = AggregateType(aggregate.Function, field);
            }
            return FindParseError(parsed.Expression, types, returnType, parsed);
        }

        // A condition of `Count`, `Any` or `All` is a bool expression over the aggregated table's fields, evaluated
        // once per detail row; it cannot aggregate again. Its errors are reported where the condition is written.
        private (string Message, int? Position)? FindConditionError(DetailAggregate aggregate, FormTable detailTable,
            int conditionStart)
        {
            if (ParseAggregates(aggregate.Condition).HasAggregates)
                return ($"{DescribeCall(aggregate)}: a condition reads one row of '{detailTable.TableName}' and cannot hold an aggregate.",
                    conditionStart);
            try
            {
                _evaluator.Validate(aggregate.Condition, GetVariableTypes(detailTable), typeof(bool));
                return null;
            }
            catch (ExpressionEvaluationException ex)
            {
                if (ex.Position is not int position) { return (ex.Message, conditionStart); }
                var message = ex.Message.Replace(
                    string.Create(CultureInfo.InvariantCulture, $"(at index {position})"),
                    string.Create(CultureInfo.InvariantCulture, $"(at index {conditionStart + position})"),
                    StringComparison.Ordinal);
                return (message, conditionStart + position);
            }
        }

        // The engine reports positions in the text it parsed, which for an expression with aggregates is the
        // rewritten one. The position, and the index the engine writes into its message, are mapped back to the text
        // as written.
        private (string Message, int? Position)? FindParseError(string expression,
            IReadOnlyDictionary<string, Type> variableTypes, Type returnType, DetailAggregateExpression parsed)
        {
            try
            {
                _evaluator.Validate(expression, variableTypes, returnType);
                return null;
            }
            catch (ExpressionEvaluationException ex)
            {
                if (ex.Position is not int position || !parsed.HasAggregates) { return (ex.Message, ex.Position); }
                var original = parsed.ToOriginalPosition(position);
                var message = ex.Message.Replace(
                    string.Create(CultureInfo.InvariantCulture, $"(at index {position})"),
                    string.Create(CultureInfo.InvariantCulture, $"(at index {original})"), StringComparison.Ordinal);
                return (message, original);
            }
        }

        /// <summary>
        /// Gets the variable types a table's expressions are parsed against: each field by its declared name,
        /// typed as <see cref="BuildVariables"/> types it at run time.
        /// </summary>
        /// <param name="formTable">The table whose fields are the variables.</param>
        /// <returns>The variable types, keyed by <see cref="FormField.FieldName"/> with ordinal comparison.</returns>
        /// <remarks>
        /// The type is taken from the value <see cref="ExpressionPolicy.CoerceValue"/> produces for an empty
        /// cell, because the evaluator types each run-time variable by the value <see cref="BuildVariables"/>
        /// hands it, and that value always comes from <see cref="ExpressionPolicy.CoerceValue"/>. Deriving the
        /// type any other way would be a second statement of that rule, free to drift from the first.
        /// </remarks>
        public static IReadOnlyDictionary<string, Type> GetVariableTypes(FormTable formTable)
        {
            ArgumentNullException.ThrowIfNull(formTable);
            var variableTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
            if (formTable.Fields == null) { return variableTypes; }
            foreach (var field in formTable.Fields)
            {
                if (StringUtilities.IsEmpty(field.FieldName)) { continue; }
                variableTypes[field.FieldName] = ExpressionPolicy.CoerceValue(null, field.DbType).GetType();
            }
            return variableTypes;
        }
    }
}
