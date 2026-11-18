using System.Globalization;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.Lists;

namespace Polhem.UI.Core.Criteria
{
    /// <summary>
    /// The filters a list's column headers set, kept as rows of the list's <see cref="QueryCriteria"/>, so the query pane
    /// and its chips show them and clearing the conditions clears them too.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each column filters through one row of its own: setting the column again replaces that row, and an empty entry
    /// removes it. Rows the user added in the pane are left alone, so a column filter and a pane condition on the same
    /// field both apply. A column counts as filtered while any row on its field has a value.
    /// </para>
    /// <para>
    /// What a user enters follows the rules of the header's inputs: text the value contains, a range of numbers in the
    /// current culture, a range of days as <c>yyyy-MM-dd</c> (a date-time range covers its end day whole), yes or no as
    /// <see cref="bool.TrueString"/> or <see cref="bool.FalseString"/>, and one listed value by its stored value.
    /// </para>
    /// </remarks>
    public sealed class ColumnFilterCriteria
    {
        private const string DayFormat = "yyyy-MM-dd";
        private readonly Dictionary<string, CriteriaRow> _rows = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Initializes a new instance of <see cref="ColumnFilterCriteria"/> over a list's criteria.</summary>
        /// <param name="criteria">The criteria the column filters add their rows to.</param>
        public ColumnFilterCriteria(QueryCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(criteria);
            Criteria = criteria;
        }

        /// <summary>Gets the criteria the column filters add their rows to.</summary>
        public QueryCriteria Criteria { get; }

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

        /// <summary>Determines whether what a user entered can be read as a column's filter.</summary>
        /// <param name="field">The schema field.</param>
        /// <param name="kind">The kind of filter, from <see cref="GetKind"/>.</param>
        /// <param name="first">The value, or the start of a range.</param>
        /// <param name="second">The end of a range, or empty.</param>
        /// <returns>Whether the entry can be read; an empty entry can.</returns>
        public static bool CanRead(FormField field, ColumnFilterKind kind, string? first, string? second)
        {
            ArgumentNullException.ThrowIfNull(field);
            return IsEmpty(first, second) || TryAddRow(new QueryCriteria(), field, kind, first?.Trim() ?? string.Empty, second?.Trim() ?? string.Empty) is not null;
        }

        /// <summary>Determines whether a column is filtered: a row on its field has a value.</summary>
        /// <param name="fieldName">The field.</param>
        /// <returns>Whether it is filtered.</returns>
        public bool IsFiltered(string fieldName)
            => Criteria.Rows.Any(r => r.HasValue && string.Equals(r.Field.FieldName, fieldName, StringComparison.OrdinalIgnoreCase));

        /// <summary>Returns what the column's own row holds, written the way its header takes it.</summary>
        /// <param name="fieldName">The field.</param>
        /// <returns>The value, or the start of a range, and the end of a range; empty when the column set no row.</returns>
        public (string First, string Second) GetEntered(string fieldName)
        {
            if (OwnRow(fieldName) is not { HasValue: true } row) return (string.Empty, string.Empty);
            return row.Operator switch
            {
                CriteriaOperator.Range when row.RelativeDate is null => (Format(row.From), Format(row.To)),
                CriteriaOperator.Range => (string.Empty, string.Empty),
                CriteriaOperator.In => (row.Values.Count > 0 ? Format(row.Values[0].Value) : string.Empty, string.Empty),
                _ => (Format(row.Value), string.Empty),
            };
        }

        /// <summary>
        /// Sets a column's filter from what the user entered, replacing the row the column set before. Empty values
        /// remove that row.
        /// </summary>
        /// <param name="field">The schema field.</param>
        /// <param name="kind">The kind of filter, from <see cref="GetKind"/>.</param>
        /// <param name="first">The value, or the start of a range.</param>
        /// <param name="second">The end of a range, or empty.</param>
        /// <returns><see langword="false"/>, leaving the criteria as they were, when a value cannot be read.</returns>
        public bool TrySet(FormField field, ColumnFilterKind kind, string? first, string? second)
        {
            ArgumentNullException.ThrowIfNull(field);
            if (!CanRead(field, kind, first, second)) return false;
            if (OwnRow(field.FieldName) is { } previous) Criteria.Remove(previous);
            _rows.Remove(field.FieldName);
            if (IsEmpty(first, second)) return true;
            _rows[field.FieldName] = TryAddRow(Criteria, field, kind, first?.Trim() ?? string.Empty, second?.Trim() ?? string.Empty)!;
            return true;
        }

