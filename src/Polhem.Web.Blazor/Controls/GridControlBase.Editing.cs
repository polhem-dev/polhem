using System.Data;
using Polhem.Definition.Layouts;
using Polhem.UI.Core;
using Polhem.UI.Core.DataObjects;
using Polhem.UI.Core.Editing;

namespace Polhem.Web.Blazor.Controls
{
    public abstract partial class GridControlBase
    {
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

        private void CloseEditorUnlessInCellEdit()
        {
            if (!InCellEdit) EditorHost.Close();
        }

        /// <summary>Selects a row and reports it through <see cref="RowSelected"/>.</summary>
        /// <param name="row">The row.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task SelectRowAsync(DataRow row, CancellationToken cancellationToken = default)
        {
            _selectedRow = row;
            if (RowSelected.HasDelegate && FormDataGuard.TryGetRowId(row, out var rowId))
                await RowSelected.InvokeAsync(rowId).ConfigureAwait(true);
        }

        /// <summary>
        /// Selects a row, opens it in the edit form when the grid edits that way, and reports it through
        /// <see cref="RowActivated"/>.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task ActivateRowAsync(DataRow row, CancellationToken cancellationToken = default)
        {
            _selectedRow = row;
            if (CanUseEditForm) OpenEditForm(row, isNew: false);
            if (RowActivated.HasDelegate && FormDataGuard.TryGetRowId(row, out var rowId))
                await RowActivated.InvokeAsync(rowId).ConfigureAwait(true);
        }

        /// <summary>Runs the toolbar's Add.</summary>
        protected void OnAddClicked()
        {
            if (!HasData) return;
            var row = AddRow();
            if (row is not null && EditMode == GridEditMode.EditForm)
                OpenEditForm(row, isNew: true);
        }

        /// <summary>Runs the toolbar's Edit.</summary>
        protected void EditSelectedRow()
        {
            if (SelectedRow is { } row && CanUseEditForm)
                OpenEditForm(row, isNew: false);
        }

        private void OpenEditForm(DataRow row, bool isNew)
        {
            _editingRow = row;
            _editingNewRow = isNew;
        }

        /// <summary>Closes the edit form after its row was committed.</summary>
        protected void OnEditCommitted()
        {
            _selectedRow = _editingRow;
            _editingRow = null;
        }

        /// <summary>Closes the edit form without committing. A cancelled Add removes the blank row again instead of leaving an empty line in the table.</summary>
        protected void OnEditCancelled()
        {
            if (_editingNewRow && _editingRow is { } row && row.Table is { } table)
            {
                table.Rows.Remove(row);
                _selectedRow = null;
            }
            _editingRow = null;
        }
    }
}
