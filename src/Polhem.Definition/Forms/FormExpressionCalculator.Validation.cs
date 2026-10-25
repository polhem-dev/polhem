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
                var variableTypes = BuildVariableTypes(formTable);
                foreach (var field in formTable.Fields)
                {
                    ValidateExpression(issues, field.DefaultValueExpression, variableTypes, typeof(object),
                        formTable, field.FieldName, null, nameof(FormField.DefaultValueExpression));
                    ValidateExpression(issues, field.ValueExpression, variableTypes, typeof(object),
                        formTable, field.FieldName, null, nameof(FormField.ValueExpression));
                }
            }

            if (schema.Rules == null) { return issues; }
            foreach (var rule in schema.Rules)
            {
                var formTable = ResolveRuleTable(rule, schema);
                if (formTable?.Fields == null) { continue; }
                var variableTypes = BuildVariableTypes(formTable);
                ValidateExpression(issues, rule.When, variableTypes, typeof(bool),
                    formTable, null, rule.RuleId, nameof(FormRule.When));
                ValidateExpression(issues, rule.Condition, variableTypes, typeof(bool),
                    formTable, null, rule.RuleId, nameof(FormRule.Condition));
            }
            return issues;
        }

        private void ValidateExpression(List<FormExpressionIssue> issues, string expression,
            IReadOnlyDictionary<string, Type> variableTypes, Type returnType, FormTable formTable,
            string? fieldName, string? ruleId, string propertyName)
        {
            if (StringUtilities.IsEmpty(expression)) { return; }
            try
            {
                _evaluator.Validate(expression, variableTypes, returnType);
            }
            catch (ExpressionEvaluationException ex)
            {
                issues.Add(new FormExpressionIssue(formTable.TableName, fieldName, ruleId, propertyName,
                    expression, ex.Message));
            }
        }

        /// <summary>
        /// Builds the variable types a table's expressions are parsed against: each field by its declared name,
        /// typed as <see cref="BuildVariables"/> types it at run time.
        /// </summary>
        /// <remarks>
        /// The type is taken from the value <see cref="ExpressionPolicy.CoerceValue"/> produces for an empty
        /// cell, because the evaluator types each run-time variable by the value <see cref="BuildVariables"/>
        /// hands it, and that value always comes from <see cref="ExpressionPolicy.CoerceValue"/>. Deriving the
        /// type any other way would be a second statement of that rule, free to drift from the first.
        /// </remarks>
        private static Dictionary<string, Type> BuildVariableTypes(FormTable formTable)
        {
            var variableTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var field in formTable.Fields!)
            {
                if (StringUtilities.IsEmpty(field.FieldName)) { continue; }
                variableTypes[field.FieldName] = ExpressionPolicy.CoerceValue(null, field.DbType).GetType();
            }
            return variableTypes;
        }
    }
}