        // The row the column set, while it is still in the criteria; the pane or a chip may have removed it.
        private CriteriaRow? OwnRow(string fieldName)
            => _rows.TryGetValue(fieldName, out var row) && Criteria.Rows.Contains(row) ? row : null;

        private static bool IsEmpty(string? first, string? second)
            => string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(second);

        // Adds the row for an entry, or returns null, leaving the criteria as they were, when a value cannot be read.
        private static CriteriaRow? TryAddRow(QueryCriteria criteria, FormField field, ColumnFilterKind kind, string first, string second)
        {
            var (fieldKind, @operator) = kind switch
            {
                ColumnFilterKind.Text => (CriteriaFieldKind.Text, CriteriaOperator.Contains),
                ColumnFilterKind.Number => (CriteriaFieldKind.Number, CriteriaOperator.Range),
                ColumnFilterKind.Date => (CriteriaFieldKind.Date, CriteriaOperator.Range),
                ColumnFilterKind.DateTime => (CriteriaFieldKind.DateTime, CriteriaOperator.Range),
                ColumnFilterKind.Boolean => (CriteriaFieldKind.Boolean, CriteriaOperator.Equal),
                ColumnFilterKind.Choice => (CriteriaFieldKind.Choice, CriteriaOperator.In),
                _ => ((CriteriaFieldKind?)null, CriteriaOperator.Equal),
            };
            if (fieldKind is null) return null;
            var criteriaField = new CriteriaField(field.FieldName, field.Caption, fieldKind.Value, field.DbType)
            {
                ListItems = fieldKind == CriteriaFieldKind.Choice && field.ListItems is { } items ? [.. items] : [],
            };
            var row = criteria.Add(criteriaField, @operator);
            if (Fill(row, kind, field, first, second)) return row;
            criteria.Remove(row);
            return null;
        }

        private static bool Fill(CriteriaRow row, ColumnFilterKind kind, FormField field, string first, string second)
        {
            switch (kind)
            {
                case ColumnFilterKind.Text:
                    row.SetValue(first);
                    return row.HasValue;
                case ColumnFilterKind.Boolean:
                    if (!bool.TryParse(first, out var flag)) return false;
                    row.SetValue(flag);
                    return true;
                case ColumnFilterKind.Choice:
                    var text = field.ListItems?.FirstOrDefault(i => string.Equals(i.Value, first, StringComparison.Ordinal))?.Text ?? first;
                    return row.TryAddValue(first, text);
                case ColumnFilterKind.Number:
                    if (!TryParseEnd(first, TryParseNumber, out var fromNumber) || !TryParseEnd(second, TryParseNumber, out var toNumber))
                        return false;
                    row.SetRange(fromNumber, toNumber);
                    return true;
                default:
                    if (!TryParseEnd(first, TryParseDay, out var fromDay) || !TryParseEnd(second, TryParseDay, out var toDay))
                        return false;
                    row.SetRange(fromDay, toDay);
                    return true;
            }
        }

        private delegate bool Parser(string text, out object value);

        // An empty end is open; anything else must read.
        private static bool TryParseEnd(string text, Parser parse, out object? value)
        {
            value = null;
            if (text.Length == 0) return true;
            if (!parse(text, out var parsed)) return false;
            value = parsed;
            return true;
        }

        private static bool TryParseNumber(string text, out object value)
        {
            bool ok = decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var number);
            value = number;
            return ok;
        }

        private static bool TryParseDay(string text, out object value)
        {
            bool ok = DateOnly.TryParseExact(text, DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day);
            value = day;
            return ok;
        }

        private static string Format(object? value) => value switch
        {
            null => string.Empty,
            DateOnly day => day.ToString(DayFormat, CultureInfo.InvariantCulture),
            bool flag => flag ? bool.TrueString : bool.FalseString,
            decimal number => number.ToString(CultureInfo.CurrentCulture),
            string text => text,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };
    }
}
