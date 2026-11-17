namespace Polhem.UI.Core.Criteria
{
    /// <summary>
    /// A date range named relative to today, such as this month or the last seven days. It is kept as such and
    /// resolved when the criteria are converted, so a query saved today still means "this month" next month.
    /// </summary>
    public readonly record struct RelativeDate
    {
        /// <summary>
        /// Initializes a new instance of <see cref="RelativeDate"/>.
        /// </summary>
        /// <param name="kind">The period.</param>
        /// <param name="days">The number of days, for <see cref="RelativeDateKind.LastDays"/> only.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="days"/> is less than one for <see cref="RelativeDateKind.LastDays"/>, or not zero for any other kind.
        /// </exception>
        public RelativeDate(RelativeDateKind kind, int days = 0)
        {
            if (kind == RelativeDateKind.LastDays)
                ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);
            else if (days != 0)
                throw new ArgumentOutOfRangeException(nameof(days), days, "Only a LastDays period takes a number of days.");
            Kind = kind;
            Days = days;
        }

        /// <summary>Gets the period.</summary>
        public RelativeDateKind Kind { get; }

        /// <summary>Gets the number of days of a <see cref="RelativeDateKind.LastDays"/> period; zero otherwise.</summary>
        public int Days { get; }

        /// <summary>
        /// Returns the first and the last day of the period, both included.
        /// </summary>
        /// <param name="today">Today in the user's time zone.</param>
        /// <param name="firstDayOfWeek">The day a week starts on, for the week periods.</param>
        /// <returns>The first and the last day.</returns>
        public (DateOnly From, DateOnly To) Resolve(DateOnly today, DayOfWeek firstDayOfWeek)
        {
            switch (Kind)
            {
                case RelativeDateKind.Today:
                    return (today, today);
                case RelativeDateKind.Yesterday:
                    return (today.AddDays(-1), today.AddDays(-1));
                case RelativeDateKind.ThisWeek:
                    return WeekOf(today, firstDayOfWeek);
                case RelativeDateKind.LastWeek:
                    return WeekOf(today.AddDays(-7), firstDayOfWeek);
                case RelativeDateKind.ThisMonth:
                    return MonthOf(today);
                case RelativeDateKind.LastMonth:
                    return MonthOf(today.AddMonths(-1));
                case RelativeDateKind.ThisQuarter:
                    var quarterStart = new DateOnly(today.Year, ((today.Month - 1) / 3 * 3) + 1, 1);
                    return (quarterStart, quarterStart.AddMonths(3).AddDays(-1));
                case RelativeDateKind.ThisYear:
                    return (new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 12, 31));
                case RelativeDateKind.LastDays:
                    return (today.AddDays(1 - Days), today);
                default:
                    throw new InvalidOperationException($"Unknown relative date kind {Kind}.");
            }
        }

        private static (DateOnly From, DateOnly To) WeekOf(DateOnly day, DayOfWeek firstDayOfWeek)
        {
            int offset = ((int)day.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
            var start = day.AddDays(-offset);
            return (start, start.AddDays(6));
        }

        private static (DateOnly From, DateOnly To) MonthOf(DateOnly day)
        {
            var start = new DateOnly(day.Year, day.Month, 1);
            return (start, start.AddMonths(1).AddDays(-1));
        }
    }
}
