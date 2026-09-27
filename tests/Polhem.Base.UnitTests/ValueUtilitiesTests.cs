using System.Collections;
using System.ComponentModel;

namespace Polhem.Base.UnitTests
{
    /// <summary>
    /// Tests for <see cref="ValueUtilities"/> covering value-emptiness checks
    /// (<c>IsEmpty</c> overloads / <c>IsNullOrDBNull</c>) and the framework's
    /// <c>Cxxx</c> type-conversion API. The framework encapsulates
    /// <see cref="System.Globalization.CultureInfo.InvariantCulture"/> and
    /// numeric parsing defaults so call sites do not pass them.
    /// </summary>
    public class ValueUtilitiesTests
    {
        private static readonly int[] s_singleIntArray = { 1 };

        // ---- IsNullOrDBNull / IsEmpty ----

        [Fact]
        [DisplayName("IsNullOrDBNull returns true for null and DBNull and false for a real value")]
        public void IsNullOrDBNull_VariousValues_ReturnsExpectedResult()
        {
            Assert.True(ValueUtilities.IsNullOrDBNull(null));
            Assert.True(ValueUtilities.IsNullOrDBNull(DBNull.Value));
            Assert.False(ValueUtilities.IsNullOrDBNull("value"));
            Assert.False(ValueUtilities.IsNullOrDBNull(0));
            Assert.False(ValueUtilities.IsNullOrDBNull(new object()));
        }

        [Fact]
        [DisplayName("IsEmpty(object) dispatches to the overload that matches each type")]
        public void IsEmpty_Object_DispatchesToCorrectOverload()
        {
            Assert.True(ValueUtilities.IsEmpty((object)null!));
            Assert.True(ValueUtilities.IsEmpty((object)DBNull.Value));
            Assert.True(ValueUtilities.IsEmpty((object)string.Empty));
            Assert.True(ValueUtilities.IsEmpty((object)"   "));
            Assert.True(ValueUtilities.IsEmpty((object)Guid.Empty));
            Assert.True(ValueUtilities.IsEmpty((object)new List<int>()));
            Assert.True(ValueUtilities.IsEmpty((object)DateTime.MinValue));

            Assert.False(ValueUtilities.IsEmpty((object)"abc"));
            Assert.False(ValueUtilities.IsEmpty((object)Guid.NewGuid()));
            Assert.False(ValueUtilities.IsEmpty((object)new List<int> { 1 }));
            Assert.False(ValueUtilities.IsEmpty((object)new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)));
            Assert.False(ValueUtilities.IsEmpty((object)123));
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("   ", true)]
        [InlineData("abc", false)]
        [DisplayName("IsEmpty(string) returns true for null, empty and whitespace")]
        public void IsEmpty_String_ReturnsExpectedResult(string? value, bool expected)
        {
            Assert.Equal(expected, ValueUtilities.IsEmpty(value!));
        }

        [Fact]
        [DisplayName("IsEmpty(Guid) returns true for Guid.Empty and false for other values")]
        public void IsEmpty_Guid_ReturnsExpectedResult()
        {
            Assert.True(ValueUtilities.IsEmpty(Guid.Empty));
            Assert.False(ValueUtilities.IsEmpty(Guid.NewGuid()));
        }

