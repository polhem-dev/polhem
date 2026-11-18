using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Threading;
using Polhem.Core;
using Polhem.Definition.Filters;
using Polhem.Definition.Language;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Core.Criteria;

namespace Polhem.UI.Avalonia.Views
{
    /// <summary>
    /// Query-condition half of <see cref="ListView"/>: the toolbar's query button, the chips of the conditions in
    /// effect above the list, the <see cref="CriteriaPanel"/> pane beside it, and the filter the list reads its pages
    /// with.
    /// </summary>
    /// <remarks>
    /// The pane follows the width of the view: docked on the right when the list keeps
    /// <see cref="FormView.DefaultCompactWidthThreshold"/> beside it, laid over the list when it does not, and a card
    /// over the whole view through the overlay host on a phone-sized view, where the safe area and the on-screen
    /// keyboard are already taken care of.
    /// </remarks>
    public partial class ListView
    {
        /// <summary>The width of the query pane beside the list.</summary>
        internal const double QueryPaneWidth = 360;

        private readonly QueryCriteria _criteria = new();
        private readonly StackPanel _chips = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
        private ScrollViewer _chipBar = null!;
        private SplitView _splitView = null!;
        private Button _queryButton = null!;
        private CriteriaPanel _criteriaPanel = null!;
        private QueryPaneMode _paneMode = QueryPaneMode.Docked;
        private TaskCompletionSource? _fullScreenPane;
        private FilterNode? _activeFilter;
        private int _activeConditions;

        /// <summary>How the query pane is shown at the current width.</summary>
        internal enum QueryPaneMode
        {
            /// <summary>Beside the list, which narrows.</summary>
            Docked,

            /// <summary>Over the right side of the list, closed by a click outside it.</summary>
            Overlay,

            /// <summary>A card over the whole view, on a phone-sized view.</summary>
            FullScreen,
        }

        /// <summary>Gets the conditions the user builds; a list starts with none.</summary>
        internal QueryCriteria Criteria => _criteria;

        /// <summary>Gets the panel that edits <see cref="Criteria"/>.</summary>
        internal CriteriaPanel CriteriaPanel => _criteriaPanel;

        /// <summary>Gets the toolbar button that shows the panel.</summary>
        internal Button QueryButton => _queryButton;

        /// <summary>Gets the text of the chips of the conditions in effect.</summary>
        internal IReadOnlyList<string> ConditionChips
            => _chips.Children.OfType<Border>().Select(b => b.Tag as string ?? string.Empty).ToArray();

        /// <summary>Gets how the query pane is shown at the current width.</summary>
        internal QueryPaneMode PaneMode => _paneMode;

        /// <summary>Gets a value indicating whether the query pane is open.</summary>
        internal bool IsQueryPaneOpen => _splitView.IsPaneOpen || _fullScreenPane is not null;

        private void CreateCriteriaChrome()
        {
            _criteriaPanel = new CriteriaPanel { Today = ResolveToday };
            _criteriaPanel.SearchRequested += (_, _) => _ = RunQueryAsync();
            _criteriaPanel.CloseRequested += (_, _) => CloseQueryPane();

            _queryButton = new Button { Content = UIText.Get(PolhemUIText.Query), IsVisible = false };
            _queryButton.Click += (_, _) => ToggleQueryPane();

            _chipBar = new ScrollViewer
            {
                Content = _chips,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 0, 0, 8),
                IsVisible = false,
            };
        }

        private SplitView WrapWithCriteriaPane(Control listArea)
        {
            _splitView = new SplitView
            {
                PanePlacement = SplitViewPanePlacement.Right,
                OpenPaneLength = QueryPaneWidth,
                DisplayMode = SplitViewDisplayMode.Inline,
                UseLightDismissOverlayMode = true,
                IsPaneOpen = false,
                Content = listArea,
                Pane = _criteriaPanel,
            };
            return _splitView;
        }

        private void AttachCriteria()
        {
            var fields = CriteriaField.FromQueryFields(Schema!, ResolveUserContext()?.Capabilities);
            _criteriaPanel.Bind(fields, _criteria);
            _queryButton.IsVisible = fields.Count > 0;
        }

        /// <summary>
        /// Chooses how the pane is shown for <paramref name="width"/>: docked while the list keeps the compact
        /// threshold beside it, over the list below that, and as a card over the whole view below the compact threshold itself.
        /// </summary>
        /// <param name="width">The width of the view.</param>
        internal void ApplyPaneMode(double width)
        {
            if (width <= 0) return;
            double threshold = FormView.DefaultCompactWidthThreshold;
            var mode = width < threshold ? QueryPaneMode.FullScreen
                : width - QueryPaneWidth >= threshold ? QueryPaneMode.Docked
                : QueryPaneMode.Overlay;
            if (mode == _paneMode) return;

            bool wasOpen = IsQueryPaneOpen;
            CloseQueryPane();
            _paneMode = mode;
            if (mode != QueryPaneMode.FullScreen)
                _splitView.DisplayMode = mode == QueryPaneMode.Docked ? SplitViewDisplayMode.Inline : SplitViewDisplayMode.Overlay;
            // A pane the user had open stays open across a resize, except into the card over the whole view, which
            // would cover the list the user is resizing to see.
            if (wasOpen && mode != QueryPaneMode.FullScreen)
                _splitView.IsPaneOpen = true;
        }

