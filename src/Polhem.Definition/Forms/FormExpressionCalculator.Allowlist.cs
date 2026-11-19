using Polhem.Core;
using Polhem.Core.Expressions;

namespace Polhem.Definition.Forms
{
    public sealed partial class FormExpressionCalculator
    {
        private const string NoAllowlistMessage =
            "The expression evaluator cannot check expressions against the allowlist, so none can be accepted from an untrusted author.";

        /// <summary>
        /// Lists the expressions of <paramref name="schema"/> that use anything outside the allowlist of
        /// <see cref="IExpressionAllowlist"/>, one issue per finding.
        /// </summary>
        /// <param name="schema">The form schema.</param>
        /// <returns>The findings; empty when every expression stays within the allowlist.</returns>
        /// <remarks>
        /// <para>
        /// The expressions are the ones <see cref="ValidateExpressions"/> checks, with their detail aggregates and the
        /// aggregates' conditions. An expression that does not parse yields no finding here; it is reported by
        /// <see cref="ValidateExpressions"/>.
        /// </para>
        /// <para>
        /// What a finding means depends on the author. In a definition file written by a developer it is a warning:
        /// the expression works, and only an untrusted author would be refused it. In expressions a language model
        /// wrote it is an error. When the evaluator does not implement <see cref="IExpressionAllowlist"/>, every
        /// non-empty expression is reported, so a caller that enforces the allowlist refuses them all.
        /// </para>
        /// </remarks>
        public IReadOnlyList<FormExpressionIssue> FindAllowlistViolations(FormSchema schema)
        {
            ArgumentNullException.ThrowIfNull(schema);
            var issues = new List<FormExpressionIssue>();
            if (schema.Tables is not null)
            {
                foreach (var formTable in schema.Tables)
                {
                    if (formTable.Fields is null) { continue; }
                    foreach (var field in formTable.Fields)
                    {
                        AddViolations(issues, schema, formTable, field.FieldName, null,
                            nameof(FormField.DefaultValueExpression), field.DefaultValueExpression);
                        AddViolations(issues, schema, formTable, field.FieldName, null,
                            nameof(FormField.ValueExpression), field.ValueExpression);
                    }
                }
            }
            if (schema.Rules is null) { return issues; }
            foreach (var rule in schema.Rules)
            {
                var formTable = ResolveRuleTable(rule, schema);
                if (formTable?.Fields is null) { continue; }
                AddViolations(issues, schema, formTable, null, rule.RuleId, nameof(FormRule.When), rule.When);
                AddViolations(issues, schema, formTable, null, rule.RuleId, nameof(FormRule.Condition), rule.Condition);
            }
            return issues;
        }

        /// <summary>
        /// Returns what one expression of <paramref name="formTable"/> uses outside the allowlist of
        /// <see cref="IExpressionAllowlist"/>, with each position an offset in <paramref name="expression"/> as written.
        /// </summary>
        /// <param name="schema">The form schema that owns the table.</param>
        /// <param name="formTable">The table the expression is evaluated on, as for <see cref="ValidateExpression"/>.</param>
        /// <param name="propertyName">The property that holds the expression, as for <see cref="ValidateExpression"/>.</param>
        /// <param name="expression">The expression; an empty one has no finding.</param>
        /// <returns>The findings; empty when the expression stays within the allowlist or does not parse.</returns>
        /// <exception cref="ArgumentException"><paramref name="propertyName"/> names no expression property.</exception>
        /// <remarks>
        /// The size limits are checked on the whole text before any part of it is parsed. A detail aggregate is
        /// checked as the variable it becomes, and its condition against the detail table's fields.
        /// </remarks>
        public IReadOnlyList<ExpressionViolation> FindAllowlistViolations(FormSchema schema, FormTable formTable,
            string propertyName, string expression)
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(formTable);
            ArgumentNullException.ThrowIfNull(expression);
            EnsureExpressionProperty(propertyName);
            if (StringUtilities.IsEmpty(expression)) { return []; }
            if (_evaluator is not IExpressionAllowlist allowlist) { return [new ExpressionViolation(NoAllowlistMessage, null)]; }

