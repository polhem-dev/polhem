using System.Collections.ObjectModel;
using System.Data;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Polhem.Definition;
using Polhem.Definition.Layouts;
using Polhem.UI.Core;

namespace Polhem.UI.Avalonia.Views
{
    /// <summary>
    /// The compact (phone) surface of <see cref="ListView"/>: a card per row, reading the next page as the user
    /// scrolls near the end instead of showing page buttons.
    /// </summary>
    public partial class ListView
    {
        // The rows stay in memory as the user scrolls, so a long scroll must not grow without limit.
        internal const int MaxCardRows = 5000;

        // How close to the last card, in cards, the next page starts loading.
        private const int CardLoadThreshold = 10;

        // A collection that raises change notifications, so appending a page keeps the scroll position.
        private readonly ObservableCollection<DataRowView> _cardRows = [];
        private readonly HashSet<string> _cardRowIds = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets the rows the card list holds.</summary>
        internal IReadOnlyList<DataRowView> CardRows => _cardRows;

        // Page 1 replaces the cards; a later page extends them. A row another user's insert pushed onto the next page
        // arrives twice, so the row id decides.
        private void ShowCardPage(int page, DataTable? table)
        {
            AttachCardRows();
            if (page == 1)
            {
                _cardRows.Clear();
                _cardRowIds.Clear();
                _selectedRowId = Guid.Empty;
            }

            if (table is not null)
            {
                foreach (DataRowView row in table.DefaultView)
                {
                    if (_cardRows.Count >= MaxCardRows) break;
                    if (_cardRowIds.Add(Convert.ToString(row[SysFields.RowId], System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty))
                        _cardRows.Add(row);
                }
            }

            _hasData = _cardRows.Count > 0;
            if (page == 1 && _hasData)
                ScrollCardsToTop();
        }

        /// <summary>
        /// Returns the card list to its first card. The list keeps its scroll offset when its rows are replaced, so a
        /// reload would otherwise land at the end of the new first page. The unit tests override this, because they
        /// run off the UI thread.
        /// </summary>
        internal virtual void ScrollCardsToTop()
            // The new rows are not laid out yet, so the scroll waits for the next dispatcher turn.
            => Dispatcher.UIThread.Post(() => _cardList.ScrollIntoView(0));

        /// <summary>
        /// Gives the card list its rows, once the view first needs them. A wide list never does, and subscribing to the
        /// collection's changes needs the UI thread. The unit tests override this, because they run on other threads.
        /// </summary>
        internal virtual void AttachCardRows()
        {
            if (_cardList.ItemsSource is null)
                _cardList.ItemsSource = _cardRows;
        }

        private void OnCardPrepared(int index)
        {
            if (index < _cardRows.Count - CardLoadThreshold) return;
            // A container is prepared during layout, which must not change the items it is laying out.
            Dispatcher.UIThread.Post(() => _ = LoadMoreCardsAsync());
        }

        private Task LoadMoreCardsAsync()
        {
            if (!_isCompact || _isLoading || _isBusy || !_paging.CanGoNext || _cardRows.Count >= MaxCardRows)
                return Task.CompletedTask;
            return LoadPageAsync(_paging.Page + 1, CancellationToken.None);
        }

        // The two surfaces show different amounts of the list, so crossing the width starts again from the first page
        // in the same order.
        private void ApplyCompact(bool compact)
        {
            if (compact == _isCompact) return;
            _isCompact = compact;
            UpdateContentVisibility();
            if (_initialized)
                _ = LoadPageAsync(1, CancellationToken.None);
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
    }
}
