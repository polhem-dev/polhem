using System.ComponentModel;
using Polhem.UI.Core.Criteria;

namespace Polhem.UI.Core.UnitTests.Criteria
{
    public class RelativeDateTests
    {
        // 2026-11-17 is a Tuesday.
        private static readonly DateOnly s_today = new(2026, 11, 17);

        [Theory]
        [DisplayName("Resolve returns the first and last day of each period, both included")]
        [InlineData(RelativeDateKind.Today, 0, "2026-11-17", "2026-11-17")]
        [InlineData(RelativeDateKind.Yesterday, 0, "2026-11-16", "2026-11-16")]
        [InlineData(RelativeDateKind.ThisMonth, 0, "2026-11-01", "2026-11-30")]
        [InlineData(RelativeDateKind.LastMonth, 0, "2026-10-01", "2026-10-31")]
        [InlineData(RelativeDateKind.ThisQuarter, 0, "2026-10-01", "2026-12-31")]
        [InlineData(RelativeDateKind.ThisYear, 0, "2026-01-01", "2026-12-31")]
        [InlineData(RelativeDateKind.LastDays, 7, "2026-11-11", "2026-11-17")]
        [InlineData(RelativeDateKind.LastDays, 1, "2026-11-17", "2026-11-17")]
        public void Resolve_EachKind_ReturnsInclusiveDays(RelativeDateKind kind, int days, string from, string to)
        {
            var range = new RelativeDate(kind, days).Resolve(s_today, DayOfWeek.Monday);

            Assert.Equal(DateOnly.Parse(from, System.Globalization.CultureInfo.InvariantCulture), range.From);
            Assert.Equal(DateOnly.Parse(to, System.Globalization.CultureInfo.InvariantCulture), range.To);
        }

        [Theory]
        [DisplayName("The week periods start on the given first day of the week")]
        [InlineData(DayOfWeek.Monday, RelativeDateKind.ThisWeek, "2026-11-16", "2026-11-22")]
        [InlineData(DayOfWeek.Sunday, RelativeDateKind.ThisWeek, "2026-11-15", "2026-11-21")]
        [InlineData(DayOfWeek.Monday, RelativeDateKind.LastWeek, "2026-11-09", "2026-11-15")]
        [InlineData(DayOfWeek.Tuesday, RelativeDateKind.ThisWeek, "2026-11-17", "2026-11-23")]
        public void Resolve_Week_StartsOnFirstDayOfWeek(DayOfWeek firstDay, RelativeDateKind kind, string from, string to)
        {
            var range = new RelativeDate(kind).Resolve(s_today, firstDay);

            Assert.Equal(DateOnly.Parse(from, System.Globalization.CultureInfo.InvariantCulture), range.From);
            Assert.Equal(DateOnly.Parse(to, System.Globalization.CultureInfo.InvariantCulture), range.To);
        }

        [Fact]
        [DisplayName("LastMonth in January and Yesterday on 1 March of a leap year cross the boundary correctly")]
        public void Resolve_Boundaries_CrossYearAndLeapDay()
        {
            var lastMonth = new RelativeDate(RelativeDateKind.LastMonth).Resolve(new DateOnly(2027, 1, 15), DayOfWeek.Monday);
            var yesterday = new RelativeDate(RelativeDateKind.Yesterday).Resolve(new DateOnly(2028, 3, 1), DayOfWeek.Monday);

            Assert.Equal((new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)), lastMonth);
            Assert.Equal((new DateOnly(2028, 2, 29), new DateOnly(2028, 2, 29)), yesterday);
        }

        [Fact]
        [DisplayName("LastDays requires at least one day and other kinds take no days")]
        public void Constructor_InvalidDays_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RelativeDate(RelativeDateKind.LastDays, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RelativeDate(RelativeDateKind.ThisMonth, 3));
        }
    }
}
