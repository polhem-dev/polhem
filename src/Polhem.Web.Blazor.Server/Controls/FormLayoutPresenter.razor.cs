using System.Globalization;
using Microsoft.AspNetCore.Components;
using Polhem.Definition;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.Fields;
using Polhem.Web.Blazor.Server.Controls.Editors;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.Controls
{
    /// <summary>
    /// Renders the master sections of a single-record form from a <see cref="FormLayout"/>, one field
    /// editor per <see cref="LayoutField"/>, chosen by <see cref="FieldEditorFactory"/> from its
    /// <see cref="ControlType"/>. The counterpart of the Avalonia head's <c>Polhem.UI.Avalonia.Controls.FormLayoutPresenter</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The editors bind through a <see cref="FormScope"/> carrying <see cref="DataObject"/> and
    /// <see cref="FormMode"/>. Without a data object each editor still follows its layout field's
    /// <see cref="LayoutFieldBase.ReadOnly"/> and <see cref="LayoutField.AllowEditModes"/>, so the presenter
    /// shows what a layout looks like without data.
    /// </para>
    /// <para>
    /// The layout's detail grids (<see cref="FormLayout.Details"/>) follow the master sections, each a
    /// <see cref="GridControl"/> in <see cref="DetailEditMode"/>, or in <see cref="GridEditMode.EditForm"/>
    /// when <see cref="IsCompact"/> is set, as the Avalonia presenter does.
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
        /// Gets or sets whether to render for a phone-sized screen: the detail grids then edit through the
        /// single-column edit form whatever <see cref="DetailEditMode"/> says. The server cannot measure the
        /// browser, so the host decides.
        /// </summary>
        [Parameter]
        public bool IsCompact { get; set; }

        /// <summary>
        /// Gets or sets the detail grids' editing model when <see cref="IsCompact"/> is <c>false</c>. Defaults
        /// to <see cref="GridEditMode.InCell"/>.
        /// </summary>
        [Parameter]
        public GridEditMode DetailEditMode { get; set; } = GridEditMode.InCell;

        /// <summary>
        /// Gets or sets the id prefix applied to every rendered input. Pages that embed several presenters
        /// set distinct prefixes so the DOM ids stay unique.
        /// </summary>
        [Parameter]
        public string IdPrefix { get; set; } = "polhem-form";

        private IEnumerable<LayoutSection> EnumerateSections()
            => Layout?.Sections ?? Enumerable.Empty<LayoutSection>();

        private GridEditMode EffectiveDetailEditMode => IsCompact ? GridEditMode.EditForm : DetailEditMode;

        private IEnumerable<LayoutGrid> EnumerateDetails()
            => Layout?.Details ?? Enumerable.Empty<LayoutGrid>();

        // A required field's caption carries the required class; a read-only field's cue is its editor's
        // read-only state instead (FieldCaptionRules). The schema's Required counts as well as the layout's,
        // because the server enforces the schema's.
        private string LabelClass(LayoutField field)
        {
            var required = field.Required || DataObject?.GetFormField(field.FieldName)?.Required == true;
            return FieldCaptionRules.ShowsRequiredCue(field.ReadOnly, required)
                ? "polhem-form-layout__label polhem-form-layout__label--required"
                : "polhem-form-layout__label";
        }

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
