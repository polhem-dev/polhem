namespace Polhem.Definition.Forms
{
    /// <summary>
    /// An aggregate a master table's expression applies to the rows of a detail table.
    /// </summary>
    internal enum DetailAggregateFunction
    {
        /// <summary>The sum of a numeric field, <c>Sum(Table.field)</c>.</summary>
        Sum,
        /// <summary>The number of rows, <c>Count(Table)</c>.</summary>
        Count,
        /// <summary>The smallest value of a numeric or date field, <c>Min(Table.field)</c>.</summary>
        Min,
        /// <summary>The largest value of a numeric or date field, <c>Max(Table.field)</c>.</summary>
        Max,
    }
}
