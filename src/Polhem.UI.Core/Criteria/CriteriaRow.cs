using System.Globalization;
using Polhem.Core.Data;

namespace Polhem.UI.Core.Criteria
{
    /// <summary>
    /// One condition of a <see cref="QueryCriteria"/>: a field, an operator and what the user entered, already
    /// converted to the field's type.
    /// </summary>
    /// <remarks>
    /// Values are converted here, once, as they are set, so a converter never parses text and two backends cannot read
    /// the same entry differently. Which setter applies depends on <see cref="Operator"/>: <see cref="SetValue"/> for
    /// <see cref="CriteriaOperator.Contains"/>, <see cref="CriteriaOperator.StartsWith"/> and
    /// <see cref="CriteriaOperator.Equal"/>; <see cref="SetRange"/> and <see cref="SetRelativeDate"/> for
    /// <see cref="CriteriaOperator.Range"/>; <see cref="TryAddValue"/> for <see cref="CriteriaOperator.In"/>.
    /// </remarks>
    public sealed class CriteriaRow
    {
        private readonly List<CriteriaValue> _values = [];

        internal CriteriaRow(CriteriaField field, CriteriaOperator @operator, CriteriaRowKind kind, bool isOperatorLocked)
        {
            if (!field.Operators.Contains(@operator))
                throw new ArgumentException($"The field {field.FieldName} does not offer {@operator}.", nameof(@operator));
            Field = field;
            Operator = @operator;
            Kind = kind;
            IsOperatorLocked = isOperatorLocked;
        }

        /// <summary>Gets the field the row compares.</summary>
        public CriteriaField Field { get; }

        /// <summary>Gets why the row is in the criteria.</summary>
        public CriteriaRowKind Kind { get; }

        /// <summary>Gets a value indicating whether the operator is fixed, as for a report parameter.</summary>
        public bool IsOperatorLocked { get; }

        /// <summary>Gets the operator.</summary>
        public CriteriaOperator Operator { get; private set; }

        /// <summary>Gets the value of a single-value operator: a trimmed string, a <see cref="decimal"/>, a
        /// <see cref="DateOnly"/> or a <see cref="bool"/>; <c>null</c> when nothing is entered.</summary>
        public object? Value { get; private set; }

        /// <summary>Gets the start of a range, of the same types as <see cref="Value"/>; <c>null</c> when open.</summary>
        public object? From { get; private set; }

        /// <summary>Gets the end of a range, of the same types as <see cref="Value"/>; <c>null</c> when open.</summary>
        public object? To { get; private set; }

        /// <summary>Gets the period of a date range named relative to today, which takes the place of
        /// <see cref="From"/> and <see cref="To"/>; <c>null</c> when the range is entered as days.</summary>
        public RelativeDate? RelativeDate { get; private set; }

        /// <summary>Gets the values of an <see cref="CriteriaOperator.In"/> row, in the order they were added.</summary>
        public IReadOnlyList<CriteriaValue> Values => _values;

        /// <summary>Gets a value indicating whether the row has something entered and therefore filters.</summary>
        public bool HasValue => Operator switch
        {
            CriteriaOperator.Range => RelativeDate is not null || From is not null || To is not null,
            CriteriaOperator.In => _values.Count > 0,
            _ => Value is not null,
        };

        /// <summary>
        /// Changes the operator. A value entered for one of the single-value operators stays when the new one is
        /// also a single-value operator; anything else entered is cleared.
        /// </summary>
        /// <param name="operator">The new operator.</param>
        /// <exception cref="InvalidOperationException">The operator is locked.</exception>
        /// <exception cref="ArgumentException">The field does not offer the operator.</exception>
        public void ChangeOperator(CriteriaOperator @operator)
        {
            if (@operator == Operator) return;
            if (IsOperatorLocked)
                throw new InvalidOperationException($"The operator of the {Field.FieldName} row is locked.");
            if (!Field.Operators.Contains(@operator))
                throw new ArgumentException($"The field {Field.FieldName} does not offer {@operator}.", nameof(@operator));
            var kept = IsSingleValue(Operator) && IsSingleValue(@operator) ? Value : null;
            ClearValue();
            Operator = @operator;
            Value = kept;
        }

        /// <summary>
        /// Sets the value of a <see cref="CriteriaOperator.Contains"/>, <see cref="CriteriaOperator.StartsWith"/>
        /// or <see cref="CriteriaOperator.Equal"/> row; <c>null</c> or blank text clears it.
        /// </summary>
        /// <param name="value">A string for a text or code field; a number for a number field; a
        /// <see cref="DateOnly"/> or a <see cref="DateTime"/> (of which the day is used) for a date or date-time
        /// field; a <see cref="bool"/> for a yes / no field.</param>
        /// <exception cref="InvalidOperationException">The operator is not a single-value operator.</exception>
        /// <exception cref="ArgumentException">The value is not of a type the field takes.</exception>
        public void SetValue(object? value)
        {
            if (!IsSingleValue(Operator))
                throw new InvalidOperationException($"A {Operator} row takes no single value.");
            Value = Normalize(value, nameof(value));
        }

