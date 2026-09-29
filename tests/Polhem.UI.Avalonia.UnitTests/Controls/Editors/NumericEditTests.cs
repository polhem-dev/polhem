using System.ComponentModel;
using Avalonia.Input;
using Avalonia.Media;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;
using Polhem.Tests.Shared;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Behaviour checks for <see cref="NumericEdit"/>: formatted display at rest, full-precision
    /// write-back (the rounded display form is never persisted), tolerance of invalid input, and
    /// right alignment.
    /// </summary>
    public class NumericEditTests
    {
        private static FormDataObject BuildDataObject()
        {
            var schema = new FormSchema("Order", "Order");
            var master = schema.Tables!.Add("Order", "Order");
            master.Fields!.Add("amount", "Amount", FieldDbType.Decimal);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        private static LayoutField AmountField(string numberFormat = "N2")
            => new() { FieldName = "amount", NumberFormat = numberFormat };

        // Commit is exercised via Enter (a KeyDown routed event), matching TextEditTests; the
        // LostFocus routed event carries a FocusChangedEventArgs that cannot be synthesised here.
        private static void Commit(NumericEdit editor)
            => editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

        [Fact]
        [DisplayName("After Bind the value is displayed formatted by NumberFormat (N2 gives two decimals)")]
        public void Bind_DecimalField_DisplaysFormatted()
        {
            var dataObject = BuildDataObject();
            dataObject.SetField("amount", "12.3456");

            var editor = new NumericEdit();
            editor.Bind(dataObject, AmountField("N2"));

            Assert.Equal("12.35", editor.Text);
        }

        [Fact]
        [DisplayName("The write-back is the parsed full-precision value, not the rounded display value")]
        public void WriteBack_StoresFullPrecision_NotRoundedDisplay()
        {
            var dataObject = BuildDataObject();
            var editor = new NumericEdit();
            editor.Bind(dataObject, AmountField("N2"));

            // Simulate the user typing a higher-precision value than the display shows.
            editor.Text = "12.3456789";
            Commit(editor);

            // The bound field keeps full precision; the display rounding is never written back.
            Assert.Equal("12.3456789", dataObject.GetField("amount"));
        }

        [Fact]
        [DisplayName("Enter commits the write-back")]
        public void EnterKey_WritesBack()
        {
            var dataObject = BuildDataObject();
            var editor = new NumericEdit();
            editor.Bind(dataObject, AmountField("N2"));

            editor.Text = "50";
            Commit(editor);

            Assert.Equal("50", dataObject.GetField("amount"));
        }

        [Fact]
        [DisplayName("Invalid input keeps the last valid value and is not written back")]
        public void InvalidInput_KeepsPreviousValue()
        {
            var dataObject = BuildDataObject();
            dataObject.SetField("amount", "42.5");
            var editor = new NumericEdit();
            editor.Bind(dataObject, AmountField("N2"));

            editor.Text = "not-a-number";
            Commit(editor);

            // The invalid text is rejected: the field keeps its previous valid value.
            Assert.Equal("42.5", dataObject.GetField("amount"));
        }

        [Fact]
        [DisplayName("Without NumberFormat the raw value is displayed (no formatting)")]
        public void Bind_NoNumberFormat_ShowsRawValue()
        {
            var dataObject = BuildDataObject();
            dataObject.SetField("amount", "7.5");
            var editor = new NumericEdit();
            editor.Bind(dataObject, AmountField(numberFormat: string.Empty));

            Assert.Equal("7.5", editor.Text);
        }

        [Fact]
        [DisplayName("NumericEdit is right-aligned")]
        public void NumericEdit_IsRightAligned()
        {
            var editor = new NumericEdit();

            Assert.Equal(TextAlignment.Right, editor.TextAlignment);
        }

        // --- Multi-currency runtime resolution ---

        private static CurrencySettings Currencies() =>
        [
            new CurrencyItem("USD", 0.01m, "$", "US Dollar"),
            new CurrencyItem("JPY", 1m, "¥", "Japanese Yen"),
        ];

        private static LayoutField AmountKindField()
            => new() { FieldName = "amount", NumberKind = NumberKind.Amount };

        [Fact]
        [DisplayName("With CurrencySettings and a default currency, the amount field shows the currency's decimals (JPY, 0 decimals)")]
        public void Amount_WithCurrency_FormatsByDefaultCurrency_Jpy()
        {
            var dataObject = BuildDataObject();
            dataObject.SetField("amount", "1234.567");
            var editor = new NumericEdit { CurrencySettings = Currencies(), DefaultCurrencyCode = "JPY" };

            editor.Bind(dataObject, AmountKindField());

            Assert.Equal("1,235", editor.Text); // JPY: 0 decimals.
        }

        [Fact]
        [DisplayName("The same data with USD as the currency shows the amount field with 2 decimals")]
        public void Amount_WithCurrency_FormatsByDefaultCurrency_Usd()
        {
            var dataObject = BuildDataObject();
            dataObject.SetField("amount", "1234.567");
            var editor = new NumericEdit { CurrencySettings = Currencies(), DefaultCurrencyCode = "USD" };

            editor.Bind(dataObject, AmountKindField());

            Assert.Equal("1,234.57", editor.Text); // USD: 2 decimals.
        }

        [Fact]
        [DisplayName("Without CurrencySettings the amount field does no currency resolution (no baked format, so the raw value shows)")]
        public void Amount_NoCurrencySettings_ShowsRaw()
        {
            var dataObject = BuildDataObject();
            dataObject.SetField("amount", "1234.567");
            var editor = new NumericEdit(); // No CurrencySettings.

            editor.Bind(dataObject, AmountKindField());

            Assert.Equal("1234.567", editor.Text);
        }

        // --- Unit of measure runtime resolution ---

        private static UnitSettings Units() =>
        [
            new UnitItem("PCS", 0, "count", "Pieces"),
            new UnitItem("KG", 3, "weight", "Kilogram"),
        ];

        private static FormDataObject BuildQtyDataObject()
        {
            var schema = new FormSchema("Order", "Order");
            var master = schema.Tables!.Add("Order", "Order");
            master.Fields!.Add("qty", "Qty", FieldDbType.Decimal);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        private static LayoutField QtyKindField()
            => new() { FieldName = "qty", NumberKind = NumberKind.Quantity, UnitField = "qty_uom" };

        [Fact]
        [DisplayName("With UnitSettings and KG as the default unit, the quantity field shows 3 decimals")]
        public void Quantity_WithUnit_FormatsByDefaultUnit_Kg()
        {
            var dataObject = BuildQtyDataObject();
            dataObject.SetField("qty", "12.345");
            var editor = new NumericEdit { UnitSettings = Units(), DefaultUnitCode = "KG" };

            editor.Bind(dataObject, QtyKindField());

            Assert.Equal("12.345", editor.Text); // KG: 3 decimals.
        }

        [Fact]
        [DisplayName("The same data with PCS as the unit shows the quantity field with 0 decimals")]
        public void Quantity_WithUnit_FormatsByDefaultUnit_Pcs()
        {
            var dataObject = BuildQtyDataObject();
            dataObject.SetField("qty", "12.345");
            var editor = new NumericEdit { UnitSettings = Units(), DefaultUnitCode = "PCS" };

            editor.Bind(dataObject, QtyKindField());

            Assert.Equal("12", editor.Text); // PCS: 0 decimals.
        }

        [Fact]
        [DisplayName("Without UnitSettings the quantity field does no unit resolution (no baked format, so the raw value shows)")]
        public void Quantity_NoUnitSettings_ShowsRaw()
        {
            var dataObject = BuildQtyDataObject();
            dataObject.SetField("qty", "12.345");
            var editor = new NumericEdit(); // No UnitSettings.

            editor.Bind(dataObject, QtyKindField());

            Assert.Equal("12.345", editor.Text);
        }

        [Fact]
        [DisplayName("Under de-DE the value displays with a comma decimal separator and a dot group separator")]
        public void Bind_CommaDecimalCulture_DisplaysInUserCulture()
        {
            using var _ = new CultureScope("de-DE");
            var dataObject = BuildDataObject();
            dataObject.SetField("amount", "1234.5");

            var editor = new NumericEdit();
            editor.Bind(dataObject, AmountField("N2"));

            Assert.Equal("1.234,50", editor.Text);
        }

        [Fact]
        [DisplayName("Under de-DE typing 1,5 writes 1.5 to the bound value, not 15")]
        public void WriteBack_CommaDecimalCulture_ParsesUserInput()
        {
            using var _ = new CultureScope("de-DE");
            var dataObject = BuildDataObject();
            var editor = new NumericEdit();
            editor.Bind(dataObject, AmountField("N2"));

            editor.Text = "1,5";
            Commit(editor);

            Assert.Equal("1.5", dataObject.GetField("amount"));
        }

        [Fact]
        [DisplayName("Under en-US a group separator is rejected, so 1,5 keeps the last valid value instead of writing 15")]
        public void WriteBack_GroupSeparatorInput_KeepsLastValidValue()
        {
            using var _ = new CultureScope("en-US");
            var dataObject = BuildDataObject();
            dataObject.SetField("amount", "7");
            var editor = new NumericEdit();
            editor.Bind(dataObject, AmountField("N2"));

            editor.Text = "1,5";
            Commit(editor);

            Assert.Equal("7", dataObject.GetField("amount"));
        }

        [Theory]
        [InlineData("de-DE", "-1,25", -1.25)]
        [InlineData("en-US", "-1.25", -1.25)]
        [InlineData("fr-FR", " 3,5 ", 3.5)]
        [DisplayName("TryParseInput reads a sign and the culture's decimal separator")]
        public void TryParseInput_UserCulture_ParsesDecimalSeparator(string culture, string input, double expected)
        {
            using var _ = new CultureScope(culture);

            Assert.True(NumericEdit.TryParseInput(input, out decimal value));
            Assert.Equal((decimal)expected, value);
        }
    }
}
