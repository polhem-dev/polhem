using Microsoft.AspNetCore.Components;
using Polhem.Definition;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.UI.Core.Fields;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// Field editor for <see cref="ControlType.NumericEdit"/>: a right-aligned numeric input that shows
    /// the value formatted per the field's number format at rest, shows the full-precision value while
    /// focused for editing, and writes the parsed value back at full precision. Partial or invalid input
    /// (for example <c>"12."</c>) keeps the last valid value.
    /// </summary>
    /// <remarks>
    /// Display, input and format resolution are <see cref="NumericFieldFormat"/>, shared with the
    /// Avalonia head's <c>NumericEdit</c>: the user's culture for separators, a sign and the decimal
    /// separator only on input, and the invariant form for the bound value.
    /// </remarks>
    public sealed partial class NumericEdit : TextEdit
    {
        // The bound value in its raw, full-precision invariant-culture string form. The display
        // Text may be a rounded rendering of this; write-backs always use the raw value.
        private string _rawValue = string.Empty;
        private bool _focused;

        /// <summary>
        /// Gets or sets the currency master for runtime decimal resolution of
        /// <see cref="NumberKind.Amount"/> fields. When <c>null</c> (the default), the delivered format is used.
        /// </summary>
        [Parameter]
        public CurrencySettings? CurrencySettings { get; set; }

        /// <summary>
        /// Gets or sets the currency code used when the bound row carries no currency-key field value.
        /// </summary>
        [Parameter]
        public string DefaultCurrencyCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unit-of-measure master for runtime decimal resolution of quantity and weight
        /// fields that bind a unit field. When <c>null</c>, the delivered format is used.
        /// </summary>
        [Parameter]
        public UnitSettings? UnitSettings { get; set; }

        /// <summary>
        /// Gets or sets the unit code used when the bound row carries no unit-field value.
        /// </summary>
        [Parameter]
        public string DefaultUnitCode { get; set; } = string.Empty;

        /// <inheritdoc />
        protected override string ModifierClass => "polhem-field-editor--numeric";

        /// <inheritdoc />
        protected override string? InputMode => "decimal";

        private string NumberFormat
            => NumericFieldFormat.ResolveFormat(
                BoundLayoutField, FormField, TargetRow,
                CurrencySettings, DefaultCurrencyCode, UnitSettings, DefaultUnitCode);

        /// <inheritdoc />
        protected override void RefreshFromSource()
        {
            _rawValue = GetValue();
            Text = _focused ? NumericFieldFormat.FormatForEditing(_rawValue) : NumericFieldFormat.FormatForDisplay(_rawValue, NumberFormat);
        }

        /// <inheritdoc />
        protected override string? GetWriteBackValue()
        {
            _rawValue = NumericFieldFormat.ToWriteBackValue(Text, _rawValue);
            return _rawValue;
        }

        // The browser reports `change` before `blur`, so a commit is written while the editing form is
        // still shown, and the display format returns on blur.
        private void OnFocus()
        {
            if (!IsEditable) return;
            _focused = true;
            Text = NumericFieldFormat.FormatForEditing(_rawValue);
        }

        private void OnBlur()
        {
            _focused = false;
            Text = NumericFieldFormat.FormatForDisplay(_rawValue, NumberFormat);
        }
    }
}
