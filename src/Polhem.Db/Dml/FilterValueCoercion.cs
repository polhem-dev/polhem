using System.Globalization;
using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Core.Exceptions;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;

namespace Polhem.Db.Dml
{
    /// <summary>
    /// Converts the string values of a filter to the type of the field they are compared with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <c>Plain</c> request body names no CLR type, so a date, a <see cref="Guid"/> or a number written as a string
    /// (the JSON wires quote <see cref="decimal"/> and <see cref="long"/> on purpose) arrives as a
    /// <see cref="string"/>. Bound as text, PostgreSQL refuses to compare it with a typed column and Oracle fails to
    /// convert it, and SQL Server converted it to the column's own precision, so `'10.5'` compared with a whole-number
    /// decimal column as 11 and the filter returned no rows. The field's
    /// <see cref="FormField.DbType"/> says what the value means, so the conversion happens here, once, for every
    /// payload format. <c>GetListFilterValueDbTests</c> runs it against each database; a relation field converts by
    /// the type it declares, which nothing checks against its source column.
    /// </para>
    /// <para>
    /// Only <see cref="string"/> values are converted, and only for comparisons. The <c>LIKE</c> operators match text,
    /// so their values are left alone, and so is the company column the framework confines a company form on. Dates
    /// are read in their ISO 8601 forms only. A <see cref="FieldDbType.DateTime"/> value is a UTC instant, as on the typed
    /// wires: text with an offset is moved to UTC by it, and text without one is taken to be UTC. A
    /// <see cref="FieldDbType.Date"/> value is the date written, with no time-zone conversion. A
    /// <see cref="FieldDbType.Time"/> value is written in its fixed-width <c>HH:mm</c> form, as the save stores it.
    /// </para>
    /// </remarks>
    internal static class FilterValueCoercion
    {
        // The ISO 8601 forms a client writes. A culture-free `TryParse` also takes "01/02/2026" as January 2 and
        // "12:00" as today, which would filter on a different day than the one the user meant.
        private static readonly string[] s_dateFormats =
        [
            "yyyy-MM-dd",
            "yyyy-MM-dd'T'HH:mmK",
            "yyyy-MM-dd'T'HH:mm:ssK",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
        ];

        // A sign and a decimal point only: `NumberStyles.Number` would also read "1,0,0" as 100 and "10-" as -10.
        private const NumberStyles DecimalStyles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

        /// <summary>
        /// Returns a copy of the filter whose string values are converted to the types of their fields. The input is
        /// not changed.
        /// </summary>
        /// <param name="node">The filter, with the field names the table declares.</param>
        /// <param name="formTable">The table whose fields the filter names.</param>
        /// <exception cref="InvalidRequestValueException">A string value cannot be read as its field's type.</exception>
        public static FilterNode Coerce(FilterNode node, FormTable formTable)
        {
            if (node is FilterGroup group)
            {
                var copy = new FilterGroup(group.Operator);
                foreach (var child in group.Nodes)
                    copy.Nodes.Add(Coerce(child, formTable));
                return copy;
            }

            if (node is not FilterCondition condition) { return node; }
            if (SelectFieldGuard.IsCompanyColumn(formTable, condition.FieldName)) { return condition; }

            var field = formTable.Fields?.GetOrDefault(condition.FieldName);
            if (field == null || !Converts(condition.Operator)) { return condition; }

            return new FilterCondition
            {
                FieldName = condition.FieldName,
                Operator = condition.Operator,
                Value = CoerceValue(condition.Value, field),
                SecondValue = CoerceValue(condition.SecondValue, field),
                IgnoreIfNull = condition.IgnoreIfNull,
            };
        }

        private static bool Converts(ComparisonOperator op) => op is not (ComparisonOperator.Like
            or ComparisonOperator.Contains or ComparisonOperator.StartsWith or ComparisonOperator.EndsWith);

        private static object? CoerceValue(object? value, FormField field) => value switch
        {
            string text => Parse(text, field),
            IEnumerable<object?> items => items.Select(item => item is string text ? Parse(text, field) : item).ToArray(),
            _ => value,
        };

        private static object Parse(string text, FormField field)
        {
            var culture = CultureInfo.InvariantCulture;
            object? parsed = field.DbType switch
            {
                FieldDbType.DateTime => DateTimeOffset.TryParseExact(text, s_dateFormats, culture, DateTimeStyles.AssumeUniversal, out var instant)
                    ? instant.UtcDateTime : null,
                FieldDbType.Date => DateTimeOffset.TryParseExact(text, s_dateFormats, culture, DateTimeStyles.AssumeUniversal, out var date)
                    ? date.DateTime.Date : null,
                FieldDbType.Guid => Guid.TryParse(text, out var guid) ? guid : null,
                FieldDbType.Integer or FieldDbType.AutoIncrement
                    => int.TryParse(text, NumberStyles.Integer, culture, out var i) ? i : null,
                FieldDbType.Short => short.TryParse(text, NumberStyles.Integer, culture, out var s) ? s : null,
                FieldDbType.Long => long.TryParse(text, NumberStyles.Integer, culture, out var l) ? l : null,
                FieldDbType.Decimal or FieldDbType.Currency
                    => decimal.TryParse(text, DecimalStyles, culture, out var d) ? d : null,
                FieldDbType.Boolean => bool.TryParse(text, out var b) ? b : null,
                // The stored form is the fixed-width "HH:mm" the save writes, so "8:30" must match "08:30". An empty
                // string is how a time of day that was not filled in is stored, and matches those rows.
                FieldDbType.Time => text.Length == 0 ? text : NullIfEmpty(ValueUtilities.CTimeString(text)),
                _ => text,
            };

            return parsed ?? throw new InvalidRequestValueException(
                $"The filter value for the field '{field.FieldName}' is not a valid {field.DbType}.");
        }

        private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;
    }
}
