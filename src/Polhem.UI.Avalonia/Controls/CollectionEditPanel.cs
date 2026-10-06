using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Polhem.Definition.Language;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// The content of <see cref="CollectionEditDialog"/>: the list of items with Add, Delete, Move up and Move down, a
    /// <see cref="PropertyGridControl"/> for the selected item, and OK and Cancel.
    /// </summary>
    /// <remarks>
    /// The panel edits the working list of a <see cref="CollectionEditSession"/>. OK writes it back when
    /// <see cref="CollectionEditSession.Validate"/> finds nothing, and otherwise shows the reason and marks the items.
    /// Removing the panel from the tree without OK or Cancel, as closing the dialog's window does, cancels.
    /// </remarks>
    internal sealed class CollectionEditPanel : UserControl
    {
        /// <summary>The marker shown before the label of an item whose key is missing or repeated.</summary>
        internal const string InvalidMarker = "⚠ ";

        private const double ListWidth = 220;
        private const double CompactListHeight = 140;
        private const double CompactGridHeight = 280;

        private readonly Func<string, string>? _translator;
        private readonly ListBox _list;
        private readonly PropertyGridControl _grid;
        private readonly TextBlock _message;
        private readonly Button _addButton;
        private readonly Button _removeButton;
        private readonly Button _upButton;
        private readonly Button _downButton;
        private bool _refreshing;
        private bool _finished;

        /// <summary>
        /// Builds the panel for <paramref name="session"/>.
        /// </summary>
        /// <param name="session">The edit to show.</param>
        /// <param name="translator">The translator for the labels of the item grid.</param>
        /// <param name="collectionEditorProvider">The provider the item grid asks before it edits a nested collection.</param>
        /// <param name="compact">Whether to stack the list above the grid, for a narrow screen.</param>
        internal CollectionEditPanel(CollectionEditSession session, Func<string, string>? translator,
            Func<CollectionEditContext, Task<bool>?>? collectionEditorProvider, bool compact)
        {
            ArgumentNullException.ThrowIfNull(session);
            Session = session;
            _translator = translator;

            _list = new ListBox { SelectionMode = SelectionMode.Multiple };
            _list.SelectionChanged += (_, _) => OnSelectionChanged();
            _grid = new PropertyGridControl
            {
                LabelTranslator = translator,
                CollectionEditorProvider = collectionEditorProvider,
            };
            _grid.PropertyValueChanged += (_, _) => RefreshList();
            _message = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            _message.Bind(TextBlock.ForegroundProperty,
                _message.GetResourceObservable("SemiColorDanger", value => value as IBrush ?? Brushes.IndianRed));

            _addButton = CreateToolButton("+", PolhemUIText.Add, AddItem);
            _removeButton = CreateToolButton("−", PolhemUIText.Delete, RemoveItems);
            _upButton = CreateToolButton("↑", PolhemUIText.MoveUp, () => MoveItem(-1));
            _downButton = CreateToolButton("↓", PolhemUIText.MoveDown, () => MoveItem(1));

            Content = BuildLayout(compact);
            RefreshList();
            if (Session.Items.Count > 0)
                _list.SelectedIndex = 0;
            UpdateButtons();
        }

        /// <summary>Occurs after OK has written the working list back.</summary>
        internal event EventHandler? Committed;

        /// <summary>Occurs when the edit ends without OK.</summary>
        internal event EventHandler? Cancelled;

        /// <summary>Gets the edit the panel shows.</summary>
        internal CollectionEditSession Session { get; }

        /// <summary>Gets the list of items.</summary>
        internal ListBox List => _list;

        /// <summary>Gets the grid that edits the selected item.</summary>
        internal PropertyGridControl ItemGrid => _grid;

        /// <summary>Gets the text of the message line.</summary>
        internal string? Message => _message.Text;

        /// <summary>Gets whether the Add, Delete, Move up and Move down buttons are enabled, in that order.</summary>
        internal (bool Add, bool Remove, bool Up, bool Down) ButtonStates =>
            (_addButton.IsEnabled, _removeButton.IsEnabled, _upButton.IsEnabled, _downButton.IsEnabled);

        /// <summary>
        /// Writes the working list back and ends the edit, or shows why it cannot be written.
        /// </summary>
        /// <returns><c>true</c> when the list was written back.</returns>
        internal bool Commit()
        {
            if (_finished) { return false; }
            if (Session.Validate() is { } error)
            {
                _message.Text = error;
                RefreshList();
                return false;
            }
            Session.Commit();
            _finished = true;
            Committed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Ends the edit without OK.
        /// </summary>
        /// <remarks>
        /// When the session works on the items themselves, their property edits already apply, so the order and the
        /// added and removed items are written back too when they pass <see cref="CollectionEditSession.Validate"/>.
        /// </remarks>
        internal void Cancel()
        {
            if (_finished) { return; }
            _finished = true;
            if (!Session.IsCancelable && Session.Validate() is null)
                Session.Commit();
            Cancelled?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Creates an item after the selected one, as the Add button does.</summary>
        internal void AddItem()
        {
            var index = Session.Add(_list.SelectedIndex);
            RefreshList(index);
        }

        /// <summary>Removes the selected items, as the Delete button does.</summary>
        internal void RemoveItems()
        {
            var selected = SelectedIndices();
            if (selected.Count == 0) { return; }
            Session.Remove(selected);
            RefreshList(Math.Min(selected.Min(), Session.Items.Count - 1));
        }

        /// <summary>Moves the selected item by <paramref name="offset"/> places, as Move up and Move down do.</summary>
        internal void MoveItem(int offset)
        {
            if (SelectedIndices() is not [var index]) { return; }
            RefreshList(Session.Move(index, offset));
        }

        /// <inheritdoc/>
        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromLogicalTree(e);
            Cancel();
        }

        private Control BuildLayout(bool compact)
        {
            var toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Margin = new Thickness(0, 0, 0, 6),
                Children = { _addButton, _removeButton, _upButton, _downButton },
            };
            var listArea = new DockPanel();
            DockPanel.SetDock(toolbar, Dock.Top);
            listArea.Children.Add(toolbar);
            listArea.Children.Add(_list);

            var ok = new Button { Content = UIText.Get(PolhemUIText.Ok), MinWidth = 80 };
            ok.Click += (_, _) => Commit();
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { ok } };
            // An edit made on the items themselves cannot be taken back, so there is no Cancel to offer.
            if (Session.IsCancelable)
            {
                var cancel = new Button { Content = UIText.Get(PolhemUIText.Cancel), MinWidth = 80 };
                cancel.Click += (_, _) => Cancel();
                buttons.Children.Add(cancel);
            }
            var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            DockPanel.SetDock(buttons, Dock.Right);
            footer.Children.Add(buttons);
            footer.Children.Add(_message);

            var body = new Grid();
            if (compact)
            {
                listArea.Height = CompactListHeight;
                _grid.Height = CompactGridHeight;
                body.RowDefinitions = new RowDefinitions("Auto,*");
                Grid.SetRow(_grid, 1);
                _grid.Margin = new Thickness(0, 8, 0, 0);
            }
            else
            {
                listArea.Width = ListWidth;
                body.ColumnDefinitions = new ColumnDefinitions("Auto,*");
                Grid.SetColumn(_grid, 1);
                _grid.Margin = new Thickness(12, 0, 0, 0);
            }
            body.Children.Add(listArea);
            body.Children.Add(_grid);

            // OK and Cancel are docked outside the part that grows, so a host that limits the height keeps them on
            // screen (maintainers/gotchas/avalonia-controls.md #18).
            var root = new DockPanel { Margin = new Thickness(16) };
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            root.Children.Add(body);
            return root;
        }

        private static Button CreateToolButton(string glyph, string tipKey, Action action)
        {
            var button = new Button { Content = glyph, MinWidth = 32 };
            ToolTip.SetTip(button, UIText.Get(tipKey));
            button.Click += (_, _) => action();
            return button;
        }

        private List<int> SelectedIndices()
        {
            if (_list.Selection is not { } selection) { return []; }
            return selection.SelectedIndexes.Where(i => i >= 0 && i < Session.Items.Count).OrderBy(i => i).ToList();
        }

        private void RefreshList(int? select = null)
        {
            var keep = select ?? _list.SelectedIndex;
            var invalid = Session.GetInvalidIndices();
            if (invalid.Count == 0)
                _message.Text = null;
            var entries = new List<CollectionEditEntry>();
            for (var i = 0; i < Session.Items.Count; i++)
                entries.Add(new CollectionEditEntry((invalid.Contains(i) ? InvalidMarker : string.Empty) + Session.GetLabel(i, _translator)));

            _refreshing = true;
            try
            {
                _list.ItemsSource = entries;
                _list.SelectedIndex = keep >= 0 && keep < entries.Count ? keep : -1;
            }
            finally
            {
                _refreshing = false;
            }
            OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            if (_refreshing) { return; }
            var index = _list.SelectedIndex;
            _grid.SelectedObject = index >= 0 && index < Session.Items.Count ? Session.Items[index] : null;
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            var resizable = PropertyGridMetadata.IsResizable(Session.Collection);
            var selected = SelectedIndices();
            _addButton.IsEnabled = resizable && Session.CanAdd;
            _removeButton.IsEnabled = resizable && selected.Count > 0;
            _upButton.IsEnabled = resizable && selected is [> 0];
            _downButton.IsEnabled = resizable && selected is [var last] && last < Session.Items.Count - 1;
        }

        // A list entry of its own, so two items with the same label are still two entries to the selection.
        private sealed class CollectionEditEntry(string label)
        {
            public override string ToString() => label;
        }
    }
}
