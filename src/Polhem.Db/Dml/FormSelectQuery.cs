using Polhem.Definition.Filters;
using Polhem.Definition.Sorting;

namespace Polhem.Db.Dml
{
    /// <summary>
    /// What <see cref="IFormCommandBuilder.BuildSelect"/> selects from a form table: the fields, the filter, the
    /// order and the page.
    /// </summary>
    /// <remarks>
    /// A new instance selects every field of every row, unordered and unpaged. A later input becomes a property here,
    /// so the interface member keeps its signature.
    /// </remarks>
    public sealed record FormSelectQuery
    {
        /// <summary>
        /// Gets or initializes a comma-separated list of the fields to retrieve; an empty string, the default, retrieves
        /// every field.
        /// </summary>
        public string SelectFields { get; init; } = string.Empty;

        /// <summary>
        /// Gets or initializes the filter condition; <c>null</c>, the default, selects every row.
        /// </summary>
        public FilterNode? Filter { get; init; }

        /// <summary>
        /// Gets or initializes the sort fields; <c>null</c>, the default, leaves the order to the database.
        /// </summary>
        public SortFieldCollection? SortFields { get; init; }

        /// <summary>
        /// Gets or initializes the rows to skip; <c>null</c>, the default, means no offset.
        /// </summary>
        public int? Skip { get; init; }

        /// <summary>
        /// Gets or initializes the rows to take; <c>null</c>, the default, means no row limit.
        /// </summary>
        public int? Take { get; init; }
    }
}
