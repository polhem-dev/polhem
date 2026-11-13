using System.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Sorting;
using Polhem.UI.Core.Fields;
using Polhem.Web.Blazor.Components;
using Polhem.Web.Blazor.Controls.Editors;
using Polhem.UI.Core.DataObjects;
using Polhem.UI.Core.Editing;
using Polhem.UI.Core.Lists;

namespace Polhem.Web.Blazor.Controls
{
    /// <summary>
    /// The state and commands of a tabular control, which a UI package renders: a <see cref="LayoutGrid"/> over a detail table of a
    /// <see cref="FormDataObject"/>, or over a caller-supplied <see cref="System.Data.DataTable"/> in list
    /// mode, with an Add / Edit / Delete toolbar while editing is allowed. The counterpart of the Avalonia
    /// head's <c>Polhem.UI.Avalonia.Controls.GridControl</c>, with the same editing rules.
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
    /// cells show text and a row is edited in an edit form; a cancelled Add removes the blank row again.
    /// </para>
    /// <para>
    /// On a read-only form a relation column's cell with a value opens the record it points at: it shows a hint, and a
    /// double-click passes the record to the enclosing <see cref="FormScope"/>'s <see cref="FormScope.OpenRelation"/>,
    /// as a relation field's editor does. Without that handler the cell offers nothing.
    /// </para>
    /// <para>
    /// The cell text is <see cref="GridCellFormatter"/>, which every grid of every head shares. A read-only
    /// column's header is parenthesised and a required one shows the required cue (<see cref="FieldCaptionRules"/>).
    /// </para>
    /// </remarks>
    public abstract partial class GridControlBase : ComponentBase, IBindTableControl, IUIControl, IDisposable, IAsyncDisposable
    {
        private bool _disposed;
        private FormDataObject? _subscribed;
        private SingleFormMode? _controlStateMode;
        private DataRow? _selectedRow;
        private DataRow? _editingRow;
        // Whether the row in the edit form was added by the toolbar, so a cancel removes it again.
        private bool _editingNewRow;

        /// <summary>Gets the row open in the edit form, or <c>null</c>.</summary>
        protected DataRow? EditingRow => _editingRow;

