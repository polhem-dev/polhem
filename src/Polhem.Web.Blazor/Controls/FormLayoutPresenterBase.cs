using System.Globalization;
using Microsoft.AspNetCore.Components;
using Polhem.Definition;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.Fields;
using Polhem.Web.Blazor.Controls.Editors;
using Polhem.UI.Core.DataObjects;

namespace Polhem.Web.Blazor.Controls
{
    /// <summary>
    /// The rules of a single-record form rendered from a <see cref="FormLayout"/>, which a UI package draws with one
    /// field editor per <see cref="LayoutField"/>. The counterpart of the Avalonia head's
    /// <c>Polhem.UI.Avalonia.Controls.FormLayoutPresenter</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The editors bind through a <see cref="FormScope"/> carrying <see cref="DataObject"/> and
    /// <see cref="FormMode"/>. Without a data object each editor still follows its layout field's
    /// <see cref="LayoutFieldBase.ReadOnly"/> and <see cref="LayoutField.AllowEditModes"/>, so the presenter
    /// shows what a layout looks like without data.
    /// </para>
    /// <para>
    /// The layout's detail grids (<see cref="FormLayout.Details"/>) follow the master sections, each in
    /// <see cref="DetailEditMode"/>. When <see cref="IsCompact"/> is set the master fields lay out in a single column
    /// and the grids edit in <see cref="GridEditMode.EditForm"/>, as the Avalonia presenter does.
    /// </para>
    /// </remarks>
    public abstract class FormLayoutPresenterBase : ComponentBase
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
        /// Gets or sets whether to render for a phone-sized screen: the master fields then lay out in a single
        /// column, whatever <see cref="FormLayout.ColumnCount"/> says, and the detail grids edit through the
        /// single-column edit form, whatever <see cref="DetailEditMode"/> says.
        /// </summary>
        /// <remarks>
        /// The presenter does not measure the browser itself. A form page sets this from a
        /// <see cref="Polhem.Web.Blazor.Components.CompactViewportObserver"/>; a page of its own can place one the same
        /// way, or set the value directly.
        /// </remarks>
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

        /// <summary>
        /// Gets or sets the handler a relation field asks to open its record through when <see cref="FormMode"/> is
        /// <see cref="SingleFormMode.View"/>: a double-click on the field passes it the record the field points at.
        /// Without one the fields do not offer it.
        /// </summary>
        [Parameter]
        public EventCallback<RelationTarget> OpenRelation { get; set; }

        /// <summary>Gets the detail grids' editing model after <see cref="IsCompact"/> applies.</summary>
        protected GridEditMode EffectiveDetailEditMode => IsCompact ? GridEditMode.EditForm : DetailEditMode;

        /// <summary>Gets the number of columns the master fields lay out in.</summary>
        protected int EffectiveColumnCount => IsCompact ? 1 : Math.Max(1, Layout?.ColumnCount ?? 1);

        /// <summary>Returns the layout's master sections.</summary>
        /// <returns>The sections, empty without a layout.</returns>
        protected IEnumerable<LayoutSection> EnumerateSections()
            => Layout?.Sections ?? Enumerable.Empty<LayoutSection>();

        /// <summary>Returns the layout's detail grids.</summary>
        /// <returns>The grids, empty without a layout.</returns>
        protected IEnumerable<LayoutGrid> EnumerateDetails()
            => Layout?.Details ?? Enumerable.Empty<LayoutGrid>();

        /// <summary>Returns the visible fields of <paramref name="section"/>.</summary>
        /// <param name="section">A master section.</param>
        /// <returns>The fields.</returns>
        protected static IEnumerable<LayoutField> EnumerateFields(LayoutSection section)
            => section.Fields?.Where(f => f.Visible) ?? Enumerable.Empty<LayoutField>();

        /// <summary>Returns whether the caption of <paramref name="field"/> carries the required cue.</summary>
        /// <remarks>
        /// A read-only field's cue is its editor's read-only state instead (<see cref="FieldCaptionRules"/>). The
        /// schema's Required counts as well as the layout's, because the server enforces the schema's.
        /// </remarks>
        /// <param name="field">A master field.</param>
        /// <returns><see langword="true"/> when the caption shows the cue.</returns>
        protected bool ShowsRequiredCue(LayoutField field)
        {
            var required = field.Required || DataObject?.GetFormField(field.FieldName)?.Required == true;
            return FieldCaptionRules.ShowsRequiredCue(field.ReadOnly, required);
        }

        /// <summary>Returns the id of the input of <paramref name="field"/>.</summary>
        /// <param name="field">A master field.</param>
        /// <returns>The id.</returns>
        protected string FieldInputId(LayoutField field)
            => string.Create(CultureInfo.InvariantCulture, $"{IdPrefix}-{field.FieldName}");

        /// <summary>Returns the parameters of the editor of <paramref name="field"/>.</summary>
        /// <param name="field">A master field.</param>
        /// <returns>The parameters, for a <see cref="DynamicComponent"/>.</returns>
        protected Dictionary<string, object?> BuildEditorParameters(LayoutField field)
        {
            var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [nameof(FieldEditorBase.FieldName)] = field.FieldName,
                [nameof(FieldEditorBase.LayoutField)] = field,
                [nameof(FieldEditorBase.Id)] = FieldInputId(field),
            };
            if (field.ControlType == ControlType.NumericEdit && RoundingContext is { } rounding)
            {
                parameters[nameof(NumericEditorBase.CurrencySettings)] = rounding.CurrencySettings;
                parameters[nameof(NumericEditorBase.UnitSettings)] = rounding.UnitSettings;
                parameters[nameof(NumericEditorBase.DefaultCurrencyCode)] = rounding.Company?.DefaultCurrency ?? string.Empty;
            }
            return parameters;
        }

        /// <summary>Returns the CSS grid of a master section.</summary>
        /// <returns>The inline style.</returns>
        protected string BuildGridStyle()
            => string.Create(
                CultureInfo.InvariantCulture,
                $"display:grid;grid-template-columns:repeat({EffectiveColumnCount},minmax(0,1fr));gap:8px");

        /// <summary>Returns the CSS grid placement of <paramref name="field"/>.</summary>
        /// <remarks>
        /// A CSS grid adds implicit columns for a span wider than the template, so a two-column field in the
        /// compact single column would widen the form again. Avalonia's Grid clamps the span itself.
        /// </remarks>
        /// <param name="field">A master field.</param>
        /// <param name="columnCount">The number of columns laid out.</param>
        /// <returns>The inline style.</returns>
        protected static string BuildFieldStyle(LayoutField field, int columnCount)
        {
            var rowSpan = field.RowSpan < 1 ? 1 : field.RowSpan;
            var colSpan = Math.Clamp(field.ColumnSpan, 1, columnCount);
            return string.Create(
                CultureInfo.InvariantCulture,
                $"grid-row:span {rowSpan};grid-column:span {colSpan}");
        }
    }
}
