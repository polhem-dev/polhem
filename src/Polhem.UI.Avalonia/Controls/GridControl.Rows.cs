using System.Data;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Core;
using Polhem.UI.Core.Editing;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// Reacting to data-object changes, and the add / edit flows that go through the popup row editor.
    /// </summary>
    /// <remarks>
    /// Apart from the binding surface because these are the asynchronous paths: they open a dialog, wait,
    /// and only then touch the grid — which is why each one re-reads state instead of capturing it.
    /// </remarks>
    public partial class GridControl
    {
        /// <summary>
        /// Re-resolves the bound table from the data object by <see cref="TableName"/>
        /// and refreshes the rows. Called after binding and whenever the underlying
        /// <c>DataSet</c> is replaced. Without a layout (ambient, table-name-only
        /// binds), plain text columns are generated from the table's columns.
        /// </summary>
        internal void RefreshFromDataObject()
        {
            var dataObject = _binder.DataObject;
            if (dataObject is null) return;

            var viewport = CaptureViewport();
            var tables = dataObject.DataSet.Tables;
            var tableName = TableName;
            _dataTable = tableName.Length > 0 && tables.Contains(tableName) ? tables[tableName] : null;
            if (_layout is null)
                RebuildFallbackColumns();
            RebuildRows();
            RestoreViewport(viewport);
        }

        private bool CanUseEditForm
            => AllowEdit
                && EditMode == GridEditMode.EditForm
                && _binder.DataObject is not null
                && (_layout?.AllowActions.HasFlag(GridControlAllowActions.Edit) ?? false);

        // Recomputes the effective editing state after AllowEdit / EditMode / bind
        // changes: toolbar visibility per action flag, in-cell editability of the
        // inner grid.
        private void UpdateControlState()
        {
            var actions = _layout?.AllowActions ?? GridControlAllowActions.None;
            var hasData = _binder.DataObject is not null;
            var canEdit = AllowEdit && (hasData || _isLayoutOnly);

            _addButton.IsVisible = actions.HasFlag(GridControlAllowActions.Add);
            _deleteButton.IsVisible = actions.HasFlag(GridControlAllowActions.Delete);
            // In-cell editing needs no Edit button (cells edit in place); EditForm
            // surfaces one beside the double-tap gesture.
            _editButton.IsVisible = EditMode == GridEditMode.EditForm
                && actions.HasFlag(GridControlAllowActions.Edit);
            _toolbar.IsVisible = canEdit && actions != GridControlAllowActions.None;
            // A layout-only grid shows its toolbar for the look of it; with no rows to act on, the buttons stay off.
            _addButton.IsEnabled = hasData;
            _deleteButton.IsEnabled = hasData;
            _editButton.IsEnabled = hasData;

            var inCellEdit = canEdit
                && EditMode == GridEditMode.InCell
                && actions.HasFlag(GridControlAllowActions.Edit);
            var readOnly = !inCellEdit;
            if (_grid.IsReadOnly == readOnly) return;

            _grid.IsReadOnly = readOnly;
            // Always-on editor cells capture the enabled state when their template
            // builds, so an editability switch must re-realize the rows.
            RefreshRows();
        }

        private async Task AddRowAsync()
        {
            if (_binder.DataObject is null || _layout is null || _dataTable is null) return;
            AddRow();
            if (EditMode != GridEditMode.EditForm) return;

            var table = _dataTable;
            var row = table.Rows[table.Rows.Count - 1];
            var committed = await RowEditDialog.ShowAsync(this, _binder.DataObject, _layout, row).ConfigureAwait(true);
            if (committed)
            {
                RefreshAndFocusRow(row);
            }
            else
            {
                // A cancelled Add removes the blank row again instead of leaving an
                // empty line in the detail table.
                table.Rows.Remove(row);
                RefreshRows();
            }
        }

        private async Task EditSelectedRowAsync()
        {
            if (_binder.DataObject is null || _layout is null) return;
            if (_grid.SelectedItem is not DataRowView rowView) return;

            var row = rowView.Row;
            var committed = await RowEditDialog.ShowAsync(this, _binder.DataObject, _layout, row).ConfigureAwait(true);
            if (committed)
                RefreshAndFocusRow(row);
        }

        // Realized text cells capture their value at template build, so a committed
        // edit form re-realizes the rows and scrolls back to the affected row.
        private void RefreshAndFocusRow(DataRow row)
        {
            RefreshRows();
            if (FindRowView(row) is not { } rowView) return;
            _grid.UpdateLayout();
            _grid.SelectedItem = rowView;
            _grid.ScrollIntoView(rowView, null);
        }

        private void OnSelectionChangedCore(object? sender, SelectionChangedEventArgs e)
        {
            var handler = RowSelected;
            if (handler is null) return;
            if (_grid.SelectedItem is not DataRowView rowView) return;
            if (!FormDataGuard.TryGetRowId(rowView.Row, out var rowId)) return;
            handler(this, rowId);
        }

        // Re-realizing replaces every row container, which drops the selection and, in a grid of bounded height,
        // scrolls back to the first row. A detail grid is re-realized after every edit that changes a computed
        // field, so without this the grid would jump to its top while the user works further down. Rows are
        // matched by reference, then by `sys_rowid`, so a save or reload that replaces the data set keeps the place.
        internal readonly record struct RowRef(DataRow Row, Guid? RowId);

        /// <summary>
        /// The rows a grid shows, top to bottom, and its selected row, so the grid, or a grid built to replace it,
        /// can return to them.
        /// </summary>
        /// <param name="Visible">The rows in view, top to bottom, each with whether its top edge is in view.</param>
        /// <param name="Selected">The selected row, if any.</param>
        internal readonly record struct RowViewport(IReadOnlyList<(RowRef Row, bool WholeTop)> Visible, RowRef? Selected);

        internal RowViewport CaptureViewport()
        {
            var visible = new List<(double Top, RowRef Row, bool WholeTop)>();
            foreach (var container in _grid.GetVisualDescendants().OfType<DataGridRow>())
            {
                if (!container.IsVisible || container.DataContext is not DataRowView view) continue;
                if (container.GetVisualParent() is not Visual presenter) continue;
                // Bounds are relative to the rows presenter, so a row scrolled partly out of view has a negative
                // top or a bottom past the presenter.
                var top = container.Bounds.Y;
                if (top + container.Bounds.Height <= 0 || top >= presenter.Bounds.Height) continue;
                if (ToRef(view.Row) is { } rowRef)
                    visible.Add((top, rowRef, top >= 0));
            }
            RowRef? selected = _grid.SelectedItem is DataRowView selectedView ? ToRef(selectedView.Row) : null;
            return new RowViewport(
                visible.OrderBy(v => v.Top).Select(v => (v.Row, v.WholeTop)).ToList(), selected);
        }

        /// <summary>
        /// Returns a grid that is not in a window yet, such as one a render just built, to
        /// <paramref name="viewport"/> once it is: it has no rows to scroll to before then.
        /// </summary>
        internal void RestoreViewportWhenShown(RowViewport viewport)
        {
            if (_grid.IsAttachedToVisualTree())
            {
                RestoreViewport(viewport);
                return;
            }

            void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
            {
                _grid.AttachedToVisualTree -= OnAttached;
                // Posted: the attach happens inside a layout pass, which `RestoreViewport` would re-enter.
                Dispatcher.UIThread.Post(() => RestoreViewport(viewport), DispatcherPriority.Loaded);
            }
            _grid.AttachedToVisualTree += OnAttached;
        }

        internal void RestoreViewport(RowViewport viewport)
        {
            if (viewport.Visible.Count > 0)
            {
                // `ScrollIntoView` measures against realized rows, and a new ItemsSource has none until the next
                // layout pass.
                _grid.UpdateLayout();
                // Scroll the lowest row still in the table to the bottom edge, then the highest one whose top was
                // in view back to the top edge. A row that is gone, such as one just deleted, is passed over for
                // its neighbour; a partial top row is skipped, since scrolling it fully into view would shift
                // the grid.
                var last = viewport.Visible.Reverse().Select(v => FindRowView(v.Row)).FirstOrDefault(v => v is not null);
                if (last is not null)
                    _grid.ScrollIntoView(last, null);
                var first = viewport.Visible.Where(v => v.WholeTop).Select(v => FindRowView(v.Row)).FirstOrDefault(v => v is not null);
                if (first is not null)
                    _grid.ScrollIntoView(first, null);
            }
            if (viewport.Selected is { } selected && FindRowView(selected) is { } selectedView)
                _grid.SelectedItem = selectedView;
        }

        private static RowRef? ToRef(DataRow row)
        {
            // A deleted row's values cannot be read, and it is not shown again anyway.
            if (row.RowState is DataRowState.Deleted or DataRowState.Detached) return null;
            return new RowRef(row, FormDataGuard.TryGetRowId(row, out var rowId) ? rowId : null);
        }

        private DataRowView? FindRowView(DataRow row) => FindRowView(new RowRef(row, null));

        private DataRowView? FindRowView(RowRef rowRef)
        {
            if (_dataTable is null) return null;
            var views = _dataTable.DefaultView.Cast<DataRowView>();
            return views.FirstOrDefault(v => ReferenceEquals(v.Row, rowRef.Row))
                ?? (rowRef.RowId is { } rowId
                    ? views.FirstOrDefault(v => FormDataGuard.TryGetRowId(v.Row, out var id) && id == rowId)
                    : null);
        }
    }
}
