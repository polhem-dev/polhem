using System.ComponentModel;
using System.Globalization;
using Polhem.Tests.Shared;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Core.UnitTests.Fields
{
    /// <summary>
    /// Tests the date-and-time field rules of <see cref="DateTimeFieldFormat"/>.
    /// </summary>
    public class DateTimeFieldFormatTests
    {
        [Fact]
        [DisplayName("The binding value keeps the time of day even at midnight")]
        public void ToBindingValue_Midnight_KeepsTime()
        {
            var value = new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Unspecified);

            Assert.Equal("2026-03-04T00:00:00", DateTimeFieldFormat.ToBindingValue(value));
        }

        [Fact]
        [DisplayName("TryParseInput accepts the user's culture and the invariant ISO form")]
        public void TryParseInput_CultureAndIso_BothParse()
        {
            using var _ = new CultureScope("de-DE");

            Assert.True(DateTimeFieldFormat.TryParseInput("04.03.2026 13:05:00", out var local));
            Assert.Equal(new DateTime(2026, 3, 4, 13, 5, 0, DateTimeKind.Unspecified), local);
            Assert.True(DateTimeFieldFormat.TryParseInput("2026-03-04 13:05", out var iso));
            Assert.Equal(new DateTime(2026, 3, 4, 13, 5, 0, DateTimeKind.Unspecified), iso);
            Assert.Equal(DateTimeKind.Unspecified, iso.Kind);
        }

        [Fact]
        [DisplayName("TryParseInput rejects empty and unparseable text")]
        public void TryParseInput_EmptyOrInvalid_ReturnsFalse()
        {
            Assert.False(DateTimeFieldFormat.TryParseInput("  ", out _));
            Assert.False(DateTimeFieldFormat.TryParseInput("not a date", out _));
        }

        [Fact]
        [DisplayName("FormatBindingValue renders the culture's general format and empties an invalid value")]
        public void FormatBindingValue_ValidAndInvalid()
        {
            using var _ = new CultureScope("en-US");

            // ICU puts a narrow no-break space before the AM/PM designator, so the expected text is built
            // from the culture rather than written as a literal.
            var expected = new DateTime(2026, 3, 4, 13, 5, 0, DateTimeKind.Unspecified).ToString("G", CultureInfo.CurrentCulture);
            Assert.Equal(expected, DateTimeFieldFormat.FormatBindingValue("2026-03-04T13:05:00"));
            Assert.Equal(string.Empty, DateTimeFieldFormat.FormatBindingValue("garbage"));
        }
    }
}
