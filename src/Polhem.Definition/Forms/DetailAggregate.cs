namespace Polhem.Definition.Forms
{
    /// <summary>
    /// One aggregate call found in an expression, and the variable that stands in for it once the expression is
    /// rewritten.
    /// </summary>
    /// <param name="Function">The aggregate.</param>
    /// <param name="TableName">The detail table, as written in the expression.</param>
    /// <param name="FieldName">The detail field, as written; empty for <see cref="DetailAggregateFunction.Count"/>,
    /// <see cref="DetailAggregateFunction.Any"/> and <see cref="DetailAggregateFunction.All"/>.</param>
    /// <param name="VariableName">The variable the rewritten expression reads the aggregate's value from.</param>
    internal sealed record DetailAggregate(
        DetailAggregateFunction Function, string TableName, string FieldName, string VariableName)
    {
        /// <summary>
        /// Gets the condition each detail row is tested with, as written and trimmed; empty when the call has none.
        /// It names the detail table's fields, the way a rule targeting that table does.
        /// </summary>
        public string Condition { get; init; } = string.Empty;

        /// <summary>Gets a value indicating whether the call tests each row with <see cref="Condition"/>.</summary>
        public bool HasCondition => Condition.Length > 0;
    }
}
