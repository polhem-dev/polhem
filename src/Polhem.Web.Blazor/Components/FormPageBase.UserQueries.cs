using System.Data;
using System.Globalization;
using Polhem.Core;
using Polhem.Definition.Language;
using Polhem.UI.Core.Criteria;
using Polhem.Web.Blazor.Controls;

namespace Polhem.Web.Blazor.Components
{
    /// <summary>
    /// The list's saved queries: reading them, applying the user's default when the list opens, applying a picked one,
    /// and the calls behind the bar's menu.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A query is stored as the text <see cref="UserQueryCodec"/> writes. Applying one replaces the conditions and runs
    /// the query; a query whose text cannot be read leaves the conditions as they were, and conditions on fields the
    /// user can no longer query are dropped. Both are named in the pane. A query is marked as changed while the
    /// conditions on screen differ from it as it was applied.
    /// </para>
    /// <para>
    /// Save as and Rename ask for a name, and Delete asks first, through the dialogs the UI package renders while
    /// <see cref="IsNamePromptOpen"/> or <see cref="PendingUserQueryDelete"/> says so. A failed call is a notice, and the
    /// list stays as it was. Saved queries that cannot be read when the list opens are a notice too, and the list opens
    /// without them. The rules match the Avalonia <c>ListView</c>.
    /// </para>
    /// </remarks>
    public abstract partial class FormPageBase
    {
        /// <summary>The longest name a saved query may have; the backend refuses a longer one.</summary>
        protected const int UserQueryNameMaxLength = 100;

        private bool _hasUserQueries;
        private IReadOnlyList<UserQueryEntry> _userQueries = [];
        private Guid _defaultUserQueryRowId;
        private UserQueryEntry? _currentUserQuery;
        private string _userQueryBaseline = string.Empty;
        private string? _criteriaNotice;
        private bool _isRenamingUserQuery;
        private bool _isNamePromptOpen;
        private UserQueryEntry? _pendingUserQueryDelete;

        /// <summary>Gets a value indicating whether the pane offers saved queries: they were read when the list opened.</summary>
        protected bool ShowsUserQueries => _hasUserQueries;

        /// <summary>Gets the saved queries the user can apply, their own first.</summary>
        protected IReadOnlyList<UserQueryEntry> UserQueries => _userQueries;

        /// <summary>Gets the applied saved query, or <c>null</c>.</summary>
        protected UserQueryEntry? CurrentUserQuery => _currentUserQuery;

        /// <summary>Gets the <c>sys_rowid</c> of the user's default query, or <see cref="Guid.Empty"/>.</summary>
        protected Guid DefaultUserQueryRowId => _defaultUserQueryRowId;

        /// <summary>Gets a value indicating whether the conditions differ from the applied query as it was applied.</summary>
        protected bool IsUserQueryModified
            => _currentUserQuery is not null && !string.Equals(UserQueryCodec.Serialize(_criteria), _userQueryBaseline, StringComparison.Ordinal);

        /// <summary>Gets what the pane should tell the user about the last applied query, or <c>null</c>.</summary>
        protected string? CriteriaNotice => _criteriaNotice;

        /// <summary>Gets a value indicating whether the name prompt of Save as or Rename is open.</summary>
        protected bool IsNamePromptOpen => _isNamePromptOpen;

        /// <summary>Gets the localized title of the open name prompt.</summary>
        protected string NamePromptTitle => Text(_isRenamingUserQuery ? PolhemUIText.UserQueryRename : PolhemUIText.UserQuerySaveAs);

        /// <summary>Gets the name the open name prompt starts with.</summary>
        protected string NamePromptName => _currentUserQuery?.Name ?? string.Empty;

        /// <summary>Gets the caption of the share option of the open name prompt, or <c>null</c> when it offers none.</summary>
        protected string? NamePromptOptionText => _isRenamingUserQuery ? null : Text(PolhemUIText.UserQueryShareOption);

        /// <summary>Gets the saved query the delete question is open for, or <c>null</c>.</summary>
        protected UserQueryEntry? PendingUserQueryDelete => _pendingUserQueryDelete;

        /// <summary>Gets the localized delete question for <see cref="PendingUserQueryDelete"/>.</summary>
        protected string UserQueryDeletePrompt => string.Format(CultureInfo.CurrentCulture, Text(PolhemUIText.UserQueryDeletePrompt),
            _pendingUserQueryDelete?.Name ?? string.Empty);

