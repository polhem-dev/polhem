using Polhem.Definition.Filters;

namespace Polhem.Db.Dml
{
    /// <summary>
    /// Defines a form-related SQL command builder that generates Select, Count, and Delete statements.
    /// </summary>
    public interface IFormCommandBuilder
    {
        /// <summary>
        /// Builds the SELECT command specification.
        /// </summary>
        /// <param name="tableName">The table name.</param>
        /// <param name="query">The fields, the filter, the order and the page to select.</param>
        DbCommandSpec BuildSelect(string tableName, FormSelectQuery query);

        /// <summary>
        /// Builds the SELECT COUNT(*) command specification for the specified table using the supplied filter.
        /// </summary>
        /// <param name="tableName">The form table name.</param>
        /// <param name="filter">The filter condition; <c>null</c> counts every row.</param>
        DbCommandSpec BuildCount(string tableName, FilterNode? filter);

        /// <summary>
        /// Builds the DELETE command specification for the specified table using the supplied filter.
        /// </summary>
        /// <param name="tableName">The form table name.</param>
        /// <param name="filter">The filter that becomes the WHERE clause; must not be null.</param>
        DbCommandSpec BuildDelete(string tableName, FilterNode filter);
    }
}
