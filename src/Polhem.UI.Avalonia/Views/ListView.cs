using System.Data;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Polhem.Api.Contracts.Form;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Core;
using Polhem.UI.Core.Connectors;
using Polhem.UI.Core.Definitions;
using Polhem.Definition.Language;

namespace Polhem.UI.Avalonia.Views
{
    /// <summary>
    /// Read-only browser for every record of a form (the master list). Pairs with
    /// <see cref="FormView"/> following the business-application convention of keeping list browsing and
    /// record viewing/editing on separate surfaces: <see cref="ListView"/> loads the list
    /// and raises <see cref="ViewRequested"/> / <see cref="EditRequested"/> /
    /// <see cref="AddRequested"/>, leaving the host to swap in the record surface. Delete is
    /// handled in place (the list stays the active surface), after asking the user unless
    /// <see cref="ConfirmDelete"/> is off. It carries no field form.
    /// </summary>
    /// <remarks>
    /// Like <see cref="FormView"/>, the host typically supplies only <see cref="ProgId"/>;
    /// when <see cref="Schema"/> / <see cref="FormConnector"/> are left unset,
    /// <see cref="InitializeAsync"/> resolves them through the <c>Resolve*</c> hooks from the services
    /// registered in <see cref="UIServices.Provider"/>, and a subclass can override the hooks to supply them
    /// another way (the unit tests rely on this).
    /// </remarks>
    public partial class ListView : UserControl
    {
        /// <summary>Identifies the <see cref="ProgId"/> styled property.</summary>
        public static readonly StyledProperty<string> ProgIdProperty =
            AvaloniaProperty.Register<ListView, string>(nameof(ProgId), defaultValue: string.Empty);

        /// <summary>Identifies the <see cref="Schema"/> styled property.</summary>
        public static readonly StyledProperty<FormSchema?> SchemaProperty =
            AvaloniaProperty.Register<ListView, FormSchema?>(nameof(Schema));

        /// <summary>Identifies the <see cref="FormConnector"/> styled property.</summary>
        public static readonly StyledProperty<IFormApiConnector?> FormConnectorProperty =
            AvaloniaProperty.Register<ListView, IFormApiConnector?>(nameof(FormConnector));

        /// <summary>Identifies the <see cref="ConfirmDelete"/> styled property.</summary>
        public static readonly StyledProperty<bool> ConfirmDeleteProperty =
            AvaloniaProperty.Register<ListView, bool>(nameof(ConfirmDelete), defaultValue: true);

        private readonly Button _viewButton;
        private readonly Button _newButton;
        private readonly Button _editButton;
        private readonly Button _deleteButton;
        private readonly MessageBar _errorLabel;
        private readonly TextBlock _loadingLabel;
        private readonly TextBlock _emptyListLabel;
        private readonly GridControl _grid;
        private readonly ListBox _cardList;
        private readonly ListPagerBar _pager;
        private readonly ListPagingState _paging = new();
        private bool _isLoading;
        private Guid _selectedRowId;
        private bool _isBusy;
        private bool _initialized;
        private bool _isInitializing;
        // On a narrow (phone) viewport the wide column grid is replaced by a stacked card list.
        // The `_hasData` and `_isCompact` flags gate which surface is shown (see `UpdateContentVisibility`).
        private bool _hasData;
        private bool _isCompact;

