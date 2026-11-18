using System.Data;
using System.Globalization;
using Polhem.Definition.Filters;

namespace Avalonia.DemoCenter.Modules.WithoutBackend
{
    /// <summary>
    /// Applies a list request's <see cref="FilterNode"/> to in-memory rows, as a backend applies it in SQL.
    /// </summary>
    /// <remarks>
    /// Values are compared after a light normalisation: a <see cref="DateOnly"/> reads as a <see cref="DateTime"/> at
    /// midnight, every number as a <see cref="decimal"/>, and text ignoring case, the way the databases the framework
    /// supports compare it by default.
    /// </remarks>
    internal static class InMemoryFilter
    {
        /// <summary>Returns the rows of <paramref name="rows"/> that <paramref name="filter"/> admits.</summary>
        /// <param name="rows">The rows.</param>
        /// <param name="filter">The filter; <c>null</c> admits every row.</param>
        /// <returns>A copy of the table holding the admitted rows.</returns>
        public static DataTable Apply(DataTable rows, FilterNode? filter)
        {
            var result = rows.Clone();
            foreach (DataRow row in rows.Rows)
            {
                if (row.RowState != DataRowState.Deleted && Matches(row, filter))
                    result.ImportRow(row);
            }
            result.AcceptChanges();
            return result;
        }

        private static bool Matches(DataRow row, FilterNode? node) => node switch
        {
            null => true,
            FilterGroup { Operator: LogicalOperator.Or } group => group.Nodes.Any(n => Matches(row, n)),
            FilterGroup group => group.Nodes.All(n => Matches(row, n)),
            FilterCondition condition => Matches(row, condition),
            _ => throw new NotSupportedException($"Unknown filter node {node.GetType().Name}."),
        };

        private static bool Matches(DataRow row, FilterCondition condition)
        {
            var value = Normalize(row[condition.FieldName]);
            if (condition.IgnoreIfNull && condition.Value is null) return true;
            var operand = Normalize(condition.Value);
            return condition.Operator switch
            {
                ComparisonOperator.Equal => Compare(value, operand) == 0,
                ComparisonOperator.NotEqual => Compare(value, operand) != 0,
                ComparisonOperator.GreaterThan => Compare(value, operand) > 0,
                ComparisonOperator.GreaterThanOrEqual => Compare(value, operand) >= 0,
                ComparisonOperator.LessThan => Compare(value, operand) < 0,
                ComparisonOperator.LessThanOrEqual => Compare(value, operand) <= 0,
                ComparisonOperator.Between => Compare(value, operand) >= 0 && Compare(value, Normalize(condition.SecondValue)) <= 0,
                ComparisonOperator.In => condition.Value is IEnumerable<object> values && values.Any(v => Compare(value, Normalize(v)) == 0),
                ComparisonOperator.StartsWith => Text(value).StartsWith(Text(operand), StringComparison.OrdinalIgnoreCase),
                ComparisonOperator.EndsWith => Text(value).EndsWith(Text(operand), StringComparison.OrdinalIgnoreCase),
                ComparisonOperator.Contains or ComparisonOperator.Like
                    => Text(value).Contains(Text(operand).Trim('%'), StringComparison.OrdinalIgnoreCase),
                _ => throw new NotSupportedException($"Unknown comparison operator {condition.Operator}."),
            };
        }

        private static object? Normalize(object? value) => value switch
        {
            null or DBNull => null,
            DateOnly day => day.ToDateTime(TimeOnly.MinValue),
            byte or short or int or long or float or double or decimal => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
            _ => value,
        };

        private static int Compare(object? left, object? right) => (left, right) switch
        {
            (null, null) => 0,
            (null, _) => -1,
            (_, null) => 1,
            (string a, string b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase),
            (IComparable a, _) when a.GetType() == right.GetType() => a.CompareTo(right),
            _ => string.Compare(Text(left), Text(right), StringComparison.OrdinalIgnoreCase),
        };

        private static string Text(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
