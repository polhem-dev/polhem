using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Core;
using Polhem.UI.Core.Fields;

namespace Polhem.Web.Blazor.Components
{
    public abstract partial class FormPageBase
    {
        /// <summary>Selects a list row.</summary>
        /// <param name="rowId">The row's id.</param>
        protected void OnRowSelected(Guid rowId) => _selectedRowId = rowId;

        // Opening a row from the list is a read-only View; editing is the explicit Edit command, so clicking
        // through the list never changes a record by accident.
        /// <summary>Opens a list row read-only: editing is the explicit Edit command, so clicking through the list never changes a record by accident.</summary>
        /// <param name="rowId">The row's id.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected Task OnRowActivatedAsync(Guid rowId, CancellationToken cancellationToken = default)
        {
            _selectedRowId = rowId;
            return OpenRecordAsync(rowId, SingleFormMode.View, cancellationToken);
        }

        /// <summary>Opens the selected row read-only.</summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected Task OnViewAsync(CancellationToken cancellationToken = default)
            => _selectedRowId is { } rowId ? OpenRecordAsync(rowId, SingleFormMode.View, cancellationToken) : Task.CompletedTask;

        /// <summary>Opens the selected row for editing.</summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected Task OnEditAsync(CancellationToken cancellationToken = default)
            => _selectedRowId is { } rowId ? OpenRecordAsync(rowId, SingleFormMode.Edit, cancellationToken) : Task.CompletedTask;

        private async Task OpenRecordAsync(Guid rowId, SingleFormMode mode, CancellationToken cancellationToken)
        {
            if (_dataObject is null) return;
            await RunGuardedAsync(async () =>
            {
                await _dataObject.LoadAsync(rowId, cancellationToken).ConfigureAwait(true);
                ShowRecord(mode);
            }).ConfigureAwait(true);
        }

        /// <summary>Opens a new record.</summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task OnNewAsync(CancellationToken cancellationToken = default)
        {
            if (_dataObject is null) return;
            await RunGuardedAsync(async () =>
            {
                await _dataObject.NewAsync(cancellationToken).ConfigureAwait(true);
                // The server's GetNewData has evaluated the default-value expressions, but computed fields
                // are left to the client. The master row is populated before the event bridge attaches and
                // raises no RowAdded, so it is initialized here.
                if (_dataObject.MasterRow is { } master)
                    _liveComputation?.InitializeNewRow(_dataObject.MasterTable.TableName, master);
                ShowRecord(SingleFormMode.Add);
            }).ConfigureAwait(true);
        }

        /// <summary>Checks the required fields and saves the record, returning to the list when the save succeeds.</summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task OnSaveAsync(CancellationToken cancellationToken = default)
        {
            if (_dataObject is null || _isBusy) return;

            // Required fields are checked before the save leaves the circuit, so the user sees every
            // empty one named at once instead of the server's rejection of the first.
            var missing = _schema is { } schema
                ? RequiredFieldCheck.FindMissing(schema, _dataObject.DataSet)
                : [];
            if (missing.Count > 0)
            {
                _notice = RequiredFieldCheck.FormatPrompt(PolhemBlazorText.GetLocalizer(Services), missing);
                return;
            }

            await RunGuardedAsync(async () =>
            {
                await _dataObject.SaveAsync(cancellationToken).ConfigureAwait(true);
                if (_dataObject.MasterRow is { } master && FormDataGuard.TryGetRowId(master, out var savedRowId))
                    _selectedRowId = savedRowId;
                ShowList();
                await ReloadListAsync(cancellationToken).ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        // Cancel (Add / Edit) and Back (View) leave the record without saving; the next View, Edit or New reloads
        // the data object, so the edits are dropped with it.
        /// <summary>Leaves the record without saving. The next View, Edit or New reloads the data object, so the edits are dropped with it.</summary>
        protected void OnClose()
        {
            if (_isBusy) return;
            _notice = null;
            ShowList();
        }

        /// <summary>Deletes the selected row, after asking when <see cref="ConfirmDelete"/> is on.</summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task OnDeleteAsync(CancellationToken cancellationToken = default)
        {
            if (_selectedRowId is not { } rowId || IsListLocked) return;
            if (ConfirmDelete)
            {
                _notice = null;
                _pendingDeleteRowId = rowId;
                _focusCancelDelete = true;
                return;
            }
            await DeleteAsync(rowId, cancellationToken).ConfigureAwait(true);
        }

        /// <summary>Answers the delete question with yes.</summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task ConfirmDeleteAsync(CancellationToken cancellationToken = default)
        {
            if (_pendingDeleteRowId is not { } rowId) return;
            _pendingDeleteRowId = null;
            await DeleteAsync(rowId, cancellationToken).ConfigureAwait(true);
        }

        /// <summary>Answers the delete question with no.</summary>
        protected void CancelDelete() => _pendingDeleteRowId = null;

        private async Task DeleteAsync(Guid rowId, CancellationToken cancellationToken)
        {
            await RunGuardedAsync(async () =>
            {
                await Client.Form(ProgId).DeleteAsync(rowId, cancellationToken).ConfigureAwait(true);
                _selectedRowId = null;
                await ReloadListAsync(cancellationToken).ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        private void ShowRecord(SingleFormMode mode)
        {
            _pendingDeleteRowId = null;
            _recordMode = mode;
            _isShowingRecord = true;
        }

        private void ShowList()
        {
            _isShowingRecord = false;
            _relation = null;
        }

        /// <summary>Opens a related record over the viewed one.</summary>
        /// <param name="target">The record to open.</param>
        protected void OpenRelation(RelationTarget target)
        {
            _notice = null;
            _relation = target;
        }

        /// <summary>Closes the related record.</summary>
        protected void CloseRelation() => _relation = null;

        /// <summary>Closes the related record and tells the user why it cannot open.</summary>
        /// <param name="message">The reason.</param>
        protected void OnRelationRefused(string message)
        {
            _relation = null;
            _notice = message;
        }

        private async Task RunGuardedAsync(Func<Task> action)
        {
            if (_isBusy) return;
            _isBusy = true;
            _notice = null;
            try
            {
                await action().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                // Boundary: the form stays on screen with what the user entered, so a rejected save can be corrected.
                _notice = ex.Message;
            }
            finally
            {
                _isBusy = false;
            }
        }
    }
}
