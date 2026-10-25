using System.Globalization;
using Microsoft.AspNetCore.Components;
using Polhem.Definition;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.Controls.Editors;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.Controls
{
    /// <summary>
    /// Renders the master sections of a single-record form from a <see cref="FormLayout"/>, one field
    /// editor per <see cref="LayoutField"/>, chosen by <see cref="FieldEditorFactory"/> from its
    /// <see cref="ControlType"/>. The counterpart of the Avalonia head's <c>FormLayoutPresenter</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The editors bind through a <see cref="FormScope"/> carrying <see cref="DataObject"/> and
    /// <see cref="FormMode"/>. Without a data object each editor still follows its layout field's
    /// <see cref="LayoutFieldBase.ReadOnly"/> and <see cref="LayoutField.AllowEditModes"/>, so the presenter
    /// shows what a layout looks like without data.
    /// </para>
    /// <para>
    /// Only the master sections are rendered. The layout's detail grids (<see cref="FormLayout.Details"/>)
    /// are not, so detail rows cannot be viewed or edited through this component yet.
    /// </para>
    /// </remarks>
    public sealed partial class FormLayoutPresenter : ComponentBase
    {
        /// <summary>
        /// Gets or sets the form layout to render.
        /// </summary>
        [Parameter]
        public FormLayout? Layout { get; set; }

        /// <summary>
        /// Gets or sets the data object the editors bind to, or <c>null</c> to render the layout without data.
        /// </summary>
        [Parameter]
        public FormDataObject? DataObject { get; set; }

        /// <summary>
        /// Gets or sets the form mode that decides which editors accept input. Defaults to
        /// <see cref="SingleFormMode.Edit"/>.
        /// </summary>
        [Parameter]
        public SingleFormMode FormMode { get; set; } = SingleFormMode.Edit;

        /// <summary>
        /// Gets or sets the currency and unit masters and the company that decide the decimals of numeric
        /// fields bound to a currency or a unit, or <c>null</c> to use the delivered formats.
        /// </summary>
        [Parameter]
        public RoundingContext? RoundingContext { get; set; }

        /// <summary>
        /// Gets or sets the id prefix applied to every rendered input. Pages that embed several presenters
        /// set distinct prefixes so the DOM ids stay unique.
        /// </summary>
        [Parameter]
        public string IdPrefix { get; set; } = "polhem-form";

        private IEnumerable<LayoutSection> EnumerateSections()
            => Layout?.Sections ?? Enumerable.Empty<LayoutSection>();

        private static IEnumerable<LayoutField> EnumerateFields(LayoutSection section)
            => section.Fields?.Where(f => f.Visible) ?? Enumerable.Empty<LayoutField>();

        private string FieldInputId(LayoutField field)
            => string.Create(CultureInfo.InvariantCulture, $"{IdPrefix}-{field.FieldName}");

        private Dictionary<string, object?> BuildEditorParameters(LayoutField field)
        {
            var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [nameof(FieldEditorBase.FieldName)] = field.FieldName,
                [nameof(FieldEditorBase.LayoutField)] = field,
                [nameof(FieldEditorBase.Id)] = FieldInputId(field),
            };
            if (field.ControlType == ControlType.NumericEdit && RoundingContext is { } rounding)
            {
                parameters[nameof(NumericEdit.CurrencySettings)] = rounding.CurrencySettings;
                parameters[nameof(NumericEdit.UnitSettings)] = rounding.UnitSettings;
                parameters[nameof(NumericEdit.DefaultCurrencyCode)] = rounding.Company?.DefaultCurrency ?? string.Empty;
            }
            return parameters;
        }

        private string BuildGridStyle()
        {
            var columns = Layout?.ColumnCount ?? 1;
            if (columns < 1) columns = 1;
            return string.Create(
                CultureInfo.InvariantCulture,
                $"display:grid;grid-template-columns:repeat({columns},minmax(0,1fr));gap:8px");
        }

        private static string BuildFieldStyle(LayoutField field)
        {
            var rowSpan = field.RowSpan < 1 ? 1 : field.RowSpan;
            var colSpan = field.ColumnSpan < 1 ? 1 : field.ColumnSpan;
            return string.Create(
                CultureInfo.InvariantCulture,
                $"grid-row:span {rowSpan};grid-column:span {colSpan}");
        }
    }
}
