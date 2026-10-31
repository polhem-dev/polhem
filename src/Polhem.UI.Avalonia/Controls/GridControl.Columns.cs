using System.Data;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Polhem.Definition.Layouts;
using Polhem.Definition.Sorting;
using Polhem.Core.Data;
using Polhem.Core;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// Column-building half of <see cref="GridControl"/> (rebuilding the DataGrid columns and
    /// their headers). Split out for file size only; behaviour is unchanged.
    /// </summary>
    public partial class GridControl
    {
        private void RebuildFallbackColumns()
        {
            _grid.Columns.Clear();
            if (_dataTable is null) return;
            foreach (DataColumn column in _dataTable.Columns)
                _grid.Columns.Add(BuildColumn(new LayoutColumn(column.ColumnName, column.ColumnName, ControlType.TextEdit)));
        }

        private void RebuildColumns()
        {
            _grid.Columns.Clear();
            if (_layout is null) return;
            foreach (var column in EnumerateVisibleColumns(_layout))
                _grid.Columns.Add(BuildColumn(column));
        }

        private void RebuildRows()
        {
            // Re-realizing discards any cell that hosted an inline editor.
            _endActiveInlineEdit = null;
            _grid.ItemsSource = _dataTable?.DefaultView;
        }

        private DataGridTemplateColumn BuildColumn(LayoutColumn column)
        {
            var templateColumn = new DataGridTemplateColumn
            {
                Header = BuildColumnHeader(column),
                IsReadOnly = column.ReadOnly,
            };
            if (_usesServerSort)
            {
                templateColumn.Tag = column.FieldName;
                templateColumn.CanUserSort = IsServerSortable(column);
            }

            if (TryGetLookupFormField(column) is { } lookupField)
            {
                // The lookup editor is a modal dialog: it takes focus out of the cell,
                // which would tear a CellEditingTemplate down mid-edit, so lookup
                // columns bypass the DataGrid edit pipeline entirely and open the
                // dialog from the display template instead.
                // See maintainers/adr/adr-021-avalonia-datagrid-editing-strategy.md.
                templateColumn.IsReadOnly = true;
                templateColumn.CellTemplate = new FuncDataTemplate<DataRowView>(
                    (row, _) => BuildLookupCell(row, column, lookupField),
                    supportsRecycling: false);
            }
            else if (IsAlwaysOnEditor(column.ControlType))
            {
                // Popup-based editors (ComboBox dropdown, DatePicker flyout) break
                // inside the DataGrid edit pipeline: opening the popup moves focus out
                // of the cell and the grid tears the editing template down. These
                // columns manage their own click-to-edit swap inside the display
                // template instead and bypass the edit pipeline entirely.
                // See maintainers/adr/adr-021-avalonia-datagrid-editing-strategy.md.
                templateColumn.IsReadOnly = true;
                templateColumn.CellTemplate = new FuncDataTemplate<DataRowView>(
                    (row, _) => BuildInteractiveCell(row, column),
                    supportsRecycling: false);
            }
            else
            {
                // Recycling MUST stay off: the cell Text is computed once at build time
                // (it is not a binding to the row's DataContext). With recycling on, the
                // DataGrid reuses a presenter across rows and the stale Text no longer
                // matches the underlying DataRowView — so the displayed value diverges
                // from the row, and a lookup pick returns a different row than shown.
                templateColumn.CellTemplate = new FuncDataTemplate<DataRowView>(
                    (row, _) => new TextBlock
                    {
                        Text = FormatColumnText(row, column),
                        Margin = new Thickness(8, 4),
                    },
                    supportsRecycling: false);
                // Recycling is off: each edit session gets a fresh editor whose change
                // handlers close over the row being edited.
                templateColumn.CellEditingTemplate = new FuncDataTemplate<DataRowView>(
                    (row, _) => BuildCellEditor(row, column),
                    supportsRecycling: false);
            }

            if (column.Width > 0)
            {
                // LayoutColumn.Width is in CSS pixels on the Blazor side; treating it as
                // device-independent units gives an equivalent column-width hint here.
                templateColumn.Width = new DataGridLength(column.Width);
            }
            else
            {
                templateColumn.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            }

            return templateColumn;
        }

        private bool _usesServerSort;
        private string? _sortFieldName;
        private SortDirection _sortDirection;

        // The grid has no sort of its own in this mode, so the mark is part of the caption.
        private string SortMark(LayoutColumn column)
        {
            if (_sortFieldName is null || !StringUtilities.IsEquals(_sortFieldName, column.FieldName)) return string.Empty;
            return _sortDirection == SortDirection.Desc ? " \u25BC" : " \u25B2";
        }

        // Oracle cannot order by a `Text` column, and a binary value has no meaningful order.
        private bool IsServerSortable(LayoutColumn column)
            => _listFormTable?.Fields is { } fields
                && fields.Contains(column.FieldName)
                && fields[column.FieldName].DbType is not (FieldDbType.Text or FieldDbType.Binary);

        /// <summary>
        /// Makes a click on a column header raise <see cref="SortRequested"/> instead of sorting the rows on screen,
        /// which are only one page. Call it before binding, because it decides how the columns are built.
        /// </summary>
        internal void EnableServerSort()
        {
            _usesServerSort = true;
            _grid.Sorting += (_, e) =>
            {
                e.Handled = true;
                if (e.Column.Tag is string fieldName)
                    SortRequested?.Invoke(this, fieldName);
            };
        }

        /// <summary>Raised when the user clicks a sortable header. Carries the field name.</summary>
        internal event EventHandler<string>? SortRequested;

        /// <summary>Shows which field the rows are sorted by, or none when <paramref name="fieldName"/> is <c>null</c>.</summary>
        /// <param name="fieldName">The sorted field.</param>
        /// <param name="direction">The direction of the sort.</param>
        internal void SetSortIndicator(string? fieldName, SortDirection direction)
        {
            _sortFieldName = fieldName;
            _sortDirection = direction;
            if (_layout is null) return;

            // The headers are replaced rather than the columns rebuilt, which would lose any width the user set.
            var columns = EnumerateVisibleColumns(_layout).ToList();
            for (var i = 0; i < columns.Count && i < _grid.Columns.Count; i++)
                _grid.Columns[i].Header = BuildColumnHeader(columns[i]);
        }

        // Read-only / required columns look identical to plain ones at rest, so the header marks
        // the cue: a read-only column's caption is parenthesised (e.g. "(Amount)"), a required
        // column's caption is blue (see FieldCaptionStyle). Keyed on the layout ReadOnly flag, NOT
        // the DataGrid column IsReadOnly: lookup and popup-editor columns set IsReadOnly to bypass
        // the edit pipeline while staying editable through click-to-swap, so they must not show
        // the read-only cue. In a bound detail grid the schema's Required counts as well as the
        // layout's, because the server enforces the schema's; a list grid shows no required cue
        // from the schema, since nothing is entered there.
        private object BuildColumnHeader(LayoutColumn column)
        {
            var caption = FieldCaptionStyle.FormatCaption(column.Caption, column.ReadOnly) + SortMark(column);
            var required = column.Required
                || (_binder.DataObject is { } dataObject
                    && dataObject.GetFormField(TableName, column.FieldName)?.Required == true);
            if (FieldCaptionStyle.GetCaptionForeground(column.ReadOnly, required) is null)
                return caption;
            var header = new TextBlock { Text = caption };
            FieldCaptionStyle.ApplyCaptionForeground(header, column.ReadOnly, required);
            return header;
        }
    }
}