        static ListView()
        {
            SchemaProperty.Changed.AddClassHandler<ListView>((v, _) => v.OnInputsChanged());
            FormConnectorProperty.Changed.AddClassHandler<ListView>((v, _) => v.OnInputsChanged());
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ListView"/> with the toolbar + grid
        /// assembled. The toolbar buttons stay disabled until <see cref="InitializeAsync"/>
        /// resolves the schema and connector inputs.
        /// </summary>
        public ListView()
        {
            _errorLabel = new MessageBar { Margin = new Thickness(0, 0, 0, 8) };
            _loadingLabel = new TextBlock { Text = UIText.Get(PolhemUIText.Loading), IsVisible = true };

            // Tag each command with the permission action it requires; the capability resolver
            // hides an un-permitted button. Untagged controls stay visible (opt-in).
            _viewButton = new Button { Content = UIText.Get(PolhemUIText.View), IsEnabled = false };
            PermissionScope.SetAction(_viewButton, PermissionActions.Read);
            _viewButton.Click += (_, _) => OnViewClicked();

            _newButton = new Button { Content = UIText.Get(PolhemUIText.New), IsEnabled = false };
            PermissionScope.SetAction(_newButton, PermissionActions.Create);
            _newButton.Click += (_, _) => OnNewClicked();

            _editButton = new Button { Content = UIText.Get(PolhemUIText.Edit), IsEnabled = false };
            PermissionScope.SetAction(_editButton, PermissionActions.Update);
            _editButton.Click += (_, _) => OnEditClicked();

            _deleteButton = new Button { Content = UIText.Get(PolhemUIText.Delete), IsEnabled = false };
            PermissionScope.SetAction(_deleteButton, PermissionActions.Delete);
            _deleteButton.Click += async (_, _) => await OnDeleteClickedAsync().ConfigureAwait(true);

            var toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
            };
            toolbar.Children.Add(_viewButton);
            toolbar.Children.Add(_newButton);
            toolbar.Children.Add(_editButton);
            toolbar.Children.Add(_deleteButton);

            _grid = new GridControl();
            _grid.RowSelected += (_, rowId) => OnRowSelected(rowId);
            // List mode never binds a FormDataObject, so the grid's own double-tap path
            // (detail EditForm editing) stays dormant; the bubbling DoubleTapped is ours to
            // treat as "open this record" — opening read-only (View), so browsing the list
            // never accidentally enters edit mode. Editing is the explicit Edit action.
            _grid.DoubleTapped += (_, _) => OnGridDoubleTapped();

            // Compact (phone) surface: one card per row with the columns stacked as caption/value
            // lines, so narrow viewports stay readable instead of horizontally scrolling a wide
            // grid. Bound to the same DataTable; only one of grid / card list is visible at a time.
            _cardList = new ListBox
            {
                IsVisible = false,
                Background = Brushes.Transparent,
                SelectionMode = SelectionMode.Single,
            };
            _cardList.SelectionChanged += (_, _) => OnCardSelectionChanged();
            _cardList.DoubleTapped += (_, _) => OnGridDoubleTapped();

            _emptyListLabel = new TextBlock { Text = UIText.Get(PolhemUIText.NoData), IsVisible = false };

            // DockPanel (not StackPanel): the chrome docks to the top and the content area fills the
            // remaining BOUNDED height as the last child. A StackPanel would give the grid unbounded
            // height, so the DataGrid grows to fit every row and never shows its own vertical scrollbar.
            toolbar.Margin = new Thickness(0, 0, 0, 8);

            // Grid and card list overlay the same fill region; visibility selects between them.
            var contentArea = new Panel();
            contentArea.Children.Add(_grid);
            contentArea.Children.Add(_cardList);

            _pager = new ListPagerBar { IsVisible = false };
            _pager.PageRequested += (_, page) => _ = LoadPageAsync(page, CancellationToken.None);
            _pager.PageSizeRequested += (_, pageSize) =>
            {
                _paging.SetPageSize(pageSize);
                _ = LoadPageAsync(_paging.Page, CancellationToken.None);
            };

            var host = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(_errorLabel, Dock.Top);
            DockPanel.SetDock(_loadingLabel, Dock.Top);
            DockPanel.SetDock(toolbar, Dock.Top);
            DockPanel.SetDock(_emptyListLabel, Dock.Top);
            DockPanel.SetDock(_pager, Dock.Bottom);
            host.Children.Add(_errorLabel);
            host.Children.Add(_loadingLabel);
            host.Children.Add(toolbar);
            host.Children.Add(_emptyListLabel);
            host.Children.Add(_pager);
            host.Children.Add(contentArea);

            Content = host;
        }

        /// <summary>
        /// Gets or sets the program identifier (e.g. "Category"). Drives both the schema lookup and the
        /// connector creation when the host leaves <see cref="Schema"/> / <see cref="FormConnector"/> unset.
        /// </summary>
        public string ProgId
        {
            get => GetValue(ProgIdProperty);
            set => SetValue(ProgIdProperty, value);
        }

        /// <summary>Gets or sets the resolved <see cref="FormSchema"/>.</summary>
        public FormSchema? Schema
        {
            get => GetValue(SchemaProperty);
            set => SetValue(SchemaProperty, value);
        }

        /// <summary>Gets or sets the connector used for the list / delete round-trips.</summary>
        public IFormApiConnector? FormConnector
        {
            get => GetValue(FormConnectorProperty);
            set => SetValue(FormConnectorProperty, value);
        }

        /// <summary>
        /// Gets or sets whether Delete asks the user before deleting the selected record. Defaults to <c>true</c>.
        /// </summary>
        /// <remarks>
        /// A styled property, so an application whose lists are created by the shell can turn it off for all of
        /// them with a style setter on <see cref="ListView"/>.
        /// </remarks>
        public bool ConfirmDelete
        {
            get => GetValue(ConfirmDeleteProperty);
            set => SetValue(ConfirmDeleteProperty, value);
        }

