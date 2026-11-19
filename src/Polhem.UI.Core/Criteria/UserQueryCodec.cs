using System.Globalization;
using System.Xml;
using Polhem.Core.Serialization;
using Polhem.UI.Core.Criteria.Storage;

namespace Polhem.UI.Core.Criteria
{
    /// <summary>
    /// Writes a <see cref="QueryCriteria"/> as the XML a user query is stored in, and reads it back against the fields
    /// the current definition offers.
    /// </summary>
    /// <remarks>
    /// Reading is tolerant: a stored row whose field is no longer offered, whose operator the field no longer allows,
    /// or whose value no longer reads as the field's type is left out and reported in
    /// <see cref="UserQueryLoadResult.Skipped"/>, rather than failing the whole query when a definition changes. Only a
    /// text that is not a user query, or one written in an unknown format version, is refused.
    /// </remarks>
    public static class UserQueryCodec
    {
        /// <summary>The format version <see cref="Serialize"/> writes and the only one <see cref="Deserialize"/> reads.</summary>
        public const int CurrentVersion = 1;

        private const string DateFormat = "yyyy-MM-dd";
        private const string CurrentUserName = "me";

        /// <summary>
        /// Writes the rows of <paramref name="criteria"/>, including the ones with nothing entered, in their order.
        /// </summary>
        /// <param name="criteria">The criteria.</param>
        /// <returns>The XML text.</returns>
        public static string Serialize(QueryCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(criteria);
            var document = new UserQueryDocument { Version = CurrentVersion };
            foreach (var row in criteria.Rows)
                document.Conditions.Add(ToCondition(row));
            return XmlCodec.Serialize(document);
        }

        /// <summary>
        /// Reads a stored user query into new criteria whose rows are all <see cref="CriteriaRowKind.Optional"/>.
        /// </summary>
        /// <param name="xml">The stored XML text.</param>
        /// <param name="fields">The fields the user can query now, matched by field name ignoring case.</param>
        /// <returns>The criteria, and the stored rows that were left out.</returns>
        public static UserQueryLoadResult Deserialize(string xml, IReadOnlyList<CriteriaField> fields)
        {
            ArgumentNullException.ThrowIfNull(fields);
            var criteria = new QueryCriteria();
            var document = Read(xml);
            if (document is null || document.Version != CurrentVersion)
                return new UserQueryLoadResult(criteria, [], isSupported: false);

            var byName = new Dictionary<string, CriteriaField>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in fields)
                byName.TryAdd(field.FieldName, field);

            var skipped = new List<UserQuerySkip>();
            foreach (var condition in document.Conditions)
            {
                var reason = TryAdd(criteria, condition, byName);
                if (reason is { } why)
                    skipped.Add(new UserQuerySkip(condition.Field, why));
            }
            return new UserQueryLoadResult(criteria, skipped, isSupported: true);
        }

        private static UserQueryDocument? Read(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return null;
            try
            {
                return XmlCodec.Deserialize<UserQueryDocument>(xml);
            }
            catch (InvalidOperationException)
            {
                // `XmlSerializer` reports malformed XML and a different root element this way.
                return null;
            }
            catch (XmlException)
            {
                // A DTD, which the hardened reader prohibits, fails before the serializer starts.
                return null;
            }
        }

        private static UserQueryCondition ToCondition(CriteriaRow row)
        {
            var condition = new UserQueryCondition { Field = row.Field.FieldName, Operator = row.Operator.ToString() };
            switch (row.Operator)
            {
                case CriteriaOperator.Range when row.RelativeDate is { } period:
                    condition.Period = period.Kind.ToString();
                    condition.Days = period.Days;
                    break;
                case CriteriaOperator.Range:
                    condition.From = Encode(row.From);
                    condition.To = Encode(row.To);
                    break;
                case CriteriaOperator.In:
                    foreach (var value in row.Values)
                        condition.Values.Add(value.IsCurrentUser
                            ? new UserQueryValue { Relative = CurrentUserName }
                            : new UserQueryValue { Value = Encode(value.Value) ?? string.Empty, Text = value.Text });
                    break;
                default:
                    condition.Value = Encode(row.Value);
                    break;
            }
            return condition;
        }