        /// <summary>
        /// Sets both ends of a <see cref="CriteriaOperator.Range"/> row, of the types <see cref="SetValue"/> takes;
        /// <c>null</c> leaves an end open. It replaces a relative date.
        /// </summary>
        /// <param name="from">The first value included, or <c>null</c>.</param>
        /// <param name="to">The last value included, or <c>null</c>.</param>
        /// <exception cref="InvalidOperationException">The operator is not <see cref="CriteriaOperator.Range"/>.</exception>
        /// <exception cref="ArgumentException">A value is not of a type the field takes.</exception>
        public void SetRange(object? from, object? to)
        {
            RequireOperator(CriteriaOperator.Range);
            var start = Normalize(from, nameof(from));
            var end = Normalize(to, nameof(to));
            From = start;
            To = end;
            RelativeDate = null;
        }

        /// <summary>
        /// Sets the range of a date or date-time row to a period named relative to today, replacing the days
        /// entered; <c>null</c> clears it.
        /// </summary>
        /// <param name="relativeDate">The period, or <c>null</c>.</param>
        /// <exception cref="InvalidOperationException">The field is not a date or date-time field, or the
        /// operator is not <see cref="CriteriaOperator.Range"/>.</exception>
        public void SetRelativeDate(RelativeDate? relativeDate)
        {
            if (Field.Kind is not (CriteriaFieldKind.Date or CriteriaFieldKind.DateTime))
                throw new InvalidOperationException($"The {Field.Kind} field {Field.FieldName} takes no relative date.");
            RequireOperator(CriteriaOperator.Range);
            RelativeDate = relativeDate;
            From = null;
            To = null;
        }

        /// <summary>
        /// Adds a value to an <see cref="CriteriaOperator.In"/> row. A value already in the row is not added again.
        /// </summary>
        /// <param name="value">For a choice field, the list item's value, converted to the field's stored type;
        /// for a relation field, the picked row's key, of the field's stored type.</param>
        /// <param name="text">The text shown for the value.</param>
        /// <returns><see langword="false"/>, leaving the row as it was, when the value cannot be converted to the
        /// field's stored type.</returns>
        /// <exception cref="InvalidOperationException">The operator is not <see cref="CriteriaOperator.In"/>.</exception>
        public bool TryAddValue(object value, string text)
        {
            ArgumentNullException.ThrowIfNull(value);
            RequireOperator(CriteriaOperator.In);
            if (!TryConvertToDbType(value, Field.DbType, out var converted)) return false;
            if (_values.Exists(v => v.Value.Equals(converted))) return true;
            _values.Add(new CriteriaValue(converted, string.IsNullOrEmpty(text) ? Convert.ToString(converted, CultureInfo.InvariantCulture) ?? string.Empty : text));
            return true;
        }

        /// <summary>Removes a value from an <see cref="CriteriaOperator.In"/> row.</summary>
        /// <param name="value">The value, as <see cref="Values"/> holds it.</param>
        /// <returns><see langword="true"/> when the value was in the row.</returns>
        public bool RemoveValue(object value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return _values.RemoveAll(v => v.Value.Equals(value)) > 0;
        }

        /// <summary>Clears everything entered, keeping the field and the operator.</summary>
        public void ClearValue()
        {
            Value = null;
            From = null;
            To = null;
            RelativeDate = null;
            _values.Clear();
        }

        private static bool IsSingleValue(CriteriaOperator @operator)
            => @operator is CriteriaOperator.Contains or CriteriaOperator.StartsWith or CriteriaOperator.Equal;

        private void RequireOperator(CriteriaOperator @operator)
        {
            if (Operator != @operator)
                throw new InvalidOperationException($"The {Field.FieldName} row is a {Operator} row, not a {@operator} row.");
        }

        private object? Normalize(object? value, string parameterName)
        {
            if (value is null) return null;
            switch (Field.Kind)
            {
                case CriteriaFieldKind.Text or CriteriaFieldKind.Code:
                    if (value is not string text) break;
                    text = text.Trim();
                    return text.Length == 0 ? null : text;
                case CriteriaFieldKind.Number:
                    return value switch
                    {
                        decimal d => d,
                        byte or sbyte or short or ushort or int or uint or long or ulong or float or double
                            => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
                        _ => throw WrongType(value, parameterName),
                    };
                case CriteriaFieldKind.Date or CriteriaFieldKind.DateTime:
                    return value switch
                    {
                        DateOnly day => day,
                        DateTime instant => DateOnly.FromDateTime(instant),
                        _ => throw WrongType(value, parameterName),
                    };
                case CriteriaFieldKind.Boolean:
                    if (value is bool) return value;
                    break;
                default:
                    throw new InvalidOperationException($"A {Field.Kind} field takes its values through {nameof(TryAddValue)}.");
            }
            throw WrongType(value, parameterName);
        }

        private ArgumentException WrongType(object value, string parameterName)
            => new($"A {Field.Kind} field does not take a {value.GetType().Name}.", parameterName);

        private static bool TryConvertToDbType(object value, FieldDbType dbType, out object converted)
        {
            converted = value;
            switch (dbType)
            {
                case FieldDbType.Guid:
                    if (value is Guid) return true;
                    if (value is string g && Guid.TryParse(g, out var guid))
                    {
                        converted = guid;
                        return true;
                    }
                    return false;
                case FieldDbType.AutoIncrement or FieldDbType.Short or FieldDbType.Integer or FieldDbType.Long:
                    if (!long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole))
                        return false;
                    converted = whole;
                    return true;
                case FieldDbType.Decimal or FieldDbType.Currency:
                    if (!decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                        return false;
                    converted = number;
                    return true;
                case FieldDbType.String:
                    if (value is not string s) return false;
                    converted = s;
                    return true;
                default:
                    return false;
            }
        }
    }
}
