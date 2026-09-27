using System.ComponentModel;
using System.Data;
using Polhem.Base.Data;

namespace Polhem.Base.UnitTests
{
    /// <summary>
    /// Covers the value-level behaviour of <see cref="FieldDbType.Time"/>: a time of day is carried
    /// as a fixed-width <c>"HH:mm"</c> string, normalised on the way in, and has the empty string —
    /// never <c>"00:00"</c> — as its unset value (ADR-033).
    /// </summary>
    public class TimeOfDayValueTests
    {
        private static readonly string[] s_unsortedTimes = ["9:05", "23:59", "08:30", "0:00"];
        private static readonly string[] s_sortedTimes = ["00:00", "08:30", "09:05", "23:59"];

        [Fact]
        [DisplayName("Time maps to the CLR type string and DbType String")]
        public void Time_MapsToStringTypes()
        {
            Assert.Equal(typeof(string), DbTypeConverter.ToType(FieldDbType.Time));
            Assert.Equal(DbType.String, DbTypeConverter.ToDbType(FieldDbType.Time));
        }

        [Fact]
        [DisplayName("Time is the last enum member so existing wire payloads do not shift")]
        public void Time_IsAppendedAtEndOfEnum()
        {
            var values = Enum.GetValues<FieldDbType>();
            Assert.Equal(FieldDbType.Time, values[^1]);
        }

        [Fact]
        [DisplayName("The default value of Time is the empty string, not 00:00")]
        public void GetDefaultValue_Time_ReturnsEmptyString()
        {
            // Midnight is a legal time of day, so it cannot double as "unset".
            Assert.Equal(string.Empty, FieldDbType.Time.GetDefaultValue());
        }

        [Theory]
        [InlineData("08:30", "08:30")]
        [InlineData("8:30", "08:30")]
        [InlineData("  8:30  ", "08:30")]
        [InlineData("00:00", "00:00")]
        [InlineData("23:59", "23:59")]
        [InlineData("", "")]
        [InlineData("25:99", "")]
        [InlineData("abc", "")]
        [DisplayName("CTimeString normalizes a time of day to fixed-width HH:mm")]
        public void CTimeString_Time_NormalizesToFixedWidth(string input, string expected)
        {
            Assert.Equal(expected, ValueUtilities.CTimeString(input));
        }

        [Fact]
        [DisplayName("Normalized fixed-width strings sort chronologically in ordinal order")]
        public void NormalizedValues_SortChronologically()
        {
            var raw = s_unsortedTimes;
            var normalized = raw.Select(v => ValueUtilities.CTimeString(v)).ToList();
            var sorted = normalized.OrderBy(v => v, StringComparer.Ordinal).ToList();
            Assert.Equal(s_sortedTimes, sorted);
        }

        [Theory]
        [InlineData("08:30", 8, 30)]
        [InlineData("8:30", 8, 30)]
        [InlineData("00:00", 0, 0)]
        [InlineData("23:59", 23, 59)]
        [DisplayName("CTimeOnly parses a valid time-of-day string")]
        public void CTimeOnly_ValidText_ReturnsTimeOnly(string input, int hour, int minute)
        {
            Assert.Equal(new TimeOnly(hour, minute), ValueUtilities.CTimeOnly(input));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("25:00")]
        [InlineData("08:99")]
        [InlineData("8")]
        [InlineData("08:30:15")]
        [InlineData("abc")]
        [DisplayName("CTimeOnly returns null for an unset or malformed value")]
        public void CTimeOnly_UnsetOrMalformed_ReturnsNull(string input)
        {
            Assert.Null(ValueUtilities.CTimeOnly(input));
        }

        [Fact]
        [DisplayName("CTimeOnly returns null for null and DBNull")]
        public void CTimeOnly_NullLike_ReturnsNull()
        {
            Assert.Null(ValueUtilities.CTimeOnly(null));
            Assert.Null(ValueUtilities.CTimeOnly(DBNull.Value));
        }

        [Fact]
        [DisplayName("CTimeOnly accepts TimeOnly, DateTime and a TimeSpan within one day")]
        public void CTimeOnly_TemporalSources_Accepted()
        {
            Assert.Equal(new TimeOnly(8, 30), ValueUtilities.CTimeOnly(new TimeOnly(8, 30)));
            Assert.Equal(new TimeOnly(8, 30),
                ValueUtilities.CTimeOnly(new DateTime(2026, 7, 27, 8, 30, 0, DateTimeKind.Unspecified)));
            Assert.Equal(new TimeOnly(8, 30), ValueUtilities.CTimeOnly(new TimeSpan(8, 30, 0)));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(24)]
        [InlineData(30)]
        [DisplayName("CTimeOnly rejects a TimeSpan outside one day")]
        public void CTimeOnly_OutOfRangeTimeSpan_ReturnsNull(int hours)
        {
            // A TimeSpan is a duration and can hold values a time of day cannot; this method is the
            // gate that keeps them out of the framework.
            Assert.Null(ValueUtilities.CTimeOnly(TimeSpan.FromHours(hours)));
        }

        [Fact]
        [DisplayName("CTimeString outputs fixed-width HH:mm, or the empty string when unset")]
        public void CTimeString_NormalizesOrReturnsEmpty()
        {
            Assert.Equal("08:30", ValueUtilities.CTimeString("8:30"));
            Assert.Equal("08:30", ValueUtilities.CTimeString(new TimeOnly(8, 30)));
            Assert.Equal(string.Empty, ValueUtilities.CTimeString(null));
            Assert.Equal(string.Empty, ValueUtilities.CTimeString("25:00"));
        }

        [Fact]
        [DisplayName("TimeOnlyLength matches TimeOnlyFormat")]
        public void TimeOnlyLength_MatchesFormat()
        {
            Assert.Equal(5, ValueUtilities.TimeOnlyLength);
            Assert.Equal(ValueUtilities.TimeOnlyFormat.Length, ValueUtilities.TimeOnlyLength);
        }

        [Fact]
        [DisplayName("AddColumn(Time) creates a string column that keeps the Time marker")]
        public void AddColumn_Time_IsStringColumnCarryingTheMarker()
        {
            var table = new DataTable("probe");
            var column = table.AddColumn("work_start", FieldDbType.Time);

            Assert.Equal(typeof(string), column.DataType);
            // Without the marker the column would read back as a plain String on the wire, which is
            // exactly the self-description this type exists to provide.
            Assert.Equal(FieldDbType.Time, column.ResolveFieldDbType());
        }
    }
}