        /// <summary>
        /// Replaces the conditions with a saved query's and runs the query, telling the user what no longer applies.
        /// </summary>
        /// <param name="entry">The saved query.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task ApplyUserQueryAsync(UserQueryEntry entry, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entry);
            if (IsListLocked) return;
            if (!ApplyUserQuery(entry)) return;
            var notice = _criteriaNotice;
            await RunQueryAsync(cancellationToken).ConfigureAwait(true);
            // A query that dropped conditions keeps the pane in view, so the note about them is read.
            if (notice is null) return;
            _criteriaNotice = notice;
            _isQueryPaneOpen = true;
        }

        /// <summary>Overwrites the applied query, which the user created, with the conditions on screen.</summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected Task SaveUserQueryAsync(CancellationToken cancellationToken = default)
        {
            if (_currentUserQuery is not { IsOwn: true } current) return Task.CompletedTask;
            return RunGuardedAsync(async () =>
            {
                string criteria = UserQueryCodec.Serialize(_criteria);
                await Client.Form(ProgId).SaveUserQueryAsync(current.RowId, current.Name, current.IsShared, criteria, cancellationToken)
                    .ConfigureAwait(true);
                await ReloadUserQueriesAsync(current.RowId, cancellationToken).ConfigureAwait(true);
                _userQueryBaseline = criteria;
            });
        }

        /// <summary>Opens the name prompt that saves the conditions as a new query.</summary>
        protected void BeginSaveUserQueryAs()
        {
            _isRenamingUserQuery = false;
            _isNamePromptOpen = true;
        }

        /// <summary>Opens the name prompt that renames the applied query, which the user created.</summary>
        protected void BeginRenameUserQuery()
        {
            if (_currentUserQuery is not { IsOwn: true }) return;
            _isRenamingUserQuery = true;
            _isNamePromptOpen = true;
        }

        /// <summary>Closes the name prompt without saving.</summary>
        protected void CancelNamePrompt() => _isNamePromptOpen = false;