        /// <summary>
        /// Raised when the user opens a record read-only (View on a selected row). Carries
        /// the record's <see cref="SysFields.RowId"/>.
        /// </summary>
        public event EventHandler<Guid>? ViewRequested;

        /// <summary>
        /// Raised when the user opens a record for editing (Edit on a selected row, or a
        /// double-click). Carries the record's <see cref="SysFields.RowId"/>.
        /// </summary>
        public event EventHandler<Guid>? EditRequested;

        /// <summary>Raised when the user presses New. The host opens a blank record surface.</summary>
        public event EventHandler? AddRequested;

        /// <summary>Raised whenever a backend round-trip throws.</summary>
        public event EventHandler<Exception>? ErrorOccurred;

        /// <summary>Resolves the schema + connector inputs and runs the initial list load.</summary>
        /// <param name="cancellationToken">
        /// A token that cancels the schema fetch and the first load. A cancelled call throws
        /// <see cref="OperationCanceledException"/> to the caller instead of being reported on the view.
        /// </param>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_initialized || _isInitializing) return;

            var hasProgId = !string.IsNullOrEmpty(ProgId);
            if (!hasProgId && (Schema is null || FormConnector is null)) return;

            _isInitializing = true;
            try
            {
                if (!await TryResolveSchemaAsync(hasProgId, cancellationToken).ConfigureAwait(true)) return;
                ResolveFormConnectorFallback(hasProgId);

                if (Schema is null || FormConnector is null) return;
                AttachGrid();

                _initialized = true;
                await ReloadAsync(cancellationToken).ConfigureAwait(true);
                UpdateToolbarState();
            }
            finally
            {
                _isInitializing = false;
            }
        }

