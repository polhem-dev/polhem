namespace Polhem.UI.Core.Criteria
{
    /// <summary>
    /// A period named relative to today, resolved each time a query runs.
    /// </summary>
    public enum RelativeDateKind
    {
        /// <summary>Today.</summary>
        Today,

        /// <summary>Yesterday.</summary>
        Yesterday,

        /// <summary>The week that contains today.</summary>
        ThisWeek,

        /// <summary>The week before this week.</summary>
        LastWeek,

        /// <summary>The month that contains today.</summary>
        ThisMonth,

        /// <summary>The month before this month.</summary>
        LastMonth,

        /// <summary>The calendar quarter that contains today.</summary>
        ThisQuarter,

        /// <summary>The year that contains today.</summary>
        ThisYear,

        /// <summary>The last given number of days, ending today.</summary>
        LastDays,
    }
}
