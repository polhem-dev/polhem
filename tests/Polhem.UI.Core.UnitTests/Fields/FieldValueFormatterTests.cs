using System.ComponentModel;
using Polhem.Tests.Shared;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Core.UnitTests.Fields
{
    /// <summary>
    /// Tests that <see cref="FieldValueFormatter"/> renders numbers and dates in the user's culture.
    /// </summary>
    public class FieldValueFormatterTests
    {
        [Fact]
        [DisplayName("A number format renders with the user's culture separators")]
        public void Format_NumberFormat_UsesCurrentCulture()
        {
            using (new CultureScope("de-DE"))
            {
                Assert.Equal("1.234,50", FieldValueFormatter.Format(1234.5m, string.Empty, "N2"));
            }
            using (new CultureScope("en-US"))
            {
                Assert.Equal("1,234.50", FieldValueFormatter.Format(1234.5m, string.Empty, "N2"));
            }
        }

        [Fact]
        [DisplayName("A date renders with the user's culture short date pattern")]
        public void Format_Date_UsesCultureShortDate()
        {
            var date = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Unspecified);
            using (new CultureScope("de-DE"))
            {
                Assert.Equal("15.01.2026", FieldValueFormatter.Format(date, string.Empty, string.Empty));
            }
            using (new CultureScope("en-US"))
            {
                Assert.Equal("1/15/2026", FieldValueFormatter.Format(date, string.Empty, string.Empty));
            }
        }

        [Fact]
        [DisplayName("An explicit display format wins over the number format")]
        public void Format_DisplayFormat_WinsOverNumberFormat()
        {
            using var _ = new CultureScope("en-US");

            Assert.Equal("1234.500", FieldValueFormatter.Format(1234.5m, "0.000", "N2"));
        }

        [Fact]
        [DisplayName("A null or DBNull value renders as an empty string")]
        public void Format_NullOrDBNull_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, FieldValueFormatter.Format(null, string.Empty, string.Empty));
            Assert.Equal(string.Empty, FieldValueFormatter.Format(DBNull.Value, string.Empty, "N2"));
        }
    }
}
