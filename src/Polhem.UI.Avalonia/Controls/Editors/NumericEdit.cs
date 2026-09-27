using System.Globalization;
using Avalonia.Media;
using Polhem.Base;
using Polhem.Definition;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;

namespace Polhem.UI.Avalonia.Controls.Editors
{
    /// <summary>
    /// Field editor for <see cref="ControlType.NumericEdit"/>: a right-aligned numeric input that
    /// shows the value formatted per the field's <c>NumberFormat</c> at rest, reveals the raw
    /// full-precision value while focused for editing, and writes the parsed value back at full
    /// precision — the rounded display form is never written back. Partial or invalid input (for
    /// example <c>"12."</c>) keeps the last valid value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Display and input follow <see cref="CultureInfo.CurrentCulture"/>, the signed-in user's
    /// culture: a <c>de-DE</c> user sees and types <c>1,5</c>, an <c>en-US</c> user <c>1.5</c>. The
    /// bound value stays in the invariant form the wire carries, whatever the user's culture.
    /// </para>
    /// <para>
    /// Input accepts a sign and the culture's decimal separator only, not group separators. A group
    /// separator is the other culture's decimal separator, so accepting it would read a <c>de-DE</c>
    /// user's <c>1,5</c> as fifteen under <c>en-US</c> rules; rejecting it keeps the last valid value
    /// instead of silently writing a different number.
    /// </para>
    /// </remarks>
    public sealed class NumericEdit : TextEdit
    {
        // The bound value in its raw, full-precision invariant-culture string form. The display
        // Text may be a rounded rendering of this; write-backs always use the raw value.
        private string _rawValue = string.Empty;

        private const NumberStyles InputStyles =
            NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite
            | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

        /// <summary>
        /// Initializes a new instance of <see cref="NumericEdit"/>.
        /// </summary>
        public NumericEdit()
        {
            TextAlignment = TextAlignment.Right;
            // Reveal full precision for editing; the display format only applies at rest.
            GotFocus += (_, _) =>
            {
                Text = FormatForEditing(_rawValue);
                SelectAll();
            };
            // Subscribed after the base TextEdit commit handler (registered in its constructor), so
            // this runs once the bound value is already written and can re-apply the display format.
            LostFocus += (_, _) => Text = FormatForDisplay(_rawValue);
        }

        /// <summary>
        /// Gets or sets the client currency master for runtime decimal resolution of
        /// <see cref="NumberKind.Amount"/> fields. When <c>null</c> (the default), the editor uses the
        /// delivered/baked format — currency awareness is off, so existing non-amount behaviour is
        /// unchanged. Hosts set this (with <see cref="DefaultCurrencyCode"/>) to format amounts by the
        /// bound row's currency.
        /// </summary>
        public CurrencySettings? CurrencySettings { get; set; }

        /// <summary>
        /// Gets or sets the fallback currency code used when the bound row carries no currency-key field
        /// value (the master document / company default currency).
        /// </summary>
        public string DefaultCurrencyCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the client unit-of-measure master for runtime decimal resolution of
        /// <see cref="NumberKind.Quantity"/> / <see cref="NumberKind.Weight"/> fields that bind a unit
        /// field. When <c>null</c>, the delivered/baked format is used — unit awareness is off.
        /// </summary>
        public UnitSettings? UnitSettings { get; set; }

        /// <summary>
        /// Gets or sets the fallback unit code used when the bound row carries no unit-field value
        /// (host-driven, for the single-record form where the row's unit is known to the host).
        /// </summary>
        public string DefaultUnitCode { get; set; } = string.Empty;

        // Reference-bound fields (amounts by currency, quantities/weights by unit) are not baked at
        // delivery — when the matching master is supplied, resolve the format at runtime from the bound
        // row's reference value. Otherwise prefer the delivered layout format (baked per company),
        // falling back to the schema field's format for ambient (field-name-only) binds.
        private string NumberFormat
        {
            get
            {
                var layoutField = Binder.LayoutField;
                if (layoutField is not null)
                {
                    var source = NumberKindProfile.GetDecimalsSource(layoutField.NumberKind);
                    if (source == DecimalsSource.Currency && CurrencySettings is not null)
                        return NumberFormatResolver.ResolveFormat(
                            layoutField.NumberKind,
                            new RoundingContext { CurrencySettings = CurrencySettings },
                            ResolveReferenceCode(layoutField.CurrencyField, DefaultCurrencyCode));

                    if (source == DecimalsSource.Unit && UnitSettings is not null)
                    {
                        string code = ResolveReferenceCode(layoutField.UnitField, DefaultUnitCode);
                        if (StringUtilities.IsNotEmpty(code))
                            return NumberFormatResolver.ResolveFormat(
                                layoutField.NumberKind, new RoundingContext { UnitSettings = UnitSettings }, code);
                    }
                }

                return layoutField?.NumberFormat is { Length: > 0 } layoutFormat
                    ? layoutFormat
                    : Binder.FormField?.NumberFormat ?? string.Empty;
            }
        }

        // Per-row reference code: the field's reference field (currency key / unit) value on the bound
        // row → the supplied fallback.
        private string ResolveReferenceCode(string referenceField, string fallback)
        {
            var row = Binder.TargetRow;
            if (row is not null
                && StringUtilities.IsNotEmpty(referenceField)
                && row.Table.Columns.Contains(referenceField))
            {
                string code = ValueUtilities.CStr(row[referenceField]);
                if (StringUtilities.IsNotEmpty(code)) { return code; }
            }
            return fallback;
        }

        /// <inheritdoc />
        protected override void RefreshFromSource()
        {
            _rawValue = Binder.GetValue();
            Text = IsFocused ? FormatForEditing(_rawValue) : FormatForDisplay(_rawValue);
        }

        /// <inheritdoc />
        protected override string? GetWriteBackValue()
        {
            // Write the parsed value at full precision; reject partial/invalid input by keeping the
            // last valid raw value so a stray keystroke never corrupts the bound field.
            if (TryParseInput(Text, out var value))
                _rawValue = value.ToString(CultureInfo.InvariantCulture);
            return _rawValue;
        }

        private string FormatForDisplay(string raw)
        {
            if (!TryParseRaw(raw, out var value))
                return raw;
            return string.IsNullOrEmpty(NumberFormat)
                ? FormatForEditing(raw)
                : CellValueFormatter.Format(value, string.Empty, NumberFormat);
        }

        // The full-precision value in the user's culture, without group separators, so what the
        // field shows while focused parses back to the same number.
        private static string FormatForEditing(string raw)
            => TryParseRaw(raw, out var value) ? value.ToString(CultureInfo.CurrentCulture) : raw;

        // The bound value: the invariant form the wire carries.
        private static bool TryParseRaw(string? raw, out decimal value)
            => decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value);

        // What the user typed: the user's culture, sign and decimal separator only.
        internal static bool TryParseInput(string? text, out decimal value)
            => decimal.TryParse(text, InputStyles, CultureInfo.CurrentCulture, out value);
    }
}
