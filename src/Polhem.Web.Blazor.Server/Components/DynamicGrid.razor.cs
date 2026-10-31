using System.Data;
using Polhem.UI.Core;
using Polhem.UI.Core.Fields;
using System.Globalization;
using Polhem.Definition;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Definition.Sorting;
using Microsoft.AspNetCore.Components;

namespace Polhem.Web.Blazor.Server.Components
{
    /// <summary>
    /// Code-behind for <c>DynamicGrid.razor</c>. Renders a <see cref="Polhem.Definition.Layouts.LayoutGrid"/>
    /// over a <see cref="DataTable"/> and raises <see cref="OnRowSelected"/> with
    /// the <see cref="SysFields.RowId"/> Guid when a row is clicked.
    /// </summary>
    /// <remarks>
    /// The grid is intentionally <em>presentation-only</em>: the host (typically
    /// <see cref="Polhem.Web.Blazor.Server.Components.FormPage"/>) owns the call to <see cref="Polhem.Api.Client.Connectors.FormApiConnector.GetListAsync"/> and
    /// passes the resulting <see cref="DataTable"/> in via <see cref="Rows"/>.
    /// Keeping the fetch outside the grid lets the host coordinate refresh with
    /// the master form (e.g. re-load the list after Save / Delete).
    /// </remarks>
    public sealed partial class DynamicGrid : ComponentBase
    {
        /// <summary>
        /// Gets or sets the list layout that defines the visible columns.
        /// </summary>
        [Parameter]
        public LayoutGrid? Layout { get; set; }

        /// <summary>
        /// Gets or sets the data rows to render.
        /// </summary>
        [Parameter]
        public DataTable? Rows { get; set; }

        /// <summary>
        /// Gets or sets the schema table whose fields describe the columns, typically the master table
        /// of the schema the list layout was generated from. With it a drop-down column shows its list
        /// item's text instead of the stored code, localized when the schema was; <c>null</c> — the
        /// default — shows stored values as they are.
        /// </summary>
        [Parameter]
        public FormTable? FormTable { get; set; }

        /// <summary>
        /// Invoked when the user clicks a row; receives the row's
        /// <see cref="SysFields.RowId"/> Guid. Rows without a parseable
        /// <c>sys_rowid</c> are silently ignored.
        /// </summary>
        [Parameter]
        public EventCallback<Guid> OnRowSelected { get; set; }

        /// <summary>
        /// Invoked when the user double-clicks a row, the gesture that confirms a choice in a picker;
        /// receives the row's <see cref="SysFields.RowId"/> Guid.
        /// </summary>
        [Parameter]
        public EventCallback<Guid> OnRowActivated { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="SysFields.RowId"/> of the row to show as selected, or <c>null</c>
        /// for none. The grid does not track a selection itself; the host passes back the id
        /// <see cref="OnRowSelected"/> gave it.
        /// </summary>
        [Parameter]
        public Guid? SelectedRowId { get; set; }

        /// <summary>
        /// Invoked when the user clicks a sortable column header; receives the field name. Without a handler the
        /// headers are plain text. The grid does not sort: the host reads the rows in the new order.
        /// </summary>
        [Parameter]
        public EventCallback<string> OnSortRequested { get; set; }

        /// <summary>Gets or sets the field to mark as sorted, or <c>null</c> for none.</summary>
        [Parameter]
        public string? SortFieldName { get; set; }

        /// <summary>Gets or sets the direction of <see cref="SortFieldName"/>.</summary>
        [Parameter]
        public SortDirection SortDirection { get; set; }

        /// <summary>
        /// Gets or sets the placeholder text shown when there is no data. <c>null</c> — the
        /// default — shows the localized <see cref="PolhemUIText.NoData"/> text.
        /// </summary>
        [Parameter]
        public string? EmptyText { get; set; }

        // Nullable: a component created outside a renderer has no services, and still renders.
        [Inject]
        private IServiceProvider? Services { get; set; }

        private string Text(string key) => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), key);

        private string DisplayedEmptyText => EmptyText ?? Text(PolhemUIText.NoData);

        private IEnumerable<LayoutColumn> VisibleColumns
            => Layout?.Columns?.Where(c => c.Visible) ?? Enumerable.Empty<LayoutColumn>();

        private async Task OnRowClickAsync(DataRow row)
        {
            if (!OnRowSelected.HasDelegate) return;
            if (!FormDataGuard.TryGetRowId(row, out var rowId)) return;
            await OnRowSelected.InvokeAsync(rowId);
        }

        private async Task OnRowDoubleClickAsync(DataRow row)
        {
            if (!OnRowActivated.HasDelegate) return;
            if (!FormDataGuard.TryGetRowId(row, out var rowId)) return;
            await OnRowActivated.InvokeAsync(rowId);
        }

        private string RowClass(DataRow row)
            => SelectedRowId is { } selected && FormDataGuard.TryGetRowId(row, out var rowId) && rowId == selected
                ? "polhem-dynamic-grid__row polhem-dynamic-grid__row--selected"
                : "polhem-dynamic-grid__row";


        /// <summary>
        /// Formats a cell for display in the circuit's <see cref="CultureInfo.CurrentCulture"/>. The rules are
        /// <see cref="GridCellFormatter.FormatColumnText"/>, which every grid of every UI head shares: the list
        /// item text of a drop-down column, the time of day of a <see cref="ControlType.DateTimeEdit"/> column
        /// even at midnight, the composed display fields of a lookup column, and localized text for Boolean
        /// values.
        /// </summary>
        /// <remarks>Display only; the values the grid is given, and anything it reports, stay invariant.</remarks>
        private string FormatCell(DataRow row, LayoutColumn column)
            => GridCellFormatter.FormatColumnText(row, column, ResolveFormField(column.FieldName),
                currencySettings: null, defaultCurrencyCode: string.Empty, unitSettings: null,
                flag => Text(flag ? PolhemUIText.True : PolhemUIText.False));

        private FormField? ResolveFormField(string fieldName)
        {
            var fields = FormTable?.Fields;
            return fields is not null && fields.Contains(fieldName) ? fields[fieldName] : null;
        }

        private static string BuildColumnStyle(LayoutColumn column)
            => column.Width > 0
                ? string.Create(CultureInfo.InvariantCulture, $"width:{column.Width}px")
                : string.Empty;

        // Oracle cannot order by a `Text` column, and a binary value has no meaningful order.
        private bool IsSortable(LayoutColumn column)
            => OnSortRequested.HasDelegate
                && FormTable?.Fields is { } fields
                && fields.Contains(column.FieldName)
                && fields[column.FieldName].DbType is not (FieldDbType.Text or FieldDbType.Binary);

        private bool IsSorted(LayoutColumn column)
            => SortFieldName is not null && StringUtilities.IsEquals(SortFieldName, column.FieldName);

        private string SortMark(LayoutColumn column)
            => !IsSorted(column) ? string.Empty : SortDirection == SortDirection.Desc ? " \u25BC" : " \u25B2";

        private string? AriaSort(LayoutColumn column)
            => !IsSorted(column) ? null : SortDirection == SortDirection.Desc ? "descending" : "ascending";
    }
}
