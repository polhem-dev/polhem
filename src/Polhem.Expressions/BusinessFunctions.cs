using System.Globalization;
using System.Text.RegularExpressions;
using Polhem.Core.Expressions;

namespace Polhem.Expressions
{
    /// <summary>
    /// The bodies of the business helper functions <see cref="DynamicExpressoEvaluator"/> registers: checks over the
    /// values of one form row that read like a sentence, such as <c>IsBlank(approver_id)</c> or
    /// <c>Between(amount, 1, 100000)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every function computes from its arguments alone: none reads the database, the session or anything outside the
    /// form.
    /// </para>
    /// <para>
    /// <see cref="Between"/> and <see cref="In"/> take any value, because a form row mixes number types (an
    /// <see cref="int"/> quantity, a <see cref="decimal"/> amount) and date types (a date field is a
    /// <see cref="DateTime"/>, <c>Today()</c> a <see cref="DateOnly"/>). They compare numbers as numbers, dates as
    /// calendar instants and text ordinally; values of different kinds cannot be compared and throw
    /// <see cref="ExpressionEvaluationException"/>, the error an expression that does not fit its data raises.
    /// </para>
    /// </remarks>
    internal static class BusinessFunctions
    {
        // Matches the timeout every regular expression in the framework takes (SonarCloud S6444).
        private static readonly TimeSpan s_regexTimeout = TimeSpan.FromSeconds(1);

        /// <summary>The signature of <see cref="In"/>: the value, then the candidates as separate arguments.</summary>
        internal delegate bool InFunction(object? value, params object?[] candidates);

        /// <summary>
        /// Whether a value counts as not filled in: empty, an empty or blank text, <see cref="Guid.Empty"/>, zero, or
        /// the minimum date.
        /// </summary>
        /// <remarks>
        /// Text and number columns are never null in the framework: an unfilled one holds an empty string or zero, so
        /// zero counts as blank. A <see cref="bool"/> is never blank.
        /// </remarks>
        public static bool IsBlank(object? value) => value switch
        {
            null or DBNull => true,
            string text => string.IsNullOrWhiteSpace(text),
            Guid guid => guid == Guid.Empty,
            DateTime dateTime => dateTime == DateTime.MinValue,
            DateOnly date => date == DateOnly.MinValue,
            byte[] bytes => bytes.Length == 0,
            bool => false,
            _ when TryNumber(value, out var number) => number == 0m,
            _ => false,
        };

        /// <summary>Whether <paramref name="value"/> lies between <paramref name="low"/> and <paramref name="high"/>, both included.</summary>
        public static bool Between(object? value, object? low, object? high) =>
            Compare(value, low, nameof(Between)) >= 0 && Compare(value, high, nameof(Between)) <= 0;

        /// <summary>Whether <paramref name="value"/> equals one of <paramref name="candidates"/>.</summary>
        public static bool In(object? value, params object?[] candidates)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            return candidates.Any(candidate => IsEmpty(value) ? IsEmpty(candidate)
                : !IsEmpty(candidate) && Compare(value, candidate, nameof(In)) == 0);
        }

        /// <summary>The number of days from <paramref name="start"/> to <paramref name="end"/>; negative when the end is earlier.</summary>
        public static int DaysBetween(DateTime start, DateTime end) =>
            DateOnly.FromDateTime(end).DayNumber - DateOnly.FromDateTime(start).DayNumber;

        /// <inheritdoc cref="DaysBetween(DateTime, DateTime)"/>
        public static int DaysBetween(DateOnly start, DateOnly end) => end.DayNumber - start.DayNumber;

        /// <inheritdoc cref="DaysBetween(DateTime, DateTime)"/>
        public static int DaysBetween(DateTime start, DateOnly end) => end.DayNumber - DateOnly.FromDateTime(start).DayNumber;

        /// <inheritdoc cref="DaysBetween(DateTime, DateTime)"/>
        public static int DaysBetween(DateOnly start, DateTime end) => DateOnly.FromDateTime(end).DayNumber - start.DayNumber;

        /// <summary>The last day of the month of <paramref name="date"/>, at midnight.</summary>
        public static DateTime MonthEnd(DateTime date) =>
            new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month), 0, 0, 0, date.Kind);

        /// <summary>The last day of the month of <paramref name="date"/>.</summary>
        public static DateOnly MonthEnd(DateOnly date) => new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

        /// <summary>Whether <paramref name="text"/> matches the regular expression <paramref name="pattern"/>; an empty text never matches.</summary>
        /// <remarks>The pattern is matched without backtracking, so backreferences and lookarounds are not supported.</remarks>
        /// <exception cref="ExpressionEvaluationException">The pattern is not a valid regular expression, uses a backreference or a lookaround, or matching takes too long.</exception>
        public static bool RegexMatch(string? text, string pattern)
        {
            if (string.IsNullOrEmpty(text)) { return false; }
            try
            {
                // IMPORTANT: NonBacktracking matches in time linear in the input, so a pattern such as `^(a|aa)+$`
                // cannot spend its whole timeout on every call; the timeout alone bounds one call, not an expression
                // that calls RegexMatch many times, or once per detail row. It rejects backreferences and lookarounds,
                // which surface as an invalid pattern.
                return Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
                    s_regexTimeout);
            }
            catch (ArgumentException ex)
            {
                throw new ExpressionEvaluationException($"RegexMatch has an invalid pattern: {ex.Message}", ex);
            }
            catch (NotSupportedException ex)
            {
                // A backreference or a lookaround, which matching without backtracking cannot do.
                throw new ExpressionEvaluationException($"RegexMatch does not support this pattern: {ex.Message}", ex);
            }
            catch (RegexMatchTimeoutException ex)
            {
                throw new ExpressionEvaluationException("RegexMatch took too long to match its pattern.", ex);
            }
        }

        private static bool IsEmpty(object? value) => value is null or DBNull;

        // Numbers compare as decimals, dates as instants (a DateOnly is its midnight) and text ordinally, so a field
        // compares with a literal or a helper result of a different but compatible type.
        private static int Compare(object? left, object? right, string function)
        {
            if (TryNumber(left, out var leftNumber) && TryNumber(right, out var rightNumber))
                return leftNumber.CompareTo(rightNumber);
            if (TryInstant(left, out var leftInstant) && TryInstant(right, out var rightInstant))
                return leftInstant.CompareTo(rightInstant);
            if (left is string leftText && right is string rightText)
                return string.CompareOrdinal(leftText, rightText);
            throw new ExpressionEvaluationException(string.Create(CultureInfo.InvariantCulture,
                $"{function} cannot compare {Describe(left)} with {Describe(right)}."));
        }

        private static bool TryNumber(object? value, out decimal number)
        {
            switch (value)
            {
                case byte or sbyte or short or ushort or int or uint or long or ulong or decimal:
                    number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                    return true;
                case double or float:
                    var real = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    var fits = !double.IsNaN(real) && !double.IsInfinity(real)
                        && real >= (double)decimal.MinValue && real <= (double)decimal.MaxValue;
                    number = fits ? (decimal)real : 0m;
                    return fits;
                default:
                    number = 0m;
                    return false;
            }
        }

        private static bool TryInstant(object? value, out DateTime instant)
        {
            switch (value)
            {
                case DateTime dateTime:
                    instant = dateTime;
                    return true;
                case DateOnly date:
                    instant = date.ToDateTime(TimeOnly.MinValue);
                    return true;
                default:
                    instant = default;
                    return false;
            }
        }

        private static string Describe(object? value) => value is null or DBNull ? "an empty value" : $"a {value.GetType().Name}";
    }
}
