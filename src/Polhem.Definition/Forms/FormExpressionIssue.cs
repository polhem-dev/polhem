namespace Polhem.Definition.Forms
{
    /// <summary>
    /// An expression of a <see cref="FormSchema"/> that does not parse, reported by
    /// <see cref="FormExpressionCalculator.ValidateExpressions"/>.
    /// </summary>
    public sealed class FormExpressionIssue
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FormExpressionIssue"/>.
        /// </summary>
        /// <param name="tableName">The table the expression was parsed against.</param>
        /// <param name="fieldName">The field that holds the expression; <c>null</c> for a rule.</param>
        /// <param name="ruleId">The rule that holds the expression; <c>null</c> for a field.</param>
        /// <param name="propertyName">The property that holds the expression, such as <c>ValueExpression</c>.</param>
        /// <param name="expression">The expression text.</param>
        /// <param name="message">Why the expression does not parse.</param>
        public FormExpressionIssue(string tableName, string? fieldName, string? ruleId, string propertyName,
            string expression, string message)
        {
            TableName = tableName;
            FieldName = fieldName;
            RuleId = ruleId;
            PropertyName = propertyName;
            Expression = expression;
            Message = message;
        }

        /// <summary>
        /// Gets the table the expression was parsed against: the field's table, or the table a rule targets.
        /// </summary>
        public string TableName { get; }

        /// <summary>
        /// Gets the field that holds the expression, or <c>null</c> when a rule holds it.
        /// </summary>
        public string? FieldName { get; }

        /// <summary>
        /// Gets the rule that holds the expression, or <c>null</c> when a field holds it.
        /// </summary>
        public string? RuleId { get; }

        /// <summary>
        /// Gets the name of the property that holds the expression: <see cref="FormField.ValueExpression"/> or
        /// <see cref="FormField.DefaultValueExpression"/> on a field, <see cref="FormRule.When"/> or
        /// <see cref="FormRule.Condition"/> on a rule.
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// Gets the expression text.
        /// </summary>
        public string Expression { get; }

        /// <summary>
        /// Gets why the expression does not parse, as the expression engine reports it.
        /// </summary>
        public string Message { get; }
    }
}