        /// <summary>
        /// Gets the host that keeps one editor open in the grid, for a UI package that renders editors on demand. The
        /// grid closes its editor when it stops editing in cells, as when the form enters View mode.
        /// </summary>
        protected GridEditorHost EditorHost { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GridControlBase"/> class.
        /// </summary>
        protected GridControlBase() => EditorHost = new GridEditorHost { EditorParametersBuilder = BuildEditorParameters };

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
        /// Invoked when the user double-clicks a row; receives the row's <see cref="SysFields.RowId"/>.
        /// </summary>
        [Parameter]
        public EventCallback<Guid> RowActivated { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="SysFields.RowId"/> of a row to show as selected, for a host that keeps the selection
        /// across pages of rows; <c>null</c> leaves it to the grid.
        /// </summary>
        [Parameter]
        public Guid? SelectedRowId { get; set; }

        /// <summary>
        /// Invoked when the user clicks a sortable column header; receives the field name. Without a handler the headers
        /// are plain text. The grid does not sort: the host reads the rows in the new order.
        /// </summary>
        [Parameter]
        public EventCallback<string> OnSortRequested { get; set; }

        /// <summary>
        /// Gets or sets the list's column filters. With <see cref="OnFilterChanged"/>, each column that can be filtered
        /// offers it. The grid does not filter its rows itself; the host reads them again.
        /// </summary>
        [Parameter]
        public ListFilterState? Filters { get; set; }

        /// <summary>Gets or sets the callback raised when the user applies or clears a column's filter.</summary>
        [Parameter]
        public EventCallback<ColumnFilterChange> OnFilterChanged { get; set; }

        /// <summary>Gets or sets the field to mark as sorted, or <c>null</c> for none.</summary>
        [Parameter]
        public string? SortFieldName { get; set; }

        /// <summary>Gets or sets the direction of <see cref="SortFieldName"/>.</summary>
        [Parameter]
        public SortDirection SortDirection { get; set; }

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

        [Inject]
        private IJSRuntime? JS { get; set; }

        private GridEditorScript? _script;

        /// <summary>
        /// Gets the CSS selector that matches a popup of the UI framework while it is open, or <c>null</c> for a grid
        /// that uses no editor script; <c>null</c> by default.
        /// </summary>
        /// <remarks>The editor script lets a key through to an open popup instead of the grid.</remarks>
        protected virtual string? OpenPopupSelector => null;

        /// <summary>
        /// Gets how long the editor script waits before it replays keys typed ahead into a new editor; zero by default.
        /// </summary>
        protected virtual int ReplayDelayMilliseconds => 0;

        /// <summary>Gets the script that holds keys typed before a cell's editor arrives and focuses the editor.</summary>
        /// <exception cref="InvalidOperationException">
        /// The grid has no <see cref="OpenPopupSelector"/>, or it was created outside a renderer.
        /// </exception>
        protected GridEditorScript Script
            => _script ??= new GridEditorScript(
                JS ?? throw new InvalidOperationException("The grid's editor script needs a renderer's JavaScript runtime."),
                OpenPopupSelector ?? throw new InvalidOperationException("The grid declares no OpenPopupSelector, so it has no editor script."),
                ReplayDelayMilliseconds);

        /// <summary>Gets the data object the grid is bound to, or <c>null</c>.</summary>
        protected FormDataObject? BoundDataObject => DataObject ?? Scope?.DataObject;

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

        /// <summary>Gets a value indicating whether the grid is bound to a data object.</summary>
        protected bool HasData => BoundDataObject is not null;

        // A form detail grid rendered from its layout alone (a layout preview): it shows the toolbar the
        // layout and the form mode allow, with its buttons disabled.
        private bool IsLayoutOnly => BoundDataObject is null && DataTable is null && Layout is not null;

        private SingleFormMode FormMode => _controlStateMode ?? Scope?.FormMode ?? SingleFormMode.Edit;

        private bool EffectiveAllowEdit
            => AllowEdit ?? (FormMode != SingleFormMode.View && (Layout?.AllowEditModes.Allows(FormMode) ?? false));

        /// <summary>
        /// Gets what identifies <see cref="Layout"/>: its table and the field names of its columns.
        /// </summary>
        /// <remarks>
        /// A UI package keys the grid it renders by this, so the widths and the order a user gave the columns stay with
        /// one layout. A form page keeps its grid when the route opens another form, and a lookup builds an equal copy of
        /// its layout on every render, so neither the grid instance nor the layout instance tells the two apart.
        /// </remarks>
        protected string LayoutIdentity
            => Layout is null
                ? string.Empty
                : $"{Layout.TableName}|{string.Join(",", Layout.Columns?.Select(c => c.FieldName) ?? [])}";

        /// <summary>Gets the toolbar actions the layout allows.</summary>
        protected GridControlAllowActions Actions => Layout?.AllowActions ?? GridControlAllowActions.None;

        private bool CanEdit => EffectiveAllowEdit && (HasData || IsLayoutOnly);

        /// <summary>Gets a value indicating whether the toolbar shows.</summary>
        protected bool ShowToolbar => CanEdit && Actions != GridControlAllowActions.None;

        private bool InCellEdit
            => CanEdit && HasData && EditMode == GridEditMode.InCell && Actions.HasFlag(GridControlAllowActions.Edit);

        private bool CanUseEditForm
            => CanEdit && HasData && EditMode == GridEditMode.EditForm && Actions.HasFlag(GridControlAllowActions.Edit);

        /// <summary>Gets the columns to render.</summary>
        protected IEnumerable<LayoutColumn> VisibleColumns
            => Layout?.Columns?.Where(c => c.Visible) ?? Enumerable.Empty<LayoutColumn>();

        /// <summary>Gets the rows to render, without deleted ones.</summary>
        protected IEnumerable<DataRow> VisibleRows
            => Table?.Rows.Cast<DataRow>().Where(r => r.RowState is not (DataRowState.Deleted or DataRowState.Detached))
                ?? Enumerable.Empty<DataRow>();

        /// <inheritdoc />
        public void SetControlState(SingleFormMode formMode)
        {
            _controlStateMode = formMode;
            CloseEditorUnlessInCellEdit();
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
            CloseEditorUnlessInCellEdit();
            RefreshRowList();
        }

        /// <inheritdoc />
        protected override bool ShouldRender()
        {
            RefreshRowList();
            return true;
        }

        /// <summary>
        /// Takes <see cref="VisibleRows"/> into the list the UI framework's grid reads. Called before every render, so a UI
        /// package keeps the list in whatever shape its grid needs. Does nothing by default.
        /// </summary>
        protected virtual void RefreshRowList()
        {
        }

        // Loaded before any cell opens, so a key typed before the first editor arrives is already held.
        /// <inheritdoc />
        protected override Task OnAfterRenderAsync(bool firstRender)
            => OpenPopupSelector is not null && VisibleColumns.Any(IsInCellEditor) ? Script.LoadAsync() : Task.CompletedTask;

        /// <summary>Gets the CSS class of a row, which marks the selected one.</summary>
        /// <param name="row">The row.</param>
        /// <returns>The class.</returns>
        protected string RowClass(DataRow row)
            => IsSelected(row) ? "polhem-grid__row polhem-grid__row--selected" : "polhem-grid__row";

        /// <summary>Gets the CSS class of a column's header, which marks a required column.</summary>
        /// <param name="column">The column.</param>
        /// <returns>The class.</returns>
        protected string HeaderClass(LayoutColumn column)
            => ShowsRequiredCue(column) ? "polhem-grid__header polhem-grid__header--required" : "polhem-grid__header";

        /// <summary>Gets the localized text of <paramref name="key"/>.</summary>
        /// <param name="key">A <see cref="PolhemUIText"/> key.</param>
        /// <returns>The text.</returns>
        protected string Text(string key) => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), key);
    }
}
