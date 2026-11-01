using System.Globalization;
using Polhem.Core.Data;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;

namespace Polhem.UI.Core.Connectors
{
    /// <summary>
    /// The column filters of a form list on screen, shared by the UI heads so each one turns what the user entered into
    /// the same <see cref="FilterNode"/>.
    /// </summary>
    /// <remarks>
    /// The entered values stay as the user typed them, so a head can show them again. Text filters use
    /// <see cref="ComparisonOperator.Contains"/>: <c>%</c> and <c>_</c> in the text act as wildcards, because the server
    /// does not escape them, and whether matching ignores case depends on the database.
    /// </remarks>
    public sealed class ListFilterState
    {
        private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets a value indicating whether any column is filtered.</summary>
        public bool HasFilters => _entries.Count > 0;

        /// <summary>
        /// Returns what a user can enter for a column. A <see cref="FieldDbType.Text"/> column offers none, as it offers
        /// no sort, and neither does a value with no meaningful comparison.
        /// </summary>
        /// <param name="column">The column.</param>
        /// <param name="field">The schema field behind it, or <c>null</c> when the schema does not know it.</param>
        /// <returns>The kind of filter.</returns>
        public static ColumnFilterKind GetKind(LayoutColumn column, FormField? field)
        {
            ArgumentNullException.ThrowIfNull(column);
            if (field is null) return ColumnFilterKind.None;
            if (column.ControlType == ControlType.DropDownEdit && field.ListItems is { Count: > 0 })
                return ColumnFilterKind.Choice;
            return field.DbType switch
            {
                FieldDbType.String => ColumnFilterKind.Text,
                FieldDbType.Boolean => ColumnFilterKind.Boolean,
                FieldDbType.AutoIncrement or FieldDbType.Short or FieldDbType.Integer or FieldDbType.Long
                    or FieldDbType.Decimal or FieldDbType.Currency => ColumnFilterKind.Number,
                FieldDbType.Date => ColumnFilterKind.Date,
                FieldDbType.DateTime => ColumnFilterKind.DateTime,
                _ => ColumnFilterKind.None,
            };
        }

        /// <summary>Determines whether a column is filtered.</summary>
        /// <param name="fieldName">The field.</param>
        /// <returns>Whether it is filtered.</returns>
        public bool IsFiltered(string fieldName) => _entries.ContainsKey(fieldName);

        /// <summary>Returns what the user entered for a column.</summary>
        /// <param name="fieldName">The field.</param>
        /// <returns>The first value, or the start of a range, and the end of a range; empty when not filtered.</returns>
        public (string First, string Second) GetEntered(string fieldName)
            => _entries.TryGetValue(fieldName, out var entry) ? (entry.First, entry.Second) : (string.Empty, string.Empty);

        /// <summary>
        /// Sets a column's filter from what the user entered. Empty values remove it.
        /// </summary>
        /// <param name="field">The schema field.</param>
        /// <param name="kind">The kind of filter, from <see cref="GetKind"/>.</param>
        /// <param name="first">The value, or the start of a range. A date is <c>yyyy-MM-dd</c>; a number is in the current culture.</param>
        /// <param name="second">The end of a range, or empty.</param>
        /// <returns><see langword="false"/>, leaving the filter as it was, when a value cannot be read.</returns>
        public bool TrySet(FormField field, ColumnFilterKind kind, string? first, string? second)
        {
            ArgumentNullException.ThrowIfNull(field);
            first = first?.Trim() ?? string.Empty;
            second = second?.Trim() ?? string.Empty;
            if (first.Length == 0 && second.Length == 0)
            {
                _entries.Remove(field.FieldName);
                return true;
            }
            if (!TryBuild(field, kind, first, second, out var nodes)) return false;
            _entries[field.FieldName] = new Entry(first, second, nodes);
            return true;
        }

        /// <summary>Removes every column's filter.</summary>
        public void Clear() => _entries.Clear();

        /// <summary>Creates the filter of a list request.</summary>
        /// <returns>The filter, or <c>null</c> when no column is filtered.</returns>
        public FilterNode? CreateFilter()
        {
            var nodes = _entries.Values.SelectMany(e => e.Nodes).ToArray();
            return nodes.Length switch
            {
                0 => null,
                1 => nodes[0],
                _ => FilterGroup.All(nodes),
            };
        }

        private static bool TryBuild(FormField field, ColumnFilterKind kind, string first, string second, out FilterNode[] nodes)
        {
            string name = field.FieldName;
            nodes = [];
            switch (kind)
            {
                case ColumnFilterKind.Text:
                    nodes = [FilterCondition.Contains(name, first)];
                    return true;
                case ColumnFilterKind.Boolean:
                    if (!bool.TryParse(first, out var flag)) return false;
                    nodes = [FilterCondition.Equal(name, flag)];
                    return true;
                case ColumnFilterKind.Choice:
                    if (!TryConvertChoice(field, first, out var choice)) return false;
                    nodes = [FilterCondition.Equal(name, choice)];
                    return true;
                case ColumnFilterKind.Number:
                    return TryBuildRange<decimal>(name, first, second, TryParseNumber, ComparisonOperator.LessThanOrEqual, static v => v, out nodes);
                case ColumnFilterKind.Date:
                    return TryBuildRange<DateOnly>(name, first, second, TryParseDay, ComparisonOperator.LessThanOrEqual, static v => v, out nodes);
                case ColumnFilterKind.DateTime:
                    // The end day is whole, so the range stops before the day after it.
                    return TryBuildRange<DateTime>(name, first, second, TryParseInstant, ComparisonOperator.LessThan, static v => v.AddDays(1), out nodes);
                default:
                    return false;
            }
        }

        private delegate bool Parser<T>(string text, out T value);

        private static bool TryBuildRange<T>(string name, string first, string second, Parser<T> parse, ComparisonOperator endOperator,
            Func<T, T> endValue, out FilterNode[] nodes) where T : notnull
        {
            nodes = [];
            var built = new List<FilterNode>(2);
            if (first.Length > 0)
            {
                if (!parse(first, out var from)) return false;
                built.Add(new FilterCondition(name, ComparisonOperator.GreaterThanOrEqual, from));
            }
            if (second.Length > 0)
            {
                if (!parse(second, out var to)) return false;
                built.Add(new FilterCondition(name, endOperator, endValue(to)));
            }
            nodes = [.. built];
            return true;
        }

        private static bool TryParseNumber(string text, out decimal value)
            => decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value);

        private static bool TryParseDay(string text, out DateOnly value)
            => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

        private static bool TryParseInstant(string text, out DateTime value)
        {
            if (TryParseDay(text, out var day))
            {
                // Unspecified: the time zone conversion of the request reads it in the user's zone.
                value = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
                return true;
            }
            value = default;
            return false;
        }

        private static bool TryConvertChoice(FormField field, string text, out object value)
        {
            value = text;
            switch (field.DbType)
            {
                case FieldDbType.Short or FieldDbType.Integer or FieldDbType.Long or FieldDbType.AutoIncrement:
                    if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole)) return false;
                    value = whole;
                    return true;
                case FieldDbType.Decimal or FieldDbType.Currency:
                    if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)) return false;
                    value = number;
                    return true;
                default:
                    return true;
            }
        }

        private sealed record Entry(string First, string Second, FilterNode[] Nodes);
    }
}