        // Returns why the row was left out, or null when it was added.
        private static UserQuerySkipReason? TryAdd(QueryCriteria criteria, UserQueryCondition condition,
            Dictionary<string, CriteriaField> fields)
        {
            if (!fields.TryGetValue(condition.Field, out var field))
                return UserQuerySkipReason.UnknownField;
            if (!Enum.TryParse<CriteriaOperator>(condition.Operator, out var @operator)
                || !Enum.IsDefined(@operator) || !field.Operators.Contains(@operator))
                return UserQuerySkipReason.OperatorNotOffered;

            var row = criteria.Add(field, @operator);
            if (TryFill(row, condition)) return null;
            criteria.Remove(row);
            return UserQuerySkipReason.InvalidValue;
        }

        private static bool TryFill(CriteriaRow row, UserQueryCondition condition) => row.Operator switch
        {
            CriteriaOperator.Range when condition.Period is not null => TryFillPeriod(row, condition),
            CriteriaOperator.Range => TryFillRange(row, condition),
            CriteriaOperator.In => condition.Values.All(value => TryAddValue(row, value)),
            _ => TryFillValue(row, condition),
        };

        private static bool TryAddValue(CriteriaRow row, UserQueryValue value)
        {
            if (value.Relative is null) return row.TryAddValue(value.Value, ValueText(row.Field, value));
            if (!string.Equals(value.Relative, CurrentUserName, StringComparison.Ordinal) || !row.Field.OffersCurrentUser)
                return false;
            row.AddCurrentUser();
            return true;
        }

        // A listed value shows the item's text in the current definition and language, so a query shared by a user of
        // another language reads in the reader's; the stored text is kept for a picked record, or a value no longer listed.
        private static string ValueText(CriteriaField field, UserQueryValue value)
            => field.Kind == CriteriaFieldKind.Choice
                && field.ListItems.FirstOrDefault(item => string.Equals(item.Value, value.Value, StringComparison.Ordinal)) is { } item
                ? item.Text
                : value.Text;

        private static bool TryFillPeriod(CriteriaRow row, UserQueryCondition condition)
        {
            if (row.Field.Kind is not (CriteriaFieldKind.Date or CriteriaFieldKind.DateTime)) return false;
            if (!Enum.TryParse<RelativeDateKind>(condition.Period, out var kind) || !Enum.IsDefined(kind)) return false;
            if (kind == RelativeDateKind.LastDays ? condition.Days < 1 : condition.Days != 0) return false;
            row.SetRelativeDate(new RelativeDate(kind, condition.Days));
            return true;
        }

        private static bool TryFillRange(CriteriaRow row, UserQueryCondition condition)
        {
            if (!TryDecode(row.Field, condition.From, out var from) || !TryDecode(row.Field, condition.To, out var to))
                return false;
            row.SetRange(from, to);
            return true;
        }

        private static bool TryFillValue(CriteriaRow row, UserQueryCondition condition)
        {
            if (!TryDecode(row.Field, condition.Value, out var value)) return false;
            row.SetValue(value);
            return true;
        }

        private static string? Encode(object? value) => value switch
        {
            null => null,
            string text => text,
            bool flag => flag ? "true" : "false",
            DateOnly day => day.ToString(DateFormat, CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture),
        };

        // Reads a single value as the type the field takes; an absent value reads as null.
        private static bool TryDecode(CriteriaField field, string? text, out object? value)
        {
            value = null;
            if (text is null) return true;
            switch (field.Kind)
            {
                case CriteriaFieldKind.Text or CriteriaFieldKind.Code:
                    value = text;
                    return true;
                case CriteriaFieldKind.Number:
                    if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)) return false;
                    value = number;
                    return true;
                case CriteriaFieldKind.Date or CriteriaFieldKind.DateTime:
                    if (!DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) return false;
                    value = day;
                    return true;
                case CriteriaFieldKind.Boolean:
                    if (!bool.TryParse(text, out var flag)) return false;
                    value = flag;
                    return true;
                default:
                    return false;
            }
        }
    }
}
