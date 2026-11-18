using System.Data;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Polhem.Api.Contracts.Form;
using Polhem.Core;
using Polhem.Definition.Language;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// The saved user queries of a form at the top of the query pane: a picker that applies one, a marker when the
    /// applied one was changed, and a menu to save, save as, rename, share, delete and make one the default.
    /// </summary>
    /// <remarks>
    /// The bar talks to the backend through <see cref="IUserQueryConnector"/> and knows nothing of the criteria: the
    /// host gives it <see cref="SerializeCurrent"/> to read the conditions on screen as stored text, applies a query on
    /// <see cref="ApplyRequested"/>, and calls <see cref="RefreshModified"/> when the conditions change.
    /// </remarks>
    internal sealed partial class UserQueryBar : UserControl
    {
        private readonly IUserQueryConnector _connector;
        private readonly ComboBox _picker;
        private readonly TextBlock _modifiedMarker;
        private readonly Button _actionsButton;
        private readonly MenuFlyout _actions = new();
        private IReadOnlyList<UserQueryEntry> _entries = [];
        private Guid _defaultRowId;
        private string _baseline = string.Empty;
        private bool _syncing;

        /// <summary>Initializes a new instance of <see cref="UserQueryBar"/>.</summary>
        /// <param name="connector">The form's user-query connector.</param>
        /// <param name="serializeCurrent">Reads the conditions on screen as stored criteria text.</param>
        public UserQueryBar(IUserQueryConnector connector, Func<string> serializeCurrent)
        {
            _connector = connector ?? throw new ArgumentNullException(nameof(connector));
            SerializeCurrent = serializeCurrent ?? throw new ArgumentNullException(nameof(serializeCurrent));

            _picker = new ComboBox
            {
                PlaceholderText = UIText.Get(PolhemUIText.UserQueryNone),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _picker.PropertyChanged += (_, e) =>
            {
                if (e.Property != SelectingItemsControl.SelectedIndexProperty || _syncing) return;
                if (_picker.SelectedIndex >= 0 && _picker.SelectedIndex < _entries.Count)
                    ApplyRequested?.Invoke(this, _entries[_picker.SelectedIndex]);
            };

            _modifiedMarker = new TextBlock
            {
                Text = "*",
                IsVisible = false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0),
            };
            ToolTip.SetTip(_modifiedMarker, UIText.Get(PolhemUIText.UserQueryModified));

            _actionsButton = new Button { Content = "⋯", Flyout = _actions, Margin = new Thickness(4, 0, 0, 0) };
            string actionsName = UIText.Get(PolhemUIText.UserQueryActions);
            global::Avalonia.Automation.AutomationProperties.SetName(_actionsButton, actionsName);
            ToolTip.SetTip(_actionsButton, actionsName);
            _actions.Opening += (_, _) => BuildActions();

            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(_actionsButton, Dock.Right);
            DockPanel.SetDock(_modifiedMarker, Dock.Right);
            row.Children.Add(_actionsButton);
            row.Children.Add(_modifiedMarker);
            row.Children.Add(_picker);
            Content = row;
        }

        /// <summary>Raised when the user picks a query to apply.</summary>
        public event EventHandler<UserQueryEntry>? ApplyRequested;

        /// <summary>Raised when a call to the backend fails.</summary>
        public event EventHandler<Exception>? Failed;

        /// <summary>Gets the delegate that reads the conditions on screen as stored criteria text.</summary>
        public Func<string> SerializeCurrent { get; }

        /// <summary>Gets the queries the user can apply, their own first.</summary>
        public IReadOnlyList<UserQueryEntry> Entries => _entries;

        /// <summary>Gets the applied query, or <c>null</c>.</summary>
        public UserQueryEntry? Current { get; private set; }

        /// <summary>Gets the <c>sys_rowid</c> of the user's default query, or <see cref="Guid.Empty"/>.</summary>
        public Guid DefaultRowId => _defaultRowId;

        /// <summary>Gets a value indicating whether the conditions differ from the applied query as it was applied.</summary>
        public bool IsModified => _modifiedMarker.IsVisible;

        /// <summary>Gets the texts the picker lists, for tests.</summary>
        internal IReadOnlyList<string> PickerTexts => _picker.ItemsSource as IReadOnlyList<string> ?? [];

        /// <summary>
        /// Reads the queries from the backend and lists them, keeping the applied one when it still exists.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>The user's default query, or <c>null</c>.</returns>
        public async Task<UserQueryEntry?> LoadAsync(CancellationToken cancellationToken = default)
        {
            var response = await _connector.GetUserQueriesAsync(cancellationToken).ConfigureAwait(true);
            _entries = ReadEntries(response.Table);
            _defaultRowId = response.DefaultRowId;
            var current = Current is null ? null : _entries.FirstOrDefault(e => e.RowId == Current.RowId);
            ShowEntries(current);
            return _entries.FirstOrDefault(e => e.RowId == _defaultRowId);
        }

        /// <summary>
        /// Marks <paramref name="entry"/> as the applied query, with the conditions on screen as it was applied.
        /// </summary>
        /// <param name="entry">The applied query, or <c>null</c> for none.</param>
        public void SetCurrent(UserQueryEntry? entry)
        {
            ShowEntries(entry);
            _baseline = entry is null ? string.Empty : SerializeCurrent();
            RefreshModified();
        }

        /// <summary>Shows or hides the changed marker for the conditions on screen.</summary>
        public void RefreshModified()
            => _modifiedMarker.IsVisible = Current is not null && !string.Equals(SerializeCurrent(), _baseline, StringComparison.Ordinal);

        private void ShowEntries(UserQueryEntry? current)
        {
            _syncing = true;
            try
            {
                Current = current;
                _picker.ItemsSource = _entries.Select(EntryText).ToArray();
                _picker.SelectedIndex = current is null ? -1 : IndexOf(current.RowId);
            }
            finally
            {
                _syncing = false;
            }
        }

        private int IndexOf(Guid rowId)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].RowId == rowId) return i;
            return -1;
        }

        private string EntryText(UserQueryEntry entry)
        {
            var markers = new List<string>(2);
            if (entry.IsShared) markers.Add(UIText.Get(PolhemUIText.UserQueryShared));
            if (entry.RowId == _defaultRowId) markers.Add(UIText.Get(PolhemUIText.UserQueryDefault));
            return markers.Count == 0
                ? entry.Name
                : string.Format(CultureInfo.CurrentCulture, "{0} ({1})", entry.Name, string.Join(", ", markers));
        }

        private static List<UserQueryEntry> ReadEntries(DataTable? table)
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
