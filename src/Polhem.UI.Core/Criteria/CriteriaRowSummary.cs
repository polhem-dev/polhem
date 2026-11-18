using System.Globalization;
using Polhem.Definition.Language;

namespace Polhem.UI.Core.Criteria
{
    /// <summary>
    /// Writes a condition row as one short line, such as "Order date: This month" or "Shipper: Federal Shipping and
    /// 2 more", for the chips that show the conditions in effect above a list.
    /// </summary>
    /// <remarks>
    /// The words come from <see cref="PolhemUIText"/> keys resolved by the caller, so each head passes its own
    /// localizer; numbers and dates follow <see cref="CultureInfo.CurrentCulture"/>.
    /// </remarks>
    public static class CriteriaRowSummary
    {
        /// <summary>
        /// Describes a row, or returns an empty string when it has nothing entered.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="text">Resolves a <see cref="PolhemUIText"/> key to the text of the user's language.</param>
        /// <returns>The line.</returns>
        public static string Describe(CriteriaRow row, Func<string, string> text)
        {
            ArgumentNullException.ThrowIfNull(row);
            ArgumentNullException.ThrowIfNull(text);
            if (!row.HasValue) return string.Empty;

            string caption = row.Field.Caption;
            switch (row.Operator)
            {
                case CriteriaOperator.Contains:
                    return $"{caption} {text(PolhemUIText.CriteriaContains)} {Format(row.Value)}";
                case CriteriaOperator.StartsWith:
                    return $"{caption} {text(PolhemUIText.CriteriaStartsWith)} {Format(row.Value)}";
                case CriteriaOperator.Equal:
                    return Labeled(caption, row.Value is bool flag ? text(flag ? PolhemUIText.True : PolhemUIText.False) : Format(row.Value), text);
                case CriteriaOperator.Range:
                    return Labeled(caption, RangeText(row, text), text);
                case CriteriaOperator.In:
                    return Labeled(caption, ValuesText(row, text), text);
                default:
                    throw new InvalidOperationException($"Unknown criteria operator {row.Operator}.");
            }
        }

        /// <summary>
        /// Names a period relative to today, such as "This month" or "Last 7 days".
        /// </summary>
        /// <param name="period">The period.</param>
        /// <param name="text">Resolves a <see cref="PolhemUIText"/> key to the text of the user's language.</param>
        /// <returns>The name.</returns>
        public static string PeriodText(RelativeDate period, Func<string, string> text)
        {
            ArgumentNullException.ThrowIfNull(text);
            return period.Kind switch
            {
                RelativeDateKind.Today => text(PolhemUIText.DateToday),
                RelativeDateKind.Yesterday => text(PolhemUIText.DateYesterday),
                RelativeDateKind.ThisWeek => text(PolhemUIText.DateThisWeek),
                RelativeDateKind.LastWeek => text(PolhemUIText.DateLastWeek),
                RelativeDateKind.ThisMonth => text(PolhemUIText.DateThisMonth),
                RelativeDateKind.LastMonth => text(PolhemUIText.DateLastMonth),
                RelativeDateKind.ThisQuarter => text(PolhemUIText.DateThisQuarter),
                RelativeDateKind.ThisYear => text(PolhemUIText.DateThisYear),
                RelativeDateKind.LastDays => string.Format(CultureInfo.CurrentCulture, text(PolhemUIText.DateLastDays), period.Days),
                _ => throw new InvalidOperationException($"Unknown relative date kind {period.Kind}."),
            };
        }

        private static string Labeled(string caption, string value, Func<string, string> text)
            => string.Format(CultureInfo.CurrentCulture, text(PolhemUIText.CriteriaLabeledValue), caption, value);

        private static string RangeText(CriteriaRow row, Func<string, string> text)
        {
            if (row.RelativeDate is { } period) return PeriodText(period, text);
            if (row.From is not null && row.To is not null)
                return string.Format(CultureInfo.CurrentCulture, text(PolhemUIText.CriteriaRangeValue), Format(row.From), Format(row.To));
            return row.From is not null ? "≥ " + Format(row.From) : "≤ " + Format(row.To);
        }

        // Two values are listed; with more, the first one and how many there are in all.
        private static string ValuesText(CriteriaRow row, Func<string, string> text)
        {
            var values = row.Values;
            if (values.Count <= 2)
                return string.Join(text(PolhemUIText.CriteriaValueSeparator), values.Select(v => v.Text));
            return string.Format(CultureInfo.CurrentCulture, text(PolhemUIText.CriteriaMoreValues),
                values[0].Text, values.Count, values.Count - 1);
        }

        private static string Format(object? value) => value switch
        {
            DateOnly day => day.ToString("d", CultureInfo.CurrentCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture),
            _ => Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty,
        };
    }
}
