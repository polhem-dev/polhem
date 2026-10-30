using System.Data;
using Avalonia.Controls;
using Polhem.Definition;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Core.Connectors;
using Polhem.UI.Core.Permissions;

namespace Polhem.UI.Avalonia.Views
{
    /// <summary>
    /// Grid-wiring and toolbar-command half of <see cref="ListView"/>. Split out for file size only;
    /// behaviour is unchanged.
    /// </summary>
    public partial class ListView
    {
        private void AttachGrid()
        {
            ClearError();
            _loadingLabel.IsVisible = false;

            var listLayout = Schema!.GetListLayout();
            // Degrade the freshly generated list layout before binding: sensitive columns the user
            // cannot Read drop out. No-op when no company context is active.
            LayoutCapabilityApplier.ApplyGrid(listLayout, Schema, ResolveUserContext()?.Capabilities);
            // Columns render immediately; rows arrive with the first ReloadAsync. The master table
            // lets drop-down columns show their (localized) list item text instead of stored codes.
            _grid.Bind(listLayout, rows: null, Schema.MasterTable);

            var columns = (listLayout.Columns ?? Enumerable.Empty<LayoutColumn>())
                .Where(c => c.Visible)
                .ToArray();
            _cardList.ItemTemplate = BuildCardTemplate(columns, _grid.FormatColumnText);
        }

        private string ComputeSelectFields()
            => Schema is { } schema ? ListSelectFields.For(schema) : string.Empty;

        private void OnRowSelected(Guid rowId)
        {
            _selectedRowId = rowId;
            UpdateToolbarState();
        }

        private void OnGridDoubleTapped()
        {
            // Opening a row from the list is a read-only View; editing is the explicit Edit
            // action (toolbar button), so clicking through the list never mutates by accident.
            if (_selectedRowId != Guid.Empty)
                ViewRequested?.Invoke(this, _selectedRowId);
        }

        private void OnViewClicked()
        {
            if (_selectedRowId != Guid.Empty)
                ViewRequested?.Invoke(this, _selectedRowId);
        }

        private void OnEditClicked()
        {
            if (_selectedRowId != Guid.Empty)
                EditRequested?.Invoke(this, _selectedRowId);
        }

        private void OnNewClicked()
        {
            if (!_initialized || _isBusy) return;
            AddRequested?.Invoke(this, EventArgs.Empty);
        }

        private async Task OnDeleteClickedAsync()
        {
            var connector = FormConnector;
            var rowId = _selectedRowId;
            if (connector is null || rowId == Guid.Empty || _isBusy) return;

            // Busy while the question is open, so the toolbar cannot start something else behind an overlay.
            _isBusy = true;
            UpdateToolbarState();
            ClearError();
            try
            {
                if (ConfirmDelete && !await ConfirmDeleteAsync(rowId).ConfigureAwait(true)) return;
                await connector.DeleteAsync(rowId).ConfigureAwait(true);
                await ReloadAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ReportError(ex);
            }
            finally
            {
                _isBusy = false;
                UpdateToolbarState();
            }
        }

        /// <summary>
        /// Asks the user whether to delete a record, when <see cref="ConfirmDelete"/> is on. A subclass can
        /// override this to ask in its own words or its own dialog.
        /// </summary>
        /// <param name="rowId">The <see cref="SysFields.RowId"/> of the record about to be deleted.</param>
        /// <param name="cancellationToken">
        /// A token that closes an open question without deleting. A cancelled call throws
        /// <see cref="OperationCanceledException"/>.
        /// </param>
        /// <returns><c>true</c> to delete the record; <c>false</c> to leave it.</returns>
        protected virtual Task<bool> ConfirmDeleteAsync(Guid rowId, CancellationToken cancellationToken = default)
            => ConfirmDialog.ShowAsync(
                this,
                UIText.Get(PolhemUIText.Delete),
                UIText.Get(PolhemUIText.DeleteRecordPrompt),
                UIText.Get(PolhemUIText.Delete),
                UIText.Get(PolhemUIText.Cancel),
                cancellationToken);

        private void UpdateToolbarState()
        {
            var hasSelection = _selectedRowId != Guid.Empty;
            _pager.IsEnabled = !_isBusy && !_isLoading;
            _newButton.IsEnabled = _initialized && !_isBusy;
            _viewButton.IsEnabled = _initialized && !_isBusy && hasSelection;
            _editButton.IsEnabled = _initialized && !_isBusy && hasSelection;
            _deleteButton.IsEnabled = _initialized && !_isBusy && hasSelection;

            // Capability gate (independent of selection/busy): hide a command the user cannot perform.
            _viewButton.IsVisible = CanCommand(_viewButton);
            _newButton.IsVisible = CanCommand(_newButton);
            _editButton.IsVisible = CanCommand(_editButton);
            _deleteButton.IsVisible = CanCommand(_deleteButton);
        }

        // Resolves whether the button's tagged PermissionActions is permitted for the current schema
        // and cached capability snapshot. Untagged buttons and a missing schema resolve to permitted.
        private bool CanCommand(Control button)
            => Schema is null
               || ElementCapabilityResolver.Default.Can(Schema, PermissionScope.GetAction(button), ResolveUserContext()?.Capabilities);

        private void ReportError(Exception ex)
        {
            _errorLabel.Show(ex.Message);
            ErrorOccurred?.Invoke(this, ex);
        }

        private void ClearError()
        {
            _errorLabel.Clear();
        }
    }
}