        /// <summary>
        /// Reloads the page on screen from the backend, clearing any prior selection. The row count is read again,
        /// since the data may have changed.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that cancels the load. A cancelled load throws <see cref="OperationCanceledException"/>
        /// to the caller instead of being reported on the view.
        /// </param>
        public Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            _paging.InvalidateTotalCount();
            return LoadPageAsync(_paging.Page, cancellationToken);
        }

        private async Task LoadPageAsync(int page, CancellationToken cancellationToken)
        {
            var connector = FormConnector;
            if (connector is null) return;

            var request = _paging.BeginRequest();
            _isLoading = true;
            UpdateToolbarState();
            try
            {
                var response = await connector.GetListAsync(ComputeSelectFields(), paging: _paging.CreateOptions(page),
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(true);
                if (!_paging.IsCurrent(request)) return;

                var rowCount = response.Table?.Rows.Count ?? 0;
                _paging.Apply(page, response.Paging);
                if (_paging.TryStepBackFromEmptyPage(rowCount))
                {
                    await LoadPageAsync(_paging.Page, cancellationToken).ConfigureAwait(true);
                    return;
                }

                _grid.DataTable = response.Table;
                _cardList.ItemsSource = response.Table?.DefaultView;
                _hasData = rowCount > 0;
                _emptyListLabel.IsVisible = !_hasData;
                _pager.Update(_paging);
                _pager.IsVisible = _hasData && response.Paging is not null;
                UpdateContentVisibility();
                _selectedRowId = Guid.Empty;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                ReportError(ex);
            }
            finally
            {
                if (_paging.IsCurrent(request))
                    _isLoading = false;
                UpdateToolbarState();
            }
        }

        /// <summary>
        /// Gets or sets the source the column headers' schema is fetched from, for this view only. <c>null</c>, the
        /// default, uses the <see cref="IFormDefinitionSource"/> registered in <see cref="UIServices.Provider"/>;
        /// with neither, the host sets <see cref="Schema"/>.
        /// </summary>
        /// <remarks>
        /// The same source <see cref="FormView.DefinitionSource"/> falls back to, so the list and the record it
        /// opens show the same captions. The schema is fetched once, when the list loads; switching the language
        /// does not re-localize a list already on screen — reopen it.
        /// </remarks>
        public IFormDefinitionSource? DefinitionSource { get; set; }

        /// <summary>
        /// Resolves the <see cref="FormSchema"/> for <paramref name="progId"/> when the host did
        /// not pre-set <see cref="Schema"/>. Defaults to <see cref="DefinitionSource"/>, else the registered
        /// source; returns <c>null</c> when there is none.
        /// </summary>
        /// <param name="progId">The program identifier.</param>
        /// <param name="cancellationToken">A token that cancels the fetch.</param>
        protected virtual async Task<FormSchema?> ResolveSchemaAsync(string progId, CancellationToken cancellationToken)
            => (DefinitionSource ?? UIServices.Get<IFormDefinitionSource>()) is { } source
                ? await source.GetFormSchemaAsync(progId, cancellationToken).ConfigureAwait(false)
                : null;

        /// <summary>
        /// Resolves the connector for the list / delete round-trips when the host did not set
        /// <see cref="FormConnector"/>. Defaults to the <see cref="IFormApiConnectorFactory"/> registered in
        /// <see cref="UIServices.Provider"/>; returns <c>null</c> when there is none.
        /// </summary>
        /// <param name="progId">The program identifier.</param>
        protected virtual IFormApiConnector? ResolveFormConnector(string progId)
            => UIServices.Get<IFormApiConnectorFactory>()?.Create(progId);

        /// <summary>
        /// Resolves the signed-in user's context, whose capabilities degrade the columns and the toolbar.
        /// Defaults to the <see cref="IUserContext"/> registered in <see cref="UIServices.Provider"/>; <c>null</c>
        /// treats the user as unrestricted.
        /// </summary>
        protected virtual IUserContext? ResolveUserContext() => UIServices.Get<IUserContext>();

        /// <inheritdoc/>
        protected override void OnAttachedToVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            if (!_initialized && !_isInitializing)
                _ = InitializeAsync();
        }

        /// <inheritdoc/>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            // Switch between the wide column grid and the stacked card list as the viewport crosses
            // the compact width threshold. Bounds is a direct property whose changes are not
            // delivered to static class handlers, so the reaction is wired here.
            if (change.Property == BoundsProperty)
            {
                var compact = Bounds.Width > 0 && Bounds.Width < FormView.DefaultCompactWidthThreshold;
                if (compact != _isCompact)
                {
                    _isCompact = compact;
                    UpdateContentVisibility();
                }
            }
        }

        private void UpdateContentVisibility()
        {
            _grid.IsVisible = _hasData && !_isCompact;
            _cardList.IsVisible = _hasData && _isCompact;
        }

        // One card per row: each visible column rendered as a "caption  value" line, read straight
        // off the DataRowView (no reflection binding, so it works under iOS AOT). The value text comes
        // from `formatValue`, which the view points at the wide grid's own cell formatter, so a date
        // column shows the same short date on a phone as on the desktop grid. A check-box column shows
        // the same disabled check box the grid does, not text.
        internal static FuncDataTemplate<DataRowView> BuildCardTemplate(
            IReadOnlyList<LayoutColumn> columns, Func<DataRowView?, LayoutColumn, string> formatValue)
            => new FuncDataTemplate<DataRowView>((row, _) =>
            {
                var stack = new StackPanel { Spacing = 3 };
                foreach (var column in columns)
                {
                    var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                    line.Children.Add(new TextBlock
                    {
                        Text = column.Caption,
                        FontWeight = FontWeight.SemiBold,
                        MinWidth = 92,
                        Opacity = 0.7,
                    });
                    line.Children.Add(column.ControlType == ControlType.CheckEdit
                        ? BuildCardCheckBox(row, column)
                        : new TextBlock { Text = formatValue(row, column), TextWrapping = TextWrapping.Wrap });
                    stack.Children.Add(line);
                }

                return new Border
                {
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 6),
                    BorderThickness = new Thickness(1),
                    BorderBrush = Brushes.Gray,
                    CornerRadius = new CornerRadius(6),
                    Child = stack,
                };
            });

        private static CheckBox BuildCardCheckBox(DataRowView? row, LayoutColumn column)
        {
            var isChecked = row is not null
                && row.Row.Table.Columns.Contains(column.FieldName)
                && row.Row[column.FieldName] is true;
            return new CheckBox
            {
                IsChecked = isChecked,
                IsEnabled = false,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        private void OnCardSelectionChanged()
        {
            if (_cardList.SelectedItem is DataRowView drv && FormDataGuard.TryGetRowId(drv.Row, out var rowId))
                OnRowSelected(rowId);
        }

        private void OnInputsChanged()
        {
            if (_initialized || _isInitializing) return;
            _ = InitializeAsync();
        }

        private async Task<bool> TryResolveSchemaAsync(bool hasProgId, CancellationToken cancellationToken)
        {
            if (Schema is not null || !hasProgId) return true;

            ClearError();
            try
            {
                var loaded = await ResolveSchemaAsync(ProgId, cancellationToken).ConfigureAwait(true);
                if (loaded is not null)
                    Schema = loaded;
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                ReportError(ex);
                return false;
            }
        }

        private void ResolveFormConnectorFallback(bool hasProgId)
        {
            if (FormConnector is not null || !hasProgId) return;
            FormConnector = ResolveFormConnector(ProgId);
        }
    }
}