        private void ToggleQueryPane()
        {
            if (IsQueryPaneOpen)
                CloseQueryPane();
            else
                OpenQueryPane(null);
        }

        /// <summary>Opens the pane, scrolled to <paramref name="row"/> when one is given.</summary>
        /// <param name="row">The row to bring into view, or <c>null</c>.</param>
        internal void OpenQueryPane(CriteriaRow? row)
        {
            if (_paneMode == QueryPaneMode.FullScreen)
                _ = ShowFullScreenPaneAsync();
            else
                _splitView.IsPaneOpen = true;
            if (row is not null)
                Dispatcher.UIThread.Post(() => _criteriaPanel.ScrollToRow(row), DispatcherPriority.Loaded);
        }

        private void CloseQueryPane()
        {
            _fullScreenPane?.TrySetResult();
            _splitView.IsPaneOpen = false;
        }

        private async Task ShowFullScreenPaneAsync()
        {
            if (_fullScreenPane is not null) return;
            var card = new TaskCompletionSource();
            _fullScreenPane = card;
            _splitView.Pane = null;
            try
            {
                await OverlayDialogHost.ShowAsync(this, _criteriaPanel, null, card.Task, Bounds.Width).ConfigureAwait(true);
            }
            catch (InvalidOperationException ex)
            {
                // Boundary: a click handler; without an overlay layer the pane cannot open as a card, and the list
                // stays as it was.
                ReportError(ex);
            }
            finally
            {
                _fullScreenPane = null;
                _splitView.Pane = _criteriaPanel;
            }
        }

        /// <summary>
        /// Converts the conditions into the list's filter and reads the first page with it. The filter stays until
        /// the next query, so paging and sorting read the same rows even while the user edits the conditions; the
        /// chips show the same conditions.
        /// </summary>
        private async Task RunQueryAsync()
        {
            if (!_initialized) return;
            _activeFilter = GetListFilterConverter.ToFilter(_criteria, ResolveToday(),
                CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);
            _activeConditions = _criteria.ActiveCount;
            _queryButton.Content = _activeConditions > 0
                ? string.Format(CultureInfo.CurrentCulture, UIText.Get(PolhemUIText.QueryWithCount), _activeConditions)
                : UIText.Get(PolhemUIText.Query);
            UpdateChips();
            // Over the list or as a card, the pane hides the rows the search just read.
            if (_paneMode != QueryPaneMode.Docked)
                CloseQueryPane();
            _paging.InvalidateTotalCount();
            await LoadPageAsync(1, CancellationToken.None).ConfigureAwait(true);
        }

        private void UpdateChips()
        {
            _chips.Children.Clear();
            foreach (var row in _criteria.Rows.Where(r => r.HasValue))
                _chips.Children.Add(BuildChip(row, CriteriaRowSummary.Describe(row, UIText.Get)));
            if (_chips.Children.Count > 0)
            {
                var clear = new Button { Content = UIText.Get(PolhemUIText.ClearFilters), VerticalAlignment = VerticalAlignment.Center };
                clear.Click += (_, _) => _criteriaPanel.ClearAndSearch();
                _chips.Children.Add(clear);
            }
            _chipBar.IsVisible = _chips.Children.Count > 0;
        }

        private Border BuildChip(CriteriaRow row, string text)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            var open = new Button { Content = text, Padding = new Thickness(8, 2), BorderThickness = new Thickness(0) };
            open.Click += (_, _) => OpenQueryPane(row);
            content.Children.Add(open);
            if (row.Kind != CriteriaRowKind.Required)
            {
                var remove = new Button { Content = "✕", Padding = new Thickness(6, 2), BorderThickness = new Thickness(0) };
                string name = string.Format(CultureInfo.CurrentCulture, UIText.Get(PolhemUIText.RemoveItem), text);
                global::Avalonia.Automation.AutomationProperties.SetName(remove, name);
                ToolTip.SetTip(remove, name);
                remove.Click += (_, _) => RemoveChip(row);
                content.Children.Add(remove);
            }
            var chip = new Border
            {
                Child = content,
                Tag = text,
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
            };
            chip.Bind(Border.BorderBrushProperty, chip.GetResourceObservable("SemiColorPrimary"));
            return chip;
        }

        /// <summary>Removes the condition behind a chip and runs the query again.</summary>
        /// <param name="row">The row the chip shows.</param>
        internal void RemoveChip(CriteriaRow row)
        {
            if (!_criteria.Remove(row)) return;
            _criteriaPanel.Refresh();
            _ = RunQueryAsync();
        }

        // Relative dates resolve against the signed-in user's today, the zone the request's dates are read in.
        private DateOnly ResolveToday()
            => ResolveUserContext()?.User?.TimeZone is { Length: > 0 } zone
                ? FrameworkClock.Today(zone)
                : DateOnly.FromDateTime(DateTime.Now);
    }
}
