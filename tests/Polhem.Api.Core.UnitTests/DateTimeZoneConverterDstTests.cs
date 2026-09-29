using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.JsonRpc;
using Polhem.Core.Data;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// The behavior of <see cref="DateTimeZoneConverter"/> in a time zone that observes DST. The other tests only use
    /// <c>Asia/Taipei</c> and <c>Pacific/Kiritimati</c>, both fixed offsets that do not observe DST.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In the request direction, only filter values are still converted from the user's time zone to UTC, so the
    /// spring-forward gap is verified through <see cref="DateTimeZoneConverter.ConvertFilterValue"/>. A <c>DataSet</c>
    /// is not converted in the request direction. Correct "read then save back" behavior in the fall-back overlap is
    /// instead ensured by the server reading back the database value; see <c>DateTimeZoneDstSaveRoundTripTests</c>.
    /// </para>
    /// <para>
    /// The tests do not assume that the transition date is a boundary. They first assert the precondition with
    /// <c>TimeZoneInfo.IsInvalidTime</c>, so if the runtime's tzdata differs from what is expected, the test fails
    /// clearly at the precondition instead of passing for the wrong reason.
    /// </para>
    /// </remarks>
    public class DateTimeZoneConverterDstTests
    {
        private const string NewYork = "America/New_York";

        private static TimeZoneInfo Zone => TimeZoneInfo.FindSystemTimeZoneById(NewYork);

        // US DST in 2026 springs forward at 02:00 on March 8, so 02:00–02:59 does not exist.
        private static readonly DateTime s_springForwardGap = new(2026, 3, 8, 2, 30, 0, DateTimeKind.Unspecified);

        private static DataTable BuildTableWithInstant(DateTime value)
        {
            var table = new DataTable("events");
            table.AddColumn("occurred_at", FieldDbType.DateTime);
            table.Rows.Add(value);
            table.AcceptChanges();
            return table;
        }

        [Fact]
        [DisplayName("Precondition: 2026-03-08 02:30 does not exist in America/New_York")]
        public void Precondition_SpringForwardGapIsInvalid()
        {
            Assert.True(Zone.IsInvalidTime(s_springForwardGap));
        }

        [Fact]
        [DisplayName("UtcToUser applies different offsets before and after DST starts instead of a fixed offset")]
        public void UtcToUser_HonoursDstOffsetChange()
        {
            // On the same day, the offset before DST is EST (UTC-5) and after DST is EDT (UTC-4).
            var beforeUtc = new DateTime(2026, 3, 8, 6, 0, 0, DateTimeKind.Unspecified);
            var afterUtc = new DateTime(2026, 3, 8, 8, 0, 0, DateTimeKind.Unspecified);

            var converted = DateTimeZoneConverter.UtcToUser(
                BuildTableWithInstant(beforeUtc), NewYork);
            var convertedAfter = DateTimeZoneConverter.UtcToUser(
                BuildTableWithInstant(afterUtc), NewYork);

            Assert.NotNull(converted);
            Assert.NotNull(convertedAfter);

            var beforeOffset = beforeUtc - (DateTime)converted.Rows[0]["occurred_at"];
            var afterOffset = afterUtc - (DateTime)convertedAfter.Rows[0]["occurred_at"];

            Assert.NotEqual(beforeOffset, afterOffset);
            Assert.Equal(TimeSpan.FromHours(1), beforeOffset - afterOffset);
        }

        [Fact]
        [DisplayName("A filter value inside the spring-forward gap is moved forward by the DST delta instead of throwing")]
        public void ConvertFilterValue_InvalidLocalTime_SkipsGapForward()
        {
            var converted = (DateTime)DateTimeZoneConverter.ConvertFilterValue(s_springForwardGap, NewYork, toUtc: true)!;

            // 02:30 does not exist. Moving it forward by the transition's delta (1 hour in this zone) gives 03:30, a real time.
            var gap = Zone.GetUtcOffset(s_springForwardGap.Date.AddDays(1))
                      - Zone.GetUtcOffset(s_springForwardGap.Date.AddDays(-1));
            var expected = DateTime.SpecifyKind(
                TimeZoneInfo.ConvertTimeToUtc(s_springForwardGap.Add(gap), Zone), DateTimeKind.Unspecified);

            Assert.Equal(expected, converted);
        }

        [Fact]
        [DisplayName("A moved-forward filter value converted back to the user's time zone lands after the gap on a time that exists")]
        public void ConvertFilterValue_InvalidLocalTime_ResultRoundTripsToARealTime()
        {
            var utc = (DateTime)DateTimeZoneConverter.ConvertFilterValue(s_springForwardGap, NewYork, toUtc: true)!;
            var backInZone = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utc, DateTimeKind.Unspecified), Zone);

            Assert.False(Zone.IsInvalidTime(backInZone));
            Assert.True(backInZone > s_springForwardGap);
        }
    }
}
