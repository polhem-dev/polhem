namespace Polhem.Definition.Forms
{
    /// <summary>
    /// An aggregate a master table's expression applies to the rows of a detail table.
    /// </summary>
    internal enum DetailAggregateFunction
    {
        /// <summary>The sum of a numeric field, <c>Sum(Table.field)</c>.</summary>
        Sum,
        /// <summary>The number of rows, <c>Count(Table)</c>, or of the rows a condition holds for, <c>Count(Table, condition)</c>.</summary>
        Count,
        /// <summary>The smallest value of a numeric or date field, <c>Min(Table.field)</c>.</summary>
        Min,
        /// <summary>The largest value of a numeric or date field, <c>Max(Table.field)</c>.</summary>
        Max,
        /// <summary>Whether a condition holds for at least one row, <c>Any(Table, condition)</c>; false over no rows.</summary>
        Any,
        /// <summary>Whether a condition holds for every row, <c>All(Table, condition)</c>; true over no rows.</summary>
        All,
    }
}
