namespace Polhem.Definition.Forms
{
    /// <summary>
    /// One aggregate call found in an expression, and the variable that stands in for it once the expression is
    /// rewritten.
    /// </summary>
    /// <param name="Function">The aggregate.</param>
    /// <param name="TableName">The detail table, as written in the expression.</param>
    /// <param name="FieldName">The detail field, as written; empty for <see cref="DetailAggregateFunction.Count"/>.</param>
    /// <param name="VariableName">The variable the rewritten expression reads the aggregate's value from.</param>
    internal sealed record DetailAggregate(
        DetailAggregateFunction Function, string TableName, string FieldName, string VariableName);
}
