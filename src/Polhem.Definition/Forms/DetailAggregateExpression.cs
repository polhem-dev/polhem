namespace Polhem.Definition.Forms
{
    /// <summary>
    /// An expression with its aggregate calls replaced by variables, ready for the expression engine.
    /// </summary>
    /// <param name="Expression">The rewritten expression; the original when it holds no aggregate.</param>
    /// <param name="Aggregates">The aggregates, one per distinct call, in the order they first appear.</param>
    internal sealed record DetailAggregateExpression(string Expression, IReadOnlyList<DetailAggregate> Aggregates)
    {
        /// <summary>Gets a value indicating whether the expression holds an aggregate.</summary>
        public bool HasAggregates => Aggregates.Count > 0;
    }
}
