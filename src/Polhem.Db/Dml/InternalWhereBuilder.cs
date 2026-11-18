using System.Globalization;
using System.Text;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;

namespace Polhem.Db.Dml
{
    /// <summary>
    /// Internal shared utility for converting filter nodes to SQL condition strings.
    /// </summary>
    internal static class InternalWhereBuilder
    {
        private const string LikeOperator = " LIKE ";

        // The escape character of the Contains, StartsWith and EndsWith patterns. Not a backslash: MySQL and
        // PostgreSQL read a backslash in a string literal differently, and the clause is written into the SQL text.
        private const char LikeEscape = '!';
        private const string LikeEscapeClause = " ESCAPE '!'";

        /// <summary>
        /// Converts a filter node to a SQL condition string.
        /// </summary>
        /// <param name="node">The filter node to convert (may be a <see cref="FilterGroup"/> or <see cref="FilterCondition"/>).</param>
        /// <param name="parameters">The parameter collector used to gather SQL parameter values.</param>
        /// <param name="databaseType">The database, which decides the characters a pattern escapes.</param>
        /// <returns>The generated SQL condition string; returns an empty string if the node is empty or all child nodes are empty.</returns>
        public static string BuildNode(FilterNode node, IParameterCollector parameters, DatabaseType databaseType)
        {
            var group = node as FilterGroup;
            if (group != null)
            {
                var parts = new List<string>();
                for (int i = 0; i < group.Nodes.Count; i++)
                {
                    var child = group.Nodes[i];
                    var s = BuildNode(child, parameters, databaseType);
                    if (!string.IsNullOrEmpty(s)) parts.Add(s);
                }
                if (parts.Count == 0) return string.Empty;
                var joiner = group.Operator == LogicalOperator.And ? " AND " : " OR ";
                return "(" + string.Join(joiner, parts.ToArray()) + ")";
            }

            var cond = node as FilterCondition;
            if (cond != null) return BuildCondition(cond, parameters, databaseType);

            throw new NotSupportedException("Unknown filter node type.");
        }

        private static string BuildCondition(FilterCondition c, IParameterCollector parameters, DatabaseType databaseType)
        {
            if (string.IsNullOrEmpty(c.FieldName))
                throw new InvalidOperationException("Field name cannot be null or empty.");

            if (c.Value == null)
                return BuildNullCondition(c);

            return c.Operator switch
            {
                ComparisonOperator.Equal => c.FieldName + " = " + parameters.Add(c.Value),
                ComparisonOperator.NotEqual => c.FieldName + " <> " + parameters.Add(c.Value),
                ComparisonOperator.GreaterThan => c.FieldName + " > " + parameters.Add(c.Value),
                ComparisonOperator.GreaterThanOrEqual => c.FieldName + " >= " + parameters.Add(c.Value),
                ComparisonOperator.LessThan => c.FieldName + " < " + parameters.Add(c.Value),
                ComparisonOperator.LessThanOrEqual => c.FieldName + " <= " + parameters.Add(c.Value),
                ComparisonOperator.Like => c.FieldName + LikeOperator + parameters.Add(c.Value),
                ComparisonOperator.Contains => BuildPattern(c, "%" + EscapePattern(c.Value, databaseType) + "%", parameters),
                ComparisonOperator.StartsWith => BuildPattern(c, EscapePattern(c.Value, databaseType) + "%", parameters),
                ComparisonOperator.EndsWith => BuildPattern(c, "%" + EscapePattern(c.Value, databaseType), parameters),
                ComparisonOperator.In => BuildInCondition(c, parameters),
                ComparisonOperator.Between => BuildBetweenCondition(c, parameters),
                _ => throw new NotSupportedException("Unsupported comparison operator."),
            };
        }

        private static string BuildPattern(FilterCondition c, string pattern, IParameterCollector parameters)
            => c.FieldName + LikeOperator + parameters.Add(pattern) + LikeEscapeClause;

        /// <summary>
        /// Escapes what a value would otherwise make a wildcard in a Contains, StartsWith or EndsWith pattern: the
        /// single-character <c>_</c>, the escape character itself, and on SQL Server the <c>[</c> that opens a
        /// character set. A <c>%</c> the user types stays a wildcard, so "50%" still matches anything after 50.
        /// </summary>
        /// <remarks>
        /// Only SQL Server escapes <c>[</c>: Oracle refuses an escape character followed by anything but a
        /// wildcard or itself (ORA-01424), and the other databases read <c>[</c> as itself anyway.
        /// <see cref="ComparisonOperator.Like"/> is left alone: its caller writes the whole pattern.
        /// </remarks>
        internal static string EscapePattern(object? value, DatabaseType databaseType)
        {
            var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            var pattern = new StringBuilder(text.Length + 4);
            foreach (char ch in text)
            {
                if (ch is LikeEscape or '_' || (ch == '[' && databaseType == DatabaseType.SQLServer))
                    pattern.Append(LikeEscape);
                pattern.Append(ch);
            }
            return pattern.ToString();
        }

        private static string BuildNullCondition(FilterCondition c)
        {
            if (c.IgnoreIfNull) return string.Empty;
            if (c.Operator == ComparisonOperator.Equal) return c.FieldName + " IS NULL";
            if (c.Operator == ComparisonOperator.NotEqual) return c.FieldName + " IS NOT NULL";
            throw new InvalidOperationException("Value is null but operator is not supported with null.");
        }

        private static string BuildInCondition(FilterCondition c, IParameterCollector parameters)
        {
            var enumerable = c.Value as System.Collections.Generic.IEnumerable<object>
                ?? throw new InvalidOperationException("IN operator requires an enumerable value.");
            var list = new List<string>();
            foreach (var item in enumerable)
                list.Add(parameters.Add(item));
            if (list.Count == 0) return "1 = 0";
            return c.FieldName + " IN (" + string.Join(", ", list.ToArray()) + ")";
        }

        private static string BuildBetweenCondition(FilterCondition c, IParameterCollector parameters)
        {
            if (c.SecondValue == null)
            {
                if (c.IgnoreIfNull) return string.Empty;
                throw new InvalidOperationException("Between operator requires two values.");
            }
            var p1 = parameters.Add(c.Value!);
            var p2 = parameters.Add(c.SecondValue);
            return c.FieldName + " BETWEEN " + p1 + " AND " + p2;
        }
    }
}
