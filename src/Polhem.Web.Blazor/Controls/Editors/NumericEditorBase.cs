using Microsoft.AspNetCore.Components;
using Polhem.Definition;
using Polhem.Definition.Settings;
using Polhem.UI.Core.Fields;

namespace Polhem.Web.Blazor.Controls.Editors
{
    /// <summary>
    /// Base component of a numeric editor: shows the value formatted per the field's number format at rest, shows the
    /// full-precision value while focused for editing, and writes the parsed value back at full precision. Partial or
    /// invalid input (for example <c>"12."</c>) keeps the last valid value.
    /// </summary>
    /// <remarks>
    /// Display, input and format resolution are <see cref="NumericFieldFormat"/>, which every UI head shares: the
    /// user's culture for separators, a sign and the decimal separator only on input, and the invariant form for the
    /// bound value. A UI package calls <see cref="BeginEditing"/> and <see cref="EndEditing"/> from the input's focus
    /// and blur.
    /// </remarks>
    public abstract class NumericEditorBase : TextEditorBase
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

        /// <summary>
        /// Switches the text to the full-precision editing form, when the editor accepts input. Call it when the input
        /// takes the focus.
        /// </summary>
        protected void BeginEditing()
        {
            if (!IsEditable) return;
            _focused = true;
            Text = NumericFieldFormat.FormatForEditing(_rawValue);
        }

        /// <summary>
        /// Returns the text to the display format. Call it when the input loses the focus.
        /// </summary>
        /// <remarks>
        /// The browser reports <c>change</c> before <c>blur</c>, so a commit is written while the editing form is still
        /// shown, and the display format returns here.
        /// </remarks>
        protected void EndEditing()
        {
            _focused = false;
            Text = NumericFieldFormat.FormatForDisplay(_rawValue, NumberFormat);
        }
    }
}
