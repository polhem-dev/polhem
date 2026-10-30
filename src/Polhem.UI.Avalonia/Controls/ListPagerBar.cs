using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Polhem.Definition.Language;
using Polhem.UI.Core.Connectors;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// The page buttons below a form list: previous, next, the current position and the rows per page.
    /// </summary>
    internal sealed class ListPagerBar : UserControl
    {
        private static readonly int[] s_pageSizes = [ListPagingState.DefaultPageSize, 100, 200];

        private readonly Button _previousButton;
        private readonly Button _nextButton;
        private readonly TextBlock _positionLabel;
        private readonly ComboBox _pageSizeBox;
        private int _page = 1;
        private bool _isUpdating;

        public ListPagerBar()
        {
            _previousButton = new Button { Content = UIText.Get(PolhemUIText.PreviousPage) };
            _previousButton.Click += (_, _) => PageRequested?.Invoke(this, _page - 1);

            _nextButton = new Button { Content = UIText.Get(PolhemUIText.NextPage) };
            _nextButton.Click += (_, _) => PageRequested?.Invoke(this, _page + 1);

            _positionLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center };

            _pageSizeBox = new ComboBox { ItemsSource = s_pageSizes, SelectedIndex = 0 };
            _pageSizeBox.SelectionChanged += (_, _) =>
            {
                if (!_isUpdating && _pageSizeBox.SelectedItem is int pageSize)
                    PageSizeRequested?.Invoke(this, pageSize);
            };

            // WrapPanel: on a phone the bar is wider than the viewport, so its parts move to a second line.
            var panel = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 4 };
            panel.Children.Add(_previousButton);
            panel.Children.Add(_nextButton);
            panel.Children.Add(_positionLabel);
            panel.Children.Add(new TextBlock
            {
                Text = UIText.Get(PolhemUIText.RowsPerPage),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
            });
            panel.Children.Add(_pageSizeBox);

            Margin = new Thickness(0, 8, 0, 0);
            Content = panel;
        }

        /// <summary>Raised when the user asks for another page. Carries the 1-based page.</summary>
        public event EventHandler<int>? PageRequested;

        /// <summary>Raised when the user picks another page size. Carries the rows per page.</summary>
        public event EventHandler<int>? PageSizeRequested;

        /// <summary>Shows <paramref name="state"/>.</summary>
        /// <param name="state">The paging position on screen.</param>
        public void Update(ListPagingState state)
        {
            _page = state.Page;
            _previousButton.IsEnabled = state.CanGoPrevious;
            _nextButton.IsEnabled = state.CanGoNext;
            _positionLabel.Text = state.TotalPages is { } totalPages
                ? string.Format(CultureInfo.CurrentCulture, UIText.Get(PolhemUIText.PageNumberOfTotal),
                    state.Page, totalPages, state.TotalCount)
                : string.Format(CultureInfo.CurrentCulture, UIText.Get(PolhemUIText.PageNumber), state.Page);

            _isUpdating = true;
            _pageSizeBox.SelectedItem = state.PageSize;
            _isUpdating = false;
        }
    }
}
