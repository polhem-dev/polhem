using System.Globalization;
using Avalonia.Controls;
using Polhem.Definition.Language;
using Polhem.UI.Avalonia.Controls.Editors;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// The actions menu of <see cref="UserQueryBar"/>, and the backend calls behind it.
    /// </summary>
    internal sealed partial class UserQueryBar
    {
        /// <summary>The longest name a user query may have; the backend refuses a longer one.</summary>
        internal const int NameMaxLength = 100;

        /// <summary>Overwrites the applied query, which the user created, with the conditions on screen.</summary>
        internal Task SaveAsync()
        {
            var current = Current;
            if (current is not { IsOwn: true }) return Task.CompletedTask;
            return RunAsync(async () =>
            {
                string criteria = SerializeCurrent();
                await _connector.SaveUserQueryAsync(current.RowId, current.Name, current.IsShared, criteria).ConfigureAwait(true);
                await ReloadAsync(current.RowId).ConfigureAwait(true);
                _baseline = criteria;
                RefreshModified();
            });
        }

        /// <summary>Saves the conditions on screen as a new query and applies it.</summary>
        /// <param name="name">The name.</param>
        /// <param name="isShared">Whether every user of the company may apply it.</param>
        internal Task SaveAsAsync(string name, bool isShared) => RunAsync(async () =>
        {
            string criteria = SerializeCurrent();
            var response = await _connector.SaveUserQueryAsync(Guid.Empty, name, isShared, criteria).ConfigureAwait(true);
            await ReloadAsync(response.RowId).ConfigureAwait(true);
            _baseline = criteria;
            RefreshModified();
        });

        /// <summary>Renames the applied query, keeping what it stores rather than the conditions on screen.</summary>
        /// <param name="name">The new name.</param>
        internal Task RenameAsync(string name)
        {
            var current = Current;
            if (current is not { IsOwn: true }) return Task.CompletedTask;
            return RunAsync(async () =>
            {
                await _connector.SaveUserQueryAsync(current.RowId, name, current.IsShared, current.Criteria).ConfigureAwait(true);
                await ReloadAsync(current.RowId).ConfigureAwait(true);
            });
        }

        /// <summary>Shares the applied query with the company, or stops sharing it.</summary>
        internal Task ToggleShareAsync()
        {
            var current = Current;
            if (current is not { IsOwn: true }) return Task.CompletedTask;
            return RunAsync(async () =>
            {
                await _connector.SaveUserQueryAsync(current.RowId, current.Name, !current.IsShared, current.Criteria).ConfigureAwait(true);
                await ReloadAsync(current.RowId).ConfigureAwait(true);
            });
        }

        /// <summary>Deletes the applied query; the conditions stay on screen, applied to no query.</summary>
        internal Task DeleteAsync()
        {
            var current = Current;
            if (current is not { IsOwn: true }) return Task.CompletedTask;
            return RunAsync(async () =>
            {
                await _connector.DeleteUserQueryAsync(current.RowId).ConfigureAwait(true);
                await ReloadAsync(Guid.Empty).ConfigureAwait(true);
                RefreshModified();
            });
        }

        /// <summary>Makes the applied query the one the list opens with, or stops it being so.</summary>
        internal Task ToggleDefaultAsync()
        {
            var current = Current;
            if (current is null) return Task.CompletedTask;
            var rowId = current.RowId == _defaultRowId ? Guid.Empty : current.RowId;
            return RunAsync(async () =>
            {
                await _connector.SetDefaultUserQueryAsync(rowId).ConfigureAwait(true);
                await ReloadAsync(current.RowId).ConfigureAwait(true);
            });
        }

        private async Task ReloadAsync(Guid currentRowId)
        {
            var response = await _connector.GetUserQueriesAsync().ConfigureAwait(true);
            _entries = ReadEntries(response.Table);
            _defaultRowId = response.DefaultRowId;
            ShowEntries(_entries.FirstOrDefault(e => e.RowId == currentRowId));
        }

        private async Task RunAsync(Func<Task> action)
        {
            _actionsButton.IsEnabled = false;
            try
            {
                await action().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                // Boundary: a menu action; a failed call is reported by the host and the list stays as it was.
                Failed?.Invoke(this, ex);
            }
            finally
            {
                _actionsButton.IsEnabled = true;
            }
        }

        private void BuildActions()
        {
            _actions.Items.Clear();
            var current = Current;
            bool own = current is { IsOwn: true };
            Add(PolhemUIText.UserQuerySave, own, () => _ = SaveAsync());
            Add(PolhemUIText.UserQuerySaveAs, true, () => _ = RunAsync(PromptSaveAsAsync));
            Add(PolhemUIText.UserQueryRename, own, () => _ = RunAsync(PromptRenameAsync));
            Add(current is { IsShared: true } ? PolhemUIText.UserQueryUnshare : PolhemUIText.UserQueryShare, own,
                () => _ = ToggleShareAsync());
            Add(current is not null && current.RowId == _defaultRowId ? PolhemUIText.UserQueryClearDefault : PolhemUIText.UserQuerySetDefault,
                current is not null, () => _ = ToggleDefaultAsync());
            Add(PolhemUIText.UserQueryDelete, own, () => _ = RunAsync(PromptDeleteAsync));

            void Add(string key, bool enabled, Action click)
            {
                var item = new MenuItem { Header = UIText.Get(key), IsEnabled = enabled };
                item.Click += (_, _) => click();
                _actions.Items.Add(item);
            }
        }

        private async Task PromptSaveAsAsync()
        {
            var answer = await NamePromptDialog.ShowAsync(this, UIText.Get(PolhemUIText.UserQuerySaveAs),
                UIText.Get(PolhemUIText.UserQueryName), Current?.Name ?? string.Empty, NameMaxLength,
                UIText.Get(PolhemUIText.UserQueryShareOption), optionChecked: false).ConfigureAwait(true);
            if (answer is { } entered)
                await SaveAsAsync(entered.Name, entered.Option).ConfigureAwait(true);
        }

        private async Task PromptRenameAsync()
        {
            if (Current is not { } current) return;
            var answer = await NamePromptDialog.ShowAsync(this, UIText.Get(PolhemUIText.UserQueryRename),
                UIText.Get(PolhemUIText.UserQueryName), current.Name, NameMaxLength, optionText: null, optionChecked: false)
                .ConfigureAwait(true);
            if (answer is { } entered)
                await RenameAsync(entered.Name).ConfigureAwait(true);
        }

        private async Task PromptDeleteAsync()
        {
            if (Current is not { } current) return;
            bool confirmed = await ConfirmDialog.ShowAsync(this, UIText.Get(PolhemUIText.UserQueryDelete),
                string.Format(CultureInfo.CurrentCulture, UIText.Get(PolhemUIText.UserQueryDeletePrompt), current.Name),
                UIText.Get(PolhemUIText.Delete), UIText.Get(PolhemUIText.Cancel)).ConfigureAwait(true);
            if (confirmed)
                await DeleteAsync().ConfigureAwait(true);
        }
    }
}