        /// <summary>Saves under the name the user accepted: a new query after Save as, or the new name after Rename.</summary>
        /// <param name="answer">The accepted name, and whether to share a new query.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected Task AcceptNamePromptAsync(NamePromptAnswer answer, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(answer);
            if (!_isNamePromptOpen) return Task.CompletedTask;
            _isNamePromptOpen = false;
            var connector = Client.Form(ProgId);
            if (_isRenamingUserQuery)
            {
                if (_currentUserQuery is not { IsOwn: true } current) return Task.CompletedTask;
                // A rename keeps what the query stores, not the conditions on screen.
                return RunGuardedAsync(async () =>
                {
                    await connector.SaveUserQueryAsync(current.RowId, answer.Name, current.IsShared, current.Criteria, cancellationToken)
                        .ConfigureAwait(true);
                    await ReloadUserQueriesAsync(current.RowId, cancellationToken).ConfigureAwait(true);
                });
            }
            return RunGuardedAsync(async () =>
            {
                string criteria = UserQueryCodec.Serialize(_criteria);
                var response = await connector.SaveUserQueryAsync(Guid.Empty, answer.Name, answer.IsOptionChecked, criteria, cancellationToken)
                    .ConfigureAwait(true);
                await ReloadUserQueriesAsync(response.RowId, cancellationToken).ConfigureAwait(true);
                _userQueryBaseline = criteria;
            });
        }

        /// <summary>Shares the applied query, which the user created, with the company, or stops sharing it.</summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected Task ToggleUserQueryShareAsync(CancellationToken cancellationToken = default)
        {
            if (_currentUserQuery is not { IsOwn: true } current) return Task.CompletedTask;
            return RunGuardedAsync(async () =>
            {
                await Client.Form(ProgId).SaveUserQueryAsync(current.RowId, current.Name, !current.IsShared, current.Criteria, cancellationToken)
                    .ConfigureAwait(true);
                await ReloadUserQueriesAsync(current.RowId, cancellationToken).ConfigureAwait(true);
            });
        }

        /// <summary>Makes the applied query the one the list opens with, or stops it being so.</summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected Task ToggleDefaultUserQueryAsync(CancellationToken cancellationToken = default)
        {
            if (_currentUserQuery is not { } current) return Task.CompletedTask;
            var rowId = current.RowId == _defaultUserQueryRowId ? Guid.Empty : current.RowId;
            return RunGuardedAsync(async () =>
            {
                await Client.Form(ProgId).SetDefaultUserQueryAsync(rowId, cancellationToken).ConfigureAwait(true);
                await ReloadUserQueriesAsync(current.RowId, cancellationToken).ConfigureAwait(true);
            });
        }

        /// <summary>Opens the question before the applied query, which the user created, is deleted.</summary>
        protected void BeginDeleteUserQuery()
        {
            if (_currentUserQuery is { IsOwn: true } current) _pendingUserQueryDelete = current;
        }

        /// <summary>Closes the delete question and keeps the query.</summary>
        protected void CancelDeleteUserQuery() => _pendingUserQueryDelete = null;

        /// <summary>Deletes the query the question was asked about; the conditions stay, applied to no query.</summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected Task ConfirmDeleteUserQueryAsync(CancellationToken cancellationToken = default)
        {
            if (_pendingUserQueryDelete is not { } query) return Task.CompletedTask;
            _pendingUserQueryDelete = null;
            return RunGuardedAsync(async () =>
            {
                await Client.Form(ProgId).DeleteUserQueryAsync(query.RowId, cancellationToken).ConfigureAwait(true);
                await ReloadUserQueriesAsync(Guid.Empty, cancellationToken).ConfigureAwait(true);
            });
        }

        private void ResetUserQueries()
        {
            _hasUserQueries = false;
            _userQueries = [];
            _defaultUserQueryRowId = Guid.Empty;
            _currentUserQuery = null;
            _userQueryBaseline = string.Empty;
            _criteriaNotice = null;
            _isNamePromptOpen = false;
            _pendingUserQueryDelete = null;
        }

        // Reads the saved queries when the list opens and applies the user's default, so the first page is read with it.
        private async Task LoadUserQueriesAsync(int version)
        {
            if (!SupportsQueryPane || _criteriaFields.Count == 0) return;
            try
            {
                var response = await Client.Form(ProgId).GetUserQueriesAsync().ConfigureAwait(true);
                if (version != _loadVersion) return;
                _userQueries = ReadUserQueries(response.Table);
                _defaultUserQueryRowId = response.DefaultRowId;
                _hasUserQueries = true;
            }
            catch (Exception ex)
            {
                // Boundary: saved queries that cannot be read are a notice, and the list still opens without them.
                if (version == _loadVersion) _notice = ex.Message;
                return;
            }
            if (_userQueries.FirstOrDefault(e => e.RowId == _defaultUserQueryRowId) is { } defaultQuery && ApplyUserQuery(defaultQuery))
                ApplyCriteriaFilter();
        }

        private bool ApplyUserQuery(UserQueryEntry entry)
        {
            var loaded = UserQueryCodec.Deserialize(entry.Criteria, _criteriaFields);
            if (!loaded.IsSupported)
            {
                _currentUserQuery = null;
                _criteriaNotice = Text(PolhemUIText.UserQueryUnreadable);
                _isQueryPaneOpen = true;
                return false;
            }
            _criteria = loaded.Criteria;
            _currentUserQuery = entry;
            _userQueryBaseline = UserQueryCodec.Serialize(_criteria);
            _criteriaNotice = loaded.Skipped.Count == 0 ? null : SkippedText(loaded.Skipped);
            return true;
        }

        private string SkippedText(IReadOnlyList<UserQuerySkip> skipped)
        {
            var names = skipped.Select(skip =>
                _criteriaFields.FirstOrDefault(f => string.Equals(f.FieldName, skip.FieldName, StringComparison.OrdinalIgnoreCase))?.Caption
                ?? skip.FieldName);
            return string.Format(CultureInfo.CurrentCulture, Text(PolhemUIText.UserQuerySkipped),
                string.Join(Text(PolhemUIText.CriteriaValueSeparator), names.Distinct(StringComparer.Ordinal)));
        }

        private async Task ReloadUserQueriesAsync(Guid currentRowId, CancellationToken cancellationToken)
        {
            var response = await Client.Form(ProgId).GetUserQueriesAsync(cancellationToken).ConfigureAwait(true);
            _userQueries = ReadUserQueries(response.Table);
            _defaultUserQueryRowId = response.DefaultRowId;
            _currentUserQuery = _userQueries.FirstOrDefault(e => e.RowId == currentRowId);
        }

        private static List<UserQueryEntry> ReadUserQueries(DataTable? table)
        {
            var entries = new List<UserQueryEntry>();
            if (table is null) return entries;
            foreach (DataRow row in table.Rows)
            {
                entries.Add(new UserQueryEntry(
                    ValueUtilities.CGuid(row["sys_rowid"]),
                    ValueUtilities.CStr(row["sys_name"]),
                    ValueUtilities.CBool(row["is_shared"]),
                    ValueUtilities.CBool(row["is_own"]),
                    ValueUtilities.CStr(row["criteria"])));
            }
            return entries;
        }
    }
}
