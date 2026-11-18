namespace Polhem.Definition.Forms
{
    /// <summary>
    /// An expression with its aggregate calls replaced by variables, ready for the expression engine.
    /// </summary>
    /// <param name="Expression">The rewritten expression; the original when it holds no aggregate.</param>
    /// <param name="Aggregates">The aggregates, one per distinct call, in the order they first appear.</param>
    /// <param name="Spans">Every replaced call, in the order it appears; a call repeated in the text has a span each time.</param>
    internal sealed record DetailAggregateExpression(string Expression, IReadOnlyList<DetailAggregate> Aggregates,
        IReadOnlyList<DetailAggregateSpan> Spans)
    {
        /// <summary>Gets a value indicating whether the expression holds an aggregate.</summary>
        public bool HasAggregates => Aggregates.Count > 0;

        /// <summary>
        /// Maps an offset in <see cref="Expression"/> to the offset in the original expression: a position inside a
        /// variable maps to the start of the call it replaced.
        /// </summary>
        /// <param name="position">An offset in the rewritten expression.</param>
        /// <returns>The offset in the original expression.</returns>
        public int ToOriginalPosition(int position)
        {
            var shift = 0;
            foreach (var span in Spans)
            {
                if (position < span.RewrittenStart) { break; }
                if (position < span.RewrittenStart + span.VariableName.Length) { return span.OriginalStart; }
                shift = span.OriginalStart + span.OriginalLength - (span.RewrittenStart + span.VariableName.Length);
            }
            return position + shift;
        }

        /// <summary>The offset in the original expression of the first call that <paramref name="aggregate"/> replaced.</summary>
        /// <param name="aggregate">One of <see cref="Aggregates"/>.</param>
        /// <returns>The offset of its first call.</returns>
        public int OriginalStartOf(DetailAggregate aggregate) =>
            Spans.First(span => span.VariableName == aggregate.VariableName).OriginalStart;

        /// <summary>The offset in the original expression of the condition of the first call that <paramref name="aggregate"/> replaced.</summary>
        /// <param name="aggregate">One of <see cref="Aggregates"/>, with a condition.</param>
        /// <returns>The offset of its condition, or of the call when it has none.</returns>
        public int ConditionStartOf(DetailAggregate aggregate)
        {
            var span = Spans.First(span => span.VariableName == aggregate.VariableName);
            return span.ConditionStart >= 0 ? span.ConditionStart : span.OriginalStart;
        }
    }
}
