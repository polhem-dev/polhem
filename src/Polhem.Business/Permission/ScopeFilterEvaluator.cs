using System.Data;
using System.Globalization;
using Polhem.Base;
using Polhem.Definition.Filters;

namespace Polhem.Business.Permission
{
    /// <summary>
    /// Evaluates a record-scope filter against the values of one in-memory row.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The database answers the scope question for stored rows. A row that is about to be inserted,
    /// or the new values of a row about to be updated, are not in the database yet, so the same filter
    /// is evaluated here instead.
    /// </para>
    /// <para>
    /// Only the shapes <see cref="ScopeResolver"/> produces are understood: <c>Equal</c> and <c>In</c>
    /// conditions combined by <c>And</c> / <c>Or</c> groups. Any other operator evaluates to
    /// <c>false</c>, so a filter this class cannot read refuses the row rather than admitting it. A
    /// custom <see cref="Polhem.Definition.Identity.IScopeResolver"/> that emits other operators
    /// therefore makes new and changed rows fail the scope check.
    /// </para>
    /// </remarks>
    internal static class ScopeFilterEvaluator
    {
        /// <summary>
        /// Determines whether the row's values satisfy the filter.
        /// </summary>
        /// <param name="filter">The record-scope filter.</param>
        /// <param name="row">The row to test.</param>
        /// <param name="version">The row version whose values are tested.</param>
        /// <returns><c>true</c> when the row is inside the scope the filter describes.</returns>
        public static bool Matches(FilterNode filter, DataRow row, DataRowVersion version) => filter switch
        {
            FilterGroup group => MatchesGroup(group, row, version),
            FilterCondition condition => MatchesCondition(condition, row, version),
            _ => false,
        };

        private static bool MatchesGroup(FilterGroup group, DataRow row, DataRowVersion version)
            => group.Operator == LogicalOperator.Or
                ? group.Nodes.Any(node => Matches(node, row, version))
                : group.Nodes.All(node => Matches(node, row, version));

        private static bool MatchesCondition(FilterCondition condition, DataRow row, DataRowVersion version)
        {
            // A column the row does not carry is written as empty, so it is tested as empty.
            object? value = row.Table.Columns.Contains(condition.FieldName)
                ? row[condition.FieldName, version]
                : null;

            return condition.Operator switch
            {
                ComparisonOperator.Equal => ValueEquals(value, condition.Value),
                ComparisonOperator.In => condition.Value is IEnumerable<object> candidates
                    && candidates.Any(candidate => ValueEquals(value, candidate)),
                _ => false,
            };
        }

        private static bool ValueEquals(object? rowValue, object? filterValue)
        {
            // The scope columns hold row ids. A Guid column can arrive as a Guid, a string or, from
            // Oracle, a byte array, and `CGuid` reads all three.
            if (filterValue is Guid expected)
                return expected != Guid.Empty && rowValue is not null && ValueUtilities.CGuid(rowValue) == expected;

            if (rowValue is null or DBNull || filterValue is null) { return false; }
            return string.Equals(
                Convert.ToString(rowValue, CultureInfo.InvariantCulture),
                Convert.ToString(filterValue, CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        }
    }
}
