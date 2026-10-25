using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Tests.Shared;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Core.UnitTests.Fields
{
    /// <summary>
    /// Tests the numeric field rules of <see cref="NumericFieldFormat"/>.
    /// </summary>
    public class NumericFieldFormatTests
    {
        [Theory]
        [InlineData("de-DE", "-1,25", -1.25)]
        [InlineData("en-US", "-1.25", -1.25)]
        [InlineData("fr-FR", " 3,5 ", 3.5)]
        [DisplayName("TryParseInput reads a sign and the culture's decimal separator")]
        public void TryParseInput_UserCulture_ParsesDecimalSeparator(string culture, string input, double expected)
        {
            using var _ = new CultureScope(culture);

            Assert.True(NumericFieldFormat.TryParseInput(input, out decimal value));
            Assert.Equal((decimal)expected, value);
        }

        [Fact]
        [DisplayName("TryParseInput rejects a group separator")]
        public void TryParseInput_GroupSeparator_ReturnsFalse()
        {
            using var scope = new CultureScope("en-US");

            Assert.False(NumericFieldFormat.TryParseInput("1,5", out _));
        }

        [Fact]
        [DisplayName("ToWriteBackValue keeps the last value when the input does not parse")]
        public void ToWriteBackValue_InvalidInput_KeepsLastValue()
        {
            using var _ = new CultureScope("en-US");

            Assert.Equal("12.5", NumericFieldFormat.ToWriteBackValue("12.x", "12.5"));
        }

        [Fact]
        [DisplayName("ToWriteBackValue writes the parsed input at full precision in the invariant culture")]
        public void ToWriteBackValue_ValidInput_WritesInvariantFullPrecision()
        {
            using var _ = new CultureScope("de-DE");

            Assert.Equal("1.23456", NumericFieldFormat.ToWriteBackValue("1,23456", "0"));
        }

        [Fact]
        [DisplayName("FormatForDisplay applies the number format and FormatForEditing keeps full precision")]
        public void FormatForDisplay_AndForEditing_DifferInPrecision()
        {
            using var _ = new CultureScope("en-US");

            Assert.Equal("1,234.57", NumericFieldFormat.FormatForDisplay("1234.567", "N2"));
            Assert.Equal("1234.567", NumericFieldFormat.FormatForEditing("1234.567"));
        }

        [Fact]
        [DisplayName("ResolveFormat prefers the layout format and falls back to the schema format")]
        public void ResolveFormat_NoMasters_LayoutThenSchemaFormat()
        {
            var formField = new FormField { FieldName = "amount", NumberFormat = "N4" };

            Assert.Equal("N1", NumericFieldFormat.ResolveFormat(
                new LayoutField { FieldName = "amount", NumberFormat = "N1" }, formField, null, null, string.Empty, null, string.Empty));
            Assert.Equal("N4", NumericFieldFormat.ResolveFormat(
                null, formField, null, null, string.Empty, null, string.Empty));
            Assert.Equal(string.Empty, NumericFieldFormat.ResolveFormat(
                null, null, null, null, string.Empty, null, string.Empty));
        }

        [Fact]
        [DisplayName("ResolveFormat ignores the currency path when no currency master is supplied")]
        public void ResolveFormat_AmountWithoutCurrencyMaster_UsesLayoutFormat()
        {
            var layoutField = new LayoutField { FieldName = "amount", NumberKind = NumberKind.Amount, NumberFormat = "N2" };

            Assert.Equal("N2", NumericFieldFormat.ResolveFormat(
                layoutField, null, null, null, "USD", null, string.Empty));
        }
    }
}