            var variableTypes = GetVariableTypes(formTable);
            var returnType = ReturnTypeOf(propertyName);
            // The whole text first, so its size limits hold before anything is parsed. With an aggregate in it the text
            // does not parse as it stands, so only a size finding comes back from this call.
            var whole = allowlist.FindViolations(expression, variableTypes, returnType);
            var parsed = ParseAggregates(expression);
            if (whole.Count > 0 || !parsed.HasAggregates) { return whole; }
            if (propertyName == nameof(FormField.DefaultValueExpression) || !IsMasterTable(schema, formTable)) { return []; }

            var types = new Dictionary<string, Type>(variableTypes, StringComparer.Ordinal);
            foreach (var aggregate in parsed.Aggregates)
            {
                var (_, field, error) = ResolveAggregate(aggregate, schema);
                if (error is not null) { return []; }
                types[aggregate.VariableName] = AggregateType(aggregate.Function, field);
            }

            var violations = allowlist.FindViolations(parsed.Expression, types, returnType)
                .Select(v => new ExpressionViolation(v.Message,
                    v.Position is int position ? parsed.ToOriginalPosition(position) : null))
                .ToList();
            foreach (var aggregate in parsed.Aggregates.Where(a => a.HasCondition))
            {
                var detailTable = schema.Tables![aggregate.TableName];
                var conditionStart = parsed.ConditionStartOf(aggregate);
                violations.AddRange(allowlist.FindViolations(aggregate.Condition, GetVariableTypes(detailTable), typeof(bool))
                    .Select(v => new ExpressionViolation(v.Message,
                        v.Position is int position ? conditionStart + position : conditionStart)));
            }
            return violations.OrderBy(v => v.Position ?? int.MaxValue).ToList();
        }

        /// <summary>
        /// Checks an expression from an untrusted author, such as a language model or a user of the application:
        /// within the size limits, parsing as <see cref="ValidateExpression"/> parses it, and within the allowlist.
        /// </summary>
        /// <param name="schema">The form schema that owns the table.</param>
        /// <param name="formTable">The table the expression is evaluated on, as for <see cref="ValidateExpression"/>.</param>
        /// <param name="propertyName">The property that holds the expression, as for <see cref="ValidateExpression"/>.</param>
        /// <param name="expression">The expression; an empty one always passes.</param>
        /// <exception cref="ArgumentException"><paramref name="propertyName"/> names no expression property.</exception>
        /// <exception cref="ExpressionEvaluationException">
        /// The expression is too large, does not parse, or uses something outside the allowlist; also when the
        /// evaluator does not implement <see cref="IExpressionAllowlist"/>.
        /// </exception>
        /// <remarks>
        /// IMPORTANT: the order matters. The size limits are checked before the expression is parsed, because a deep
        /// enough nesting overflows the parser's stack, which ends the process instead of throwing.
        /// </remarks>
        public void ValidateUntrustedExpression(FormSchema schema, FormTable formTable, string propertyName,
            string expression)
        {
            var violations = FindAllowlistViolations(schema, formTable, propertyName, expression);
            if (violations.Count > 0)
            {
                var first = violations[0];
                throw new ExpressionEvaluationException(first.Message) { Expression = expression, Position = first.Position };
            }
            // An expression that does not parse has no allowlist finding, so this is what reports it.
            ValidateExpression(schema, formTable, propertyName, expression);
        }

        private void AddViolations(List<FormExpressionIssue> issues, FormSchema schema, FormTable formTable,
            string? fieldName, string? ruleId, string propertyName, string expression)
        {
            foreach (var violation in FindAllowlistViolations(schema, formTable, propertyName, expression))
                issues.Add(new FormExpressionIssue(formTable.TableName, fieldName, ruleId, propertyName, expression,
                    violation.Message));
        }

        private static Type ReturnTypeOf(string propertyName) =>
            propertyName is nameof(FormRule.When) or nameof(FormRule.Condition) ? typeof(bool) : typeof(object);

        private static void EnsureExpressionProperty(string propertyName)
        {
            if (propertyName is not (nameof(FormField.ValueExpression) or nameof(FormField.DefaultValueExpression)
                or nameof(FormRule.When) or nameof(FormRule.Condition)))
                throw new ArgumentException($"'{propertyName}' is not an expression property.", nameof(propertyName));
        }
    }
}
