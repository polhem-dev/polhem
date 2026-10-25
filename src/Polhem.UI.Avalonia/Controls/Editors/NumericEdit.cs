using System.Globalization;
using Avalonia.Media;
using Polhem.Definition;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.UI.Core.Fields;

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

        private string NumberFormat
            => NumericFieldFormat.ResolveFormat(
                Binder.LayoutField, Binder.FormField, Binder.TargetRow,
                CurrencySettings, DefaultCurrencyCode, UnitSettings, DefaultUnitCode);

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
            _rawValue = NumericFieldFormat.ToWriteBackValue(Text, _rawValue);
            return _rawValue;
        }

        private string FormatForDisplay(string raw) => NumericFieldFormat.FormatForDisplay(raw, NumberFormat);

        private static string FormatForEditing(string raw) => NumericFieldFormat.FormatForEditing(raw);
    }
}
