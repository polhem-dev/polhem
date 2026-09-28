using System.Data;
using Polhem.Api.Client;
using System.Globalization;
using Polhem.Definition;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
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


        /// <summary>
        /// Formats a cell for display in the circuit's <see cref="CultureInfo.CurrentCulture"/>:
        /// its separators and date patterns, the list item text of a drop-down column, the time of day
        /// of a <see cref="ControlType.DateTimeEdit"/> column even at midnight, and localized text for
        /// Boolean values.
        /// </summary>
        /// <remarks>Display only; the values the grid is given, and anything it reports, stay invariant.</remarks>
        private string FormatCell(DataRow row, LayoutColumn column)
        {
            if (!row.Table.Columns.Contains(column.FieldName)) return string.Empty;
            var raw = row[column.FieldName];
            if (raw is null || raw == DBNull.Value) return string.Empty;

            if (column.ControlType == ControlType.DropDownEdit
                && FormValueBinding.TryGetListItemText(raw, ResolveListItems(column.FieldName), out var itemText))
            {
                return itemText;
            }

            var culture = CultureInfo.CurrentCulture;
            if (column.ControlType == ControlType.DateTimeEdit && string.IsNullOrEmpty(column.DisplayFormat)
                && raw is DateTime instant)
            {
                return instant.ToString("G", culture);
            }
            if (!string.IsNullOrEmpty(column.DisplayFormat) && raw is IFormattable formattableDisplay)
                return formattableDisplay.ToString(column.DisplayFormat, culture);
            if (!string.IsNullOrEmpty(column.NumberFormat) && raw is IFormattable formattableNumber)
                return formattableNumber.ToString(column.NumberFormat, culture);

            return raw switch
            {
                bool b => Text(b ? PolhemUIText.True : PolhemUIText.False),
                DateTime dt => dt.TimeOfDay == TimeSpan.Zero
                    ? dt.ToString("d", culture)
                    : dt.ToString("G", culture),
                IFormattable f => f.ToString(null, culture),
                _ => raw.ToString() ?? string.Empty,
            };
        }

        private ListItemCollection? ResolveListItems(string fieldName)
        {
            var fields = FormTable?.Fields;
            return fields is not null && fields.Contains(fieldName) ? fields[fieldName].ListItems : null;
        }

        private static string BuildColumnStyle(LayoutColumn column)
            => column.Width > 0
                ? string.Create(CultureInfo.InvariantCulture, $"width:{column.Width}px")
                : string.Empty;
    }
}
