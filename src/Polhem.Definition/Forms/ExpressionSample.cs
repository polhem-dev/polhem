namespace Polhem.Definition.Forms
{
    /// <summary>
    /// The sample values an expression is tried against, outside a data set: what a definition editor or a rule
    /// assistant passes to the sample methods of <see cref="FormExpressionCalculator"/>.
    /// </summary>
    /// <remarks>
    /// Each value is coerced to its field's type as a cell read from the database is. A later input for a sample
    /// becomes a property here, so the methods that take one keep their signatures.
    /// </remarks>
    public sealed class ExpressionSample
    {
        /// <summary>
        /// Gets or initializes the values of the row the expression reads, by field name; a field without one is
        /// empty.
        /// </summary>
        public required IReadOnlyDictionary<string, object?> Values { get; init; }

        /// <summary>
        /// Gets or initializes sample rows of the detail tables by table name, each row its values by field name.
        /// The default is no rows.
        /// </summary>
        /// <remarks>
        /// Only <see cref="FormExpressionCalculator.EvaluateMasterRuleSample"/> reads them. The other sample methods
        /// evaluate one row of a table, where a detail aggregate takes the value it has over no rows.
        /// </remarks>
        public IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> DetailRows { get; init; } =
            new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>();

        /// <summary>
        /// Gets or initializes the IANA time zone id the <c>Today()</c> and <c>Now()</c> helpers see, in the
        /// expression and in its aggregates' conditions. The default, an empty string, means UTC.
        /// </summary>
        public string TimeZoneId { get; init; } = string.Empty;
    }
}
