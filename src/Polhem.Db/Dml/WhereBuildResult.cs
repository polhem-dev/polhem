namespace Polhem.Db.Dml
{
    /// <summary>
    /// Represents the result of building a WHERE clause.
    /// </summary>
    public sealed class WhereBuildResult
    {
        /// <summary>
        /// Gets the WHERE clause string (with or without the "WHERE" keyword).
        /// </summary>
        public string WhereClause { get; init; } = string.Empty;

        /// <summary>
        /// Gets the named parameters generated for the WHERE clause.
        /// </summary>
        public IDictionary<string, object>? Parameters { get; init; }
    }
}