        [Fact]
        [DisplayName("IsEmpty(DateTime) returns true for MinValue and dates before 1753 and false for ordinary dates")]
        public void IsEmpty_DateTime_ReturnsExpectedResult()
        {
            Assert.True(ValueUtilities.IsEmpty(DateTime.MinValue));
            Assert.True(ValueUtilities.IsEmpty(new DateTime(1752, 12, 31, 0, 0, 0, DateTimeKind.Unspecified)));
            Assert.False(ValueUtilities.IsEmpty(new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)));
            Assert.False(ValueUtilities.IsEmpty(new DateTime(2026, 4, 18, 0, 0, 0, DateTimeKind.Unspecified)));
        }

        [Fact]
        [DisplayName("IsEmpty(IList) returns true for null and an empty list and false for a list with elements")]
        public void IsEmpty_IList_ReturnsExpectedResult()
        {
            Assert.True(ValueUtilities.IsEmpty((IList)null!));
            Assert.True(ValueUtilities.IsEmpty((IList)new List<int>()));
            Assert.False(ValueUtilities.IsEmpty((IList)new List<int> { 1, 2 }));
        }

        [Fact]
        [DisplayName("IsEmpty(IEnumerable) returns true for null and no elements and false when there are elements")]
        public void IsEmpty_IEnumerable_ReturnsExpectedResult()
        {
            Assert.True(ValueUtilities.IsEmpty((IEnumerable)null!));
            Assert.True(ValueUtilities.IsEmpty((IEnumerable)Array.Empty<int>()));
            Assert.False(ValueUtilities.IsEmpty((IEnumerable)s_singleIntArray));
        }

        [Fact]
        [DisplayName("IsEmpty(byte[]) returns true for null and an empty array and false for an array with content")]
        public void IsEmpty_ByteArray_ReturnsExpectedResult()
        {
            Assert.True(ValueUtilities.IsEmpty((byte[])null!));
            Assert.True(ValueUtilities.IsEmpty(Array.Empty<byte>()));
            Assert.False(ValueUtilities.IsEmpty(new byte[] { 0x01 }));
        }

        // ---- CStr ----

        [Fact]
        [DisplayName("CStr(object) returns the matching string representation for various inputs")]
        public void CStr_Object_ReturnsExpectedString()
        {
            Assert.Equal(string.Empty, ValueUtilities.CStr(null!));
            Assert.Equal(string.Empty, ValueUtilities.CStr(DBNull.Value));
            Assert.Equal("abc", ValueUtilities.CStr("abc"));
            Assert.Equal("Day", ValueUtilities.CStr(TestInterval.Day));
            Assert.Equal("123", ValueUtilities.CStr(123));
        }

        [Fact]
        [DisplayName("CStr(object, defaultValue) returns defaultValue for null and DBNull and a string otherwise")]
        public void CStr_ObjectWithDefault_ReturnsExpectedString()
        {
            Assert.Equal("N/A", ValueUtilities.CStr(null!, "N/A"));
            Assert.Equal("N/A", ValueUtilities.CStr(DBNull.Value, "N/A"));
            Assert.Equal("abc", ValueUtilities.CStr("abc", "N/A"));
            Assert.Equal("Day", ValueUtilities.CStr(TestInterval.Day, "N/A"));
        }

        // ---- CBool ----

        [Theory]
        [InlineData("1", true)]
        [InlineData("T", true)]
        [InlineData("TRUE", true)]
        [InlineData("true", true)]
        [InlineData("Y", true)]
        [InlineData("YES", true)]
        [InlineData("是", true)]
        [InlineData("真", true)]
        [InlineData("0", false)]
        [InlineData("N", false)]
        [InlineData("false", false)]
        [InlineData("other", false)]
        [DisplayName("CBool(string) returns the matching boolean for common true and false strings")]
        public void CBool_String_ReturnsExpectedResult(string value, bool expected)
        {
            Assert.Equal(expected, ValueUtilities.CBool(value));
        }

        [Fact]
        [DisplayName("CBool(string) returns defaultValue for an empty string")]
        public void CBool_String_Empty_ReturnsDefault()
        {
            Assert.False(ValueUtilities.CBool(""));
            Assert.True(ValueUtilities.CBool("", true));
            Assert.True(ValueUtilities.CBool(null!, true));
        }

        [Fact]
        [DisplayName("CBool(object) casts a bool directly and converts other types through their string form")]
        public void CBool_Object_ReturnsExpectedResult()
        {
            Assert.True(ValueUtilities.CBool((object)true));
            Assert.False(ValueUtilities.CBool((object)false));
            Assert.True(ValueUtilities.CBool((object)"1"));
            Assert.False(ValueUtilities.CBool((object)"0"));
            Assert.False(ValueUtilities.CBool((object)null!));
            Assert.True(ValueUtilities.CBool((object)null!, true));
        }

        // ---- CEnum ----

        [Theory]
        [InlineData("Day", TestInterval.Day)]
        [InlineData("day", TestInterval.Day)] // case-insensitive by framework default
        [InlineData("Hour", TestInterval.Hour)]
        [DisplayName("CEnum(string, Type) returns the matching enum value for a valid string (case-insensitive)")]
        public void CEnum_ValidString_ReturnsEnumValue(string input, TestInterval expected)
        {
            // This test deliberately calls the non-generic overload to verify its behavior.
#pragma warning disable CA2263 // Prefer generic overload when type is known
            var result = ValueUtilities.CEnum(input, typeof(TestInterval));
#pragma warning restore CA2263
            Assert.Equal(expected, (TestInterval)result);
        }

        [Fact]
        [DisplayName("CEnum<T>(string) returns the enum value for a valid string and throws ArgumentException for an invalid one")]
        public void CEnum_Generic_ValidAndInvalid_BehavesAsExpected()
        {
            Assert.Equal(TestInterval.Day, ValueUtilities.CEnum<TestInterval>("Day"));
            Assert.Throws<ArgumentException>(() => ValueUtilities.CEnum<TestInterval>("NotExist"));
        }

        // ---- IsNumeric / ConvertToNumber ----

        [Fact]
        [DisplayName("IsNumeric correctly decides whether values of various types are numeric")]
        public void IsNumeric_VariousTypes_ReturnsExpectedResult()
        {
            // Booleans
            Assert.True(ValueUtilities.IsNumeric(true));
            Assert.True(ValueUtilities.IsNumeric(false));

            // Enum
            Assert.True(ValueUtilities.IsNumeric(TestInterval.Day));
            Assert.True(ValueUtilities.IsNumeric(TestInterval.Hour));

            // Numeric types
            Assert.True(ValueUtilities.IsNumeric(123));
            Assert.True(ValueUtilities.IsNumeric(123.45));
            Assert.True(ValueUtilities.IsNumeric(123.45m));

            // Strings
            Assert.True(ValueUtilities.IsNumeric("123"));
            Assert.True(ValueUtilities.IsNumeric("123.45"));
            Assert.False(ValueUtilities.IsNumeric("abc"));

            // Special values
            Assert.False(ValueUtilities.IsNumeric(null!));
            Assert.False(ValueUtilities.IsNumeric(new object()));
            Assert.False(ValueUtilities.IsNumeric(DateTime.Now));
        }

        [Theory]
        [InlineData("12345", 5, true)]
        [InlineData("12345", 4, false)]
        [InlineData("abc", 3, false)]
        [InlineData("", 0, false)]
        [DisplayName("IsNumeric(string, length) checks both that the value is numeric and its length")]
        public void IsNumeric_WithLength_ChecksBothConditions(string value, int length, bool expected)
        {
            Assert.Equal(expected, ValueUtilities.IsNumeric(value, length));
        }

        [Fact]
        [DisplayName("ConvertToNumber returns the matching number for various inputs")]
        public void ConvertToNumber_VariousInputs_ReturnsExpectedResult()
        {
            Assert.Equal(0, ValueUtilities.ConvertToNumber(null!));
            Assert.Equal(0, ValueUtilities.ConvertToNumber(DBNull.Value));
            Assert.Equal(0, ValueUtilities.ConvertToNumber(""));

            Assert.Equal((double)123.45, ValueUtilities.ConvertToNumber("123.45"));
            Assert.Equal(1, ValueUtilities.ConvertToNumber(true));
            Assert.Equal(0, ValueUtilities.ConvertToNumber(false));
            Assert.Equal((int)TestInterval.Day, ValueUtilities.ConvertToNumber(TestInterval.Day));
            Assert.Equal(123, ValueUtilities.ConvertToNumber(123));
            Assert.Equal(123.45m, ValueUtilities.ConvertToNumber(123.45m));
        }

        [Fact]
        [DisplayName("ConvertToNumber throws InvalidCastException for a value it cannot convert")]
        public void ConvertToNumber_InvalidInput_Throws()
        {
            Assert.Throws<InvalidCastException>(() => ValueUtilities.ConvertToNumber(new object()));
        }

        private sealed class NumericToString
        {
            public override string ToString() => "3.14";
        }

        [Fact]
        [DisplayName("ConvertToNumber returns a double for an unsupported type whose ToString is numeric")]
        public void ConvertToNumber_NonStandardType_FallsBackToToString()
        {
            // This reaches the final `double.TryParse(value.ToString(), ...)` branch.
            var value = new NumericToString();
            var result = ValueUtilities.ConvertToNumber(value);
            Assert.Equal(3.14d, result);
        }

        // ---- CInt / CDouble / CDecimal ----

        [Theory]
        [InlineData(null, 0)]
        [InlineData("", 0)]
        [InlineData("123", 123)]
        [InlineData("  ", 0)]
        [InlineData(123, 123)]
        [InlineData(true, 1)]
        [InlineData(false, 0)]
        [InlineData("abc", 0)] // unconvertible, returns defaultValue
        [DisplayName("CInt returns the matching integer for various inputs and defaultValue when it cannot convert")]
        public void CInt_VariousInputs_ReturnsExpectedResult(object? value, int expected)
        {
            Assert.Equal(expected, ValueUtilities.CInt(value!));
        }

        [Fact]
        [DisplayName("CInt returns the underlying integer of an enum")]
        public void CInt_Enum_ReturnsIntegerValue()
        {
            Assert.Equal((int)TestInterval.Day, ValueUtilities.CInt(TestInterval.Day));
        }

        [Fact]
        [DisplayName("CDouble returns the matching double for various inputs and defaultValue when it cannot convert")]
        public void CDouble_VariousInputs_ReturnsExpectedResult()
        {
            Assert.Equal(0d, ValueUtilities.CDouble(null!));
            Assert.Equal(0d, ValueUtilities.CDouble(DBNull.Value));
            Assert.Equal(123.45, ValueUtilities.CDouble("123.45"));
            Assert.Equal(1d, ValueUtilities.CDouble(true));
            Assert.Equal(-1.5d, ValueUtilities.CDouble("abc", -1.5));
        }

        [Fact]
        [DisplayName("CDecimal returns the matching decimal for various inputs and defaultValue when it cannot convert")]
        public void CDecimal_VariousInputs_ReturnsExpectedResult()
        {
            Assert.Equal(0m, ValueUtilities.CDecimal(null!));
            Assert.Equal(0m, ValueUtilities.CDecimal(DBNull.Value));
            Assert.Equal(123.45m, ValueUtilities.CDecimal("123.45"));
            Assert.Equal(1m, ValueUtilities.CDecimal(true));
            Assert.Equal(-1.5m, ValueUtilities.CDecimal("abc", -1.5m));
        }

        // ---- CDateTime / CDateOnly ----

        [Fact]
        [DisplayName("CDateTime returns the matching DateTime for various inputs")]
        public void CDateTime_VariousInputs_ReturnsExpectedResult()
        {
            // Unset input yields null, not a sentinel the caller has to remember to compare against.
            Assert.Null(ValueUtilities.CDateTime(null!));
            Assert.Null(ValueUtilities.CDateTime(DBNull.Value));
            Assert.Null(ValueUtilities.CDateTime(""));

            var expected = new DateTime(2015, 3, 12, 0, 0, 0, DateTimeKind.Unspecified);
            Assert.Equal(expected, ValueUtilities.CDateTime(expected));
            Assert.Equal(expected, ValueUtilities.CDateTime("2015-03-12"));
            Assert.Equal(expected, ValueUtilities.CDateTime("20150312"));

            // ROC (Minguo) calendar date.
            Assert.Equal(expected, ValueUtilities.CDateTime("1040312"));

            // A non-numeric string is not recognized as a date and yields null without throwing.
            Assert.Null(ValueUtilities.CDateTime("not-a-date"));
        }

        [Theory]
        [InlineData("20150312", 2015, 3, 12)] // 8-digit Gregorian
        [InlineData("1040312", 2015, 3, 12)]  // 7-digit ROC
        [InlineData("201503", 2015, 3, 1)]    // 6-digit Gregorian year and month
        [InlineData("10403", 2015, 3, 1)]     // 5-digit ROC year and month
        [InlineData("2015", 2015, 1, 1)]      // 4-digit Gregorian year
        [InlineData("104", 2015, 1, 1)]       // 3-digit ROC year
        [DisplayName("CDateTime parses the date format that matches the string length")]
        public void CDateTime_VariousLengths_ParsesCorrectly(string input, int y, int m, int d)
        {
            var result = ValueUtilities.CDateTime(input);
            Assert.Equal(new DateTime(y, m, d), result);
        }

        [Fact]
        [DisplayName("CDateTime returns null for a numeric string of an unsupported length")]
        public void CDateTime_UnsupportedLength_ReturnsNull()
        {
            // Length 2 is outside the lengths 3 to 8 handled by the switch, so it is not recognized as a date.
            Assert.Null(ValueUtilities.CDateTime("12"));
        }

        [Fact]
        [DisplayName("CDateTime returns null for a non-numeric string")]
        public void CDateTime_NonNumericString_ReturnsNull()
        {
            // "abcdefgh" is not numeric after the separators are removed, so it is not recognized as a date.
            Assert.Null(ValueUtilities.CDateTime("abcdefgh"));
        }

        [Fact]
        [DisplayName("CDateTime returns the given default for unrecognizable input")]
        public void CDateTime_Unrecognizable_WithExplicitDefault_ReturnsDefault()
        {
            // Earlier versions always returned `MinValue` on this path and ignored the caller's default. The explicit overload makes the default take effect.
            var fallback = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
            Assert.Equal(fallback, ValueUtilities.CDateTime("abcdefgh", fallback));
            Assert.Equal(fallback, ValueUtilities.CDateTime(DBNull.Value, fallback));
        }

        [Fact]
        [DisplayName("The single-argument overloads of the temporal family return nullable values")]
        public void TemporalFamily_SingleArgumentOverloads_AreNullable()
        {
            Assert.Null(ValueUtilities.CDateTime(DBNull.Value));
            Assert.Null(ValueUtilities.CDateOnly(DBNull.Value));
            Assert.Null(ValueUtilities.CTimeOnly(DBNull.Value));
        }

        [Fact]
        [DisplayName("The default-value overloads of the temporal family return the given default")]
        public void TemporalFamily_DefaultOverloads_ReturnTheGivenDefault()
        {
            var date = new DateOnly(2000, 1, 1);
            var time = new TimeOnly(8, 30);
            Assert.Equal(date, ValueUtilities.CDateOnly(DBNull.Value, date));
            Assert.Equal(time, ValueUtilities.CTimeOnly(DBNull.Value, time));
        }

        [Fact]
        [DisplayName("CDateTime returns defaultValue for a numeric string that is not a valid calendar date")]
        public void CDateTime_InvalidCalendarDate_FallsBackToDefault()
        {
            // `20150230` becomes "2015-02-30", which `Convert.ToDateTime` rejects. The exception is caught and defaultValue is returned.
            var fallback = new DateTime(2000, 1, 1);
            var result = ValueUtilities.CDateTime("20150230", fallback);
            Assert.Equal(fallback, result);
        }

        [Fact]
        [DisplayName("CDateOnly drops the time of day and returns a DateOnly")]
        public void CDate_ReturnsDatePortionOnly()
        {
            var input = new DateTime(2026, 4, 18, 15, 30, 45, DateTimeKind.Unspecified);
            var result = ValueUtilities.CDateOnly(input);
            Assert.Equal(new DateOnly(2026, 4, 18), result);
        }

        [Fact]
        [DisplayName("CDateOnly parses a date string into a DateOnly")]
        public void CDate_ParsesDateString()
        {
            var result = ValueUtilities.CDateOnly("20150312");
            Assert.Equal(new DateOnly(2015, 3, 12), result);
        }

        [Fact]
        [DisplayName("CDateOnly returns the given DateOnly default for an unconvertible value")]
        public void CDate_UnparsableValue_ReturnsDefault()
        {
            var fallback = new DateOnly(2026, 1, 1);
            Assert.Equal(fallback, ValueUtilities.CDateOnly(DBNull.Value, fallback));
            Assert.Equal(fallback, ValueUtilities.CDateOnly(string.Empty, fallback));
        }

        [Fact]
        [DisplayName("CDateTime accepts a DateOnly directly instead of taking the culture-dependent string path")]
        public void CDateTime_AcceptsDateOnly()
        {
            // `DateOnly.ToString()` formats with the current culture, and parsing that back with the invariant
            // culture only works when the two happen to agree. So it must be handled before the string path.
            var result = ValueUtilities.CDateTime(new DateOnly(2026, 7, 25));
            Assert.Equal(new DateTime(2026, 7, 25, 0, 0, 0, DateTimeKind.Unspecified), result);
        }

        // ---- CGuid ----

        [Fact]
        [DisplayName("CGuid(string) returns the Guid for a valid string, Guid.Empty for an empty string, and throws for an invalid string")]
        public void CGuid_String_BehavesAsExpected()
        {
            var guid = Guid.NewGuid();
            Assert.Equal(guid, ValueUtilities.CGuid(guid.ToString()));
            Assert.Equal(Guid.Empty, ValueUtilities.CGuid(string.Empty));
            Assert.Equal(Guid.Empty, ValueUtilities.CGuid(null!));
            Assert.Throws<FormatException>(() => ValueUtilities.CGuid("not-a-guid"));
        }

        [Fact]
        [DisplayName("CGuid(object) returns Guid.Empty for null, DBNull and non-string values, and the Guid for a Guid or a valid string")]
        public void CGuid_Object_BehavesAsExpected()
        {
            var guid = Guid.NewGuid();
            Assert.Equal(guid, ValueUtilities.CGuid((object)guid));
            Assert.Equal(guid, ValueUtilities.CGuid((object)guid.ToString()));
            Assert.Equal(Guid.Empty, ValueUtilities.CGuid((object)null!));
            Assert.Equal(Guid.Empty, ValueUtilities.CGuid((object)DBNull.Value));
            Assert.Equal(Guid.Empty, ValueUtilities.CGuid((object)123));
        }
    }
}
