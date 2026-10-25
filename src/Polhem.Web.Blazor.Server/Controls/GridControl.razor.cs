using System.Data;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.UI.Core;
using Polhem.UI.Core.Fields;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.Controls.Editors;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.Controls
{
    /// <summary>
    /// Tabular control that renders a <see cref="LayoutGrid"/> over a detail table of a
    /// <see cref="FormDataObject"/>, or over a caller-supplied <see cref="System.Data.DataTable"/> in list
    /// mode, with an Add / Edit / Delete toolbar while editing is allowed. The counterpart of the Avalonia
    /// head's <c>GridControl</c>, with the same editing rules.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Editing needs a data object (its <see cref="DataObject"/> parameter or the enclosing
    /// <see cref="FormScope"/>'s) and is allowed when <see cref="AllowEdit"/> says so: by default when the form
    /// mode is not View and the layout's <see cref="LayoutGrid.AllowEditModes"/> admits it. The layout's
    /// <see cref="LayoutGrid.AllowActions"/> then decides which toolbar buttons show and whether rows edit.
    /// A list-mode grid never edits.
    /// </para>
    /// <para>
    /// In <see cref="GridEditMode.InCell"/> each editable cell shows its field editor bound to the row, so
    /// a value commits when the user leaves the cell, as in a form. In <see cref="GridEditMode.EditForm"/>
    /// cells show text and a row is edited in <see cref="RowEditDialog"/>; a cancelled Add removes the
    /// blank row again. The Avalonia grid needs click-to-swap editors and other workarounds for its native
    /// DataGrid's edit pipeline (ADR-021); an HTML table has no such pipeline, so this grid renders the
    /// editors directly.
    /// </para>
    /// <para>
    /// The cell text is <see cref="GridCellFormatter"/>, which every grid of every head shares. A read-only
    /// column's header is parenthesised and a required one carries the
    /// <c>polhem-grid__header--required</c> class (<see cref="FieldCaptionRules"/>).
    /// </para>
    /// </remarks>
    public sealed partial class GridControl : ComponentBase, IBindTableControl, IUIControl, IDisposable
    {
        private const string AddIconGeometry =
            "M12 4a1 1 0 0 1 1 1v6h6a1 1 0 1 1 0 2h-6v6a1 1 0 1 1-2 0v-6H5a1 1 0 1 1 0-2h6V5a1 1 0 0 1 1-1Z";
        private const string EditIconGeometry =
            "M16.77 3.16a2.5 2.5 0 0 1 3.54 0l.53.53a2.5 2.5 0 0 1 0 3.54l-1.42 1.41-4.06-4.06 1.41-1.42Z" +
            "M14.3 5.64l4.06 4.06-9.53 9.53a2 2 0 0 1-.94.53l-3.6.9a.75.75 0 0 1-.91-.9l.9-3.61a2 2 0 0 1 .53-.94L14.3 5.64Z";
        private const string DeleteIconGeometry =
            "M10 2.5h4A1.5 1.5 0 0 1 15.5 4v.5H20a1 1 0 1 1 0 2h-.55l-.88 13.2A2.5 2.5 0 0 1 16.08 22H7.92" +
            "a2.5 2.5 0 0 1-2.49-2.3L4.55 6.5H4a1 1 0 0 1 0-2h4.5V4A1.5 1.5 0 0 1 10 2.5Z" +
            "M6.56 6.5l.86 12.97a.5.5 0 0 0 .5.46h8.16a.5.5 0 0 0 .5-.46l.86-12.97H6.56Z" +
            "M10 9.5a1 1 0 0 1 1 1v6a1 1 0 1 1-2 0v-6a1 1 0 0 1 1-1Zm4 0a1 1 0 0 1 1 1v6a1 1 0 1 1-2 0v-6a1 1 0 0 1 1-1Z";

        private FormDataObject? _subscribed;
        private SingleFormMode? _controlStateMode;
        private DataRow? _selectedRow;
        private DataRow? _editingRow;
        // Whether the row in the edit form was added by the toolbar, so a cancel removes it again.
        private bool _editingNewRow;

        /// <summary>
        /// Gets or sets the grid layout that defines the columns and the allowed actions.
        /// </summary>
        [Parameter]
        public LayoutGrid? Layout { get; set; }

        /// <summary>
        /// Gets or sets the data object whose detail table the grid shows; <c>null</c> uses the enclosing
        /// <see cref="FormScope"/>'s.
        /// </summary>
        [Parameter]
        public FormDataObject? DataObject { get; set; }

        /// <summary>
        /// Gets or sets the bound table name; empty uses the layout's <see cref="LayoutGrid.TableName"/>.
        /// </summary>
        [Parameter]
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the rows of a list-mode grid: a caller-supplied table outside any data object, which
        /// the grid never edits. Ignored while the grid is bound to a data object.
        /// </summary>
        [Parameter]
        public DataTable? DataTable { get; set; }

        /// <summary>
        /// Gets or sets the schema table that describes a list-mode grid's columns, so a drop-down column shows
        /// its list item's text; a bound grid reads its fields from the data object.
        /// </summary>
        [Parameter]
        public FormTable? FormTable { get; set; }

        /// <summary>
        /// Gets or sets the editing model. Defaults to <see cref="GridEditMode.InCell"/>.
        /// </summary>
        [Parameter]
        public GridEditMode EditMode { get; set; } = GridEditMode.InCell;

        /// <summary>
        /// Gets or sets whether editing is allowed; <c>null</c> (the default) derives it from the form mode
        /// and the layout's <see cref="LayoutGrid.AllowEditModes"/>.
        /// </summary>
        [Parameter]
        public bool? AllowEdit { get; set; }

        /// <summary>
        /// Gets or sets whether the edit form lays out in a single column (phone-sized screens).
        /// </summary>
        [Parameter]
        public bool Compact { get; set; }

        /// <summary>
        /// Gets or sets the currency master for per-row decimals of amount columns, or <c>null</c> to keep the
        /// delivered formats.
        /// </summary>
        [Parameter]
        public CurrencySettings? CurrencySettings { get; set; }

        /// <summary>
        /// Gets or sets the currency code for amount cells whose row carries no currency-key field.
        /// </summary>
        [Parameter]
        public string DefaultCurrencyCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unit-of-measure master for per-row decimals of quantity and weight columns, or
        /// <c>null</c> to keep the delivered formats.
        /// </summary>
        [Parameter]
        public UnitSettings? UnitSettings { get; set; }

        /// <summary>
        /// Invoked when the user selects a row; receives the row's <see cref="SysFields.RowId"/>. Rows
        /// without a parseable <c>sys_rowid</c> are ignored.
        /// </summary>
        [Parameter]
        public EventCallback<Guid> RowSelected { get; set; }

        /// <summary>
        /// Gets the selected row, or <c>null</c>.
        /// </summary>
        public DataRow? SelectedRow => _selectedRow is { RowState: not (DataRowState.Deleted or DataRowState.Detached) }
            ? _selectedRow
            : null;

        [CascadingParameter]
        private FormScopeContext? Scope { get; set; }

        // Nullable: a component created outside a renderer has no services, and still renders.
        [Inject]
        private IServiceProvider? Services { get; set; }

        private FormDataObject? BoundDataObject => DataObject ?? Scope?.DataObject;

        private string EffectiveTableName => TableName.Length > 0 ? TableName : Layout?.TableName ?? string.Empty;

        private DataTable? Table
        {
            get
            {
                if (BoundDataObject is { } dataObject)
                {
                    var tables = dataObject.DataSet.Tables;
                    var name = EffectiveTableName;
                    return name.Length > 0 && tables.Contains(name) ? tables[name] : null;
                }
                return DataTable;
            }
        }

        private bool HasData => BoundDataObject is not null;

        // A form detail grid rendered from its layout alone (a layout preview): it shows the toolbar the
        // layout and the form mode allow, with its buttons disabled.
        private bool IsLayoutOnly => BoundDataObject is null && DataTable is null && Layout is not null;

        private SingleFormMode FormMode => _controlStateMode ?? Scope?.FormMode ?? SingleFormMode.Edit;

        private bool EffectiveAllowEdit
            => AllowEdit ?? (FormMode != SingleFormMode.View && (Layout?.AllowEditModes.Allows(FormMode) ?? false));

        private GridControlAllowActions Actions => Layout?.AllowActions ?? GridControlAllowActions.None;

        private bool CanEdit => EffectiveAllowEdit && (HasData || IsLayoutOnly);

        private bool ShowToolbar => CanEdit && Actions != GridControlAllowActions.None;

        private bool InCellEdit
            => CanEdit && HasData && EditMode == GridEditMode.InCell && Actions.HasFlag(GridControlAllowActions.Edit);

        private bool CanUseEditForm
            => CanEdit && HasData && EditMode == GridEditMode.EditForm && Actions.HasFlag(GridControlAllowActions.Edit);

        private IEnumerable<LayoutColumn> VisibleColumns
            => Layout?.Columns?.Where(c => c.Visible) ?? Enumerable.Empty<LayoutColumn>();

        private IEnumerable<DataRow> VisibleRows
            => Table?.Rows.Cast<DataRow>().Where(r => r.RowState is not (DataRowState.Deleted or DataRowState.Detached))
                ?? Enumerable.Empty<DataRow>();

        /// <inheritdoc />
        public void SetControlState(SingleFormMode formMode)
        {
            _controlStateMode = formMode;
            _ = InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Has no effect: an in-cell editor commits its value when the user leaves it, so no edit is pending
        /// when a host inspects or saves the data set. Present for the <see cref="IBindTableControl"/> contract
        /// the Avalonia grid also implements.
        /// </summary>
        public void EndEdit()
        {
        }

        /// <summary>
        /// Appends a new row to the bound table. The owning <see cref="FormDataObject"/> seeds it with the
        /// schema's defaults; a list-mode table gets type-appropriate empty values for its non-null columns.
        /// </summary>
        /// <returns>The new row, or <c>null</c> when there is no table.</returns>
        public DataRow? AddRow()
        {
            if (Table is not { } table) return null;
            var row = table.NewRow();
            if (BoundDataObject is null)
            {
                foreach (DataColumn column in table.Columns)
                {
                    if (!column.AllowDBNull && (column.DefaultValue is null || column.DefaultValue == DBNull.Value))
                        row[column] = FormValueBinding.GetEmptyValue(column.DataType);
                }
            }
            table.Rows.Add(row);
            _selectedRow = row;
            _ = InvokeAsync(StateHasChanged);
            return row;
        }

        /// <summary>
        /// Deletes the selected row (marks it <see cref="DataRowState.Deleted"/> so the save sends the deletion);
        /// a no-op when nothing is selected.
        /// </summary>
        public void DeleteSelectedRow()
        {
            if (SelectedRow is not { } row) return;
            row.Delete();
            _selectedRow = null;
            _ = InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Renders the rows again. Rows refresh by themselves when the data object reports a change; call
        /// this after changing a list-mode table in place.
        /// </summary>
        public void RefreshRows() => _ = InvokeAsync(StateHasChanged);

        /// <inheritdoc />
        protected override void OnParametersSet()
        {
            Subscribe(BoundDataObject);
        }

        /// <inheritdoc />
        public void Dispose() => Subscribe(null);

        private string Text(string key) => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), key);

        private void Subscribe(FormDataObject? dataObject)
        {
            if (ReferenceEquals(dataObject, _subscribed)) return;
            if (_subscribed is not null)
            {
                _subscribed.DataSetReplaced -= OnDataSetReplaced;
                _subscribed.FieldValueChanged -= OnFieldValueChanged;
                _subscribed.RowAdded -= OnRowChanged;
                _subscribed.RowDeleted -= OnRowChanged;
            }
            _subscribed = dataObject;
            if (dataObject is not null)
            {
                dataObject.DataSetReplaced += OnDataSetReplaced;
                dataObject.FieldValueChanged += OnFieldValueChanged;
                dataObject.RowAdded += OnRowChanged;
                dataObject.RowDeleted += OnRowChanged;
            }
        }

        private void OnDataSetReplaced(object? sender, EventArgs e)
        {
            _selectedRow = null;
            _editingRow = null;
            _ = InvokeAsync(StateHasChanged);
        }

        private void OnFieldValueChanged(object? sender, FieldValueChangedEventArgs e)
        {
            if (string.Equals(e.TableName, EffectiveTableName, StringComparison.OrdinalIgnoreCase))
                _ = InvokeAsync(StateHasChanged);
        }

        private void OnRowChanged(object? sender, RowChangedEventArgs e)
        {
            if (string.Equals(e.TableName, EffectiveTableName, StringComparison.OrdinalIgnoreCase))
                _ = InvokeAsync(StateHasChanged);
        }

        private async Task SelectRowAsync(DataRow row)
        {
            _selectedRow = row;
            if (RowSelected.HasDelegate && FormDataGuard.TryGetRowId(row, out var rowId))
                await RowSelected.InvokeAsync(rowId).ConfigureAwait(true);
        }

        private void ActivateRow(DataRow row)
        {
            _selectedRow = row;
            if (CanUseEditForm) OpenEditForm(row, isNew: false);
        }

        private void OnAddClicked()
        {
            if (!HasData) return;
            var row = AddRow();
            if (row is not null && EditMode == GridEditMode.EditForm)
                OpenEditForm(row, isNew: true);
        }

        private void EditSelectedRow()
        {
            if (SelectedRow is { } row && CanUseEditForm)
                OpenEditForm(row, isNew: false);
        }

        private void OpenEditForm(DataRow row, bool isNew)
        {
            _editingRow = row;
            _editingNewRow = isNew;
        }

        private void OnEditCommitted()
        {
            _selectedRow = _editingRow;
            _editingRow = null;
        }

        private void OnEditCancelled()
        {
            // A cancelled Add removes the blank row again instead of leaving an empty line in the table.
            if (_editingNewRow && _editingRow is { } row && row.Table is { } table)
            {
                table.Rows.Remove(row);
                _selectedRow = null;
            }
            _editingRow = null;
        }

        private bool IsInCellEditor(LayoutColumn column) => InCellEdit && !column.ReadOnly;

        private Dictionary<string, object?> BuildEditorParameters(LayoutColumn column, DataRow row)
        {
            var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [nameof(FieldEditorBase.FieldName)] = column.FieldName,
                [nameof(FieldEditorBase.LayoutField)] = column,
                [nameof(FieldEditorBase.Row)] = row,
                [nameof(FieldEditorBase.DataObject)] = BoundDataObject,
            };
            if (column.ControlType == ControlType.NumericEdit)
            {
                parameters[nameof(NumericEdit.CurrencySettings)] = CurrencySettings;
                parameters[nameof(NumericEdit.UnitSettings)] = UnitSettings;
                parameters[nameof(NumericEdit.DefaultCurrencyCode)] = DefaultCurrencyCode;
            }
            return parameters;
        }

        private string FormatCell(DataRow row, LayoutColumn column)
            => GridCellFormatter.FormatColumnText(row, column, ResolveFormField(column.FieldName),
                CurrencySettings, DefaultCurrencyCode, UnitSettings,
                flag => Text(flag ? PolhemUIText.True : PolhemUIText.False));

        private static bool IsChecked(DataRow row, LayoutColumn column)
            => row.Table.Columns.Contains(column.FieldName)
                && string.Equals(FormValueBinding.ToBindingString(row[column.FieldName]), bool.TrueString,
                    StringComparison.OrdinalIgnoreCase);

        // The schema field behind a column: through the data object for a bound grid, through the supplied
        // table for a list-mode grid; null when neither knows the field.
        private FormField? ResolveFormField(string fieldName)
        {
            if (BoundDataObject is { } dataObject)
                return dataObject.GetFormField(EffectiveTableName, fieldName);
            var fields = FormTable?.Fields;
            return fields is not null && fields.Contains(fieldName) ? fields[fieldName] : null;
        }

        // In a bound grid the schema's Required counts as well as the layout's, because the server enforces
        // the schema's; a list grid shows no required cue from the schema, since nothing is entered there.
        private string HeaderClass(LayoutColumn column)
        {
            var required = column.Required
                || (BoundDataObject is { } dataObject && dataObject.GetFormField(EffectiveTableName, column.FieldName)?.Required == true);
            return FieldCaptionRules.ShowsRequiredCue(column.ReadOnly, required)
                ? "polhem-grid__header polhem-grid__header--required"
                : "polhem-grid__header";
        }

        private string RowClass(DataRow row)
            => ReferenceEquals(row, _selectedRow) ? "polhem-grid__row polhem-grid__row--selected" : "polhem-grid__row";

        private static string ColumnStyle(LayoutColumn column)
            => column.Width > 0 ? string.Create(CultureInfo.InvariantCulture, $"width:{column.Width}px") : string.Empty;
    }
}
