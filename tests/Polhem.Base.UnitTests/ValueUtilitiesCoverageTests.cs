using System.ComponentModel;

namespace Polhem.Base.UnitTests
{
    /// <summary>
    /// Coverage tests for ValueUtilities: the IsEmpty(DateTime) boundary, the enum/ToString branches of CStr,
    /// and the OverflowException catch branches of CInt / CDecimal (a value outside the target type's range returns defaultValue).
    /// </summary>
    public class ValueUtilitiesCoverageTests
    {
        // ---- IsEmpty(DateTime) boundary (line 76) ----

        [Theory]
        [InlineData(1752, 12, 31, true)]  // before 1753 → empty
        [InlineData(1753, 1, 1, false)]   // boundary value → not empty
        [InlineData(2026, 7, 11, false)]  // ordinary date → not empty
        [DisplayName("IsEmpty(DateTime) returns true before 1753 and false from 1753 on")]
        public void IsEmpty_DateTimeBoundary_ReturnsExpectedResult(int y, int m, int d, bool expected)
        {
            var value = new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Unspecified);
            Assert.Equal(expected, ValueUtilities.IsEmpty(value));
        }

        [Fact]
        [DisplayName("IsEmpty(DateTime) returns true for MinValue")]
        public void IsEmpty_DateTimeMinValue_ReturnsTrue()
        {
            Assert.True(ValueUtilities.IsEmpty(DateTime.MinValue));
        }

        // ---- CStr enum / ToString branches (line 129 / 131) ----

        [Fact]
        [DisplayName("CStr returns the name of an enum and the ToString result of other objects")]
        public void CStr_EnumAndObject_ReturnsExpectedString()
        {
            Assert.Equal("Hour", ValueUtilities.CStr(DateInterval.Hour));
            Assert.Equal("42", ValueUtilities.CStr(42));
            Assert.Equal("3.5", ValueUtilities.CStr(3.5));
        }

        // ---- CInt OverflowException catch (line 309/311) ----

        [Fact]
        [DisplayName("CInt returns defaultValue for a value outside the int range (OverflowException branch)")]
        public void CInt_ValueOverflowsInt_ReturnsDefault()
        {
            // `1e20` is a double, so `Convert.ToInt32` throws `OverflowException`, which is caught and yields defaultValue.
            Assert.Equal(0, ValueUtilities.CInt(1e20));
            Assert.Equal(-9, ValueUtilities.CInt(1e20, -9));
        }

        // ---- CDecimal OverflowException catch (line 363/365) ----

        [Fact]
        [DisplayName("CDecimal returns defaultValue for a value outside the decimal range (OverflowException branch)")]
        public void CDecimal_ValueOverflowsDecimal_ReturnsDefault()
        {
            // `1e30` exceeds the decimal maximum (about 7.9e28), so `Convert.ToDecimal` throws `OverflowException`, which is caught.
            Assert.Equal(0m, ValueUtilities.CDecimal(1e30));
            Assert.Equal(-1m, ValueUtilities.CDecimal(1e30, -1m));
        }

        // ---- CDouble / CDecimal normal conversion path (line 326 / 353) ----

        [Fact]
        [DisplayName("CDouble returns the matching double for valid numeric input")]
        public void CDouble_ValidInput_ReturnsConvertedValue()
        {
            Assert.Equal(123.45d, ValueUtilities.CDouble("123.45"));
            Assert.Equal(7d, ValueUtilities.CDouble(7));
            Assert.Equal(1d, ValueUtilities.CDouble(true));
        }

        [Fact]
        [DisplayName("CDecimal returns the matching decimal for valid numeric input")]
        public void CDecimal_ValidInput_ReturnsConvertedValue()
        {
            Assert.Equal(123.45m, ValueUtilities.CDecimal("123.45"));
            Assert.Equal(7m, ValueUtilities.CDecimal(7));
            Assert.Equal(1m, ValueUtilities.CDecimal(true));
        }
    }
}
