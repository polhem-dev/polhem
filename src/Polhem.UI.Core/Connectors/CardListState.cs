namespace Polhem.UI.Core.Connectors
{
    /// <summary>
    /// The rows of a list a phone shows as cards, read a page at a time as the user scrolls, shared by the UI heads so
    /// each one accumulates them the same way.
    /// </summary>
    /// <remarks>
    /// The rows stay in memory as the user scrolls, so a long scroll stops at <see cref="MaxRows"/>. A row that another
    /// user's insert pushed onto the next page arrives twice, so the row id decides.
    /// </remarks>
    /// <typeparam name="TRow">The head's row type.</typeparam>
    public sealed class CardListState<TRow>
    {
        /// <summary>The most rows the cards hold.</summary>
        public const int MaxRows = 5000;

        private readonly HashSet<string> _rowIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly IList<TRow> _rows;
        private readonly Func<TRow, string> _getRowId;

        /// <summary>
        /// Initializes a new instance of <see cref="CardListState{TRow}"/>.
        /// </summary>
        /// <param name="rows">The list to fill, which the head binds its surface to.</param>
        /// <param name="getRowId">Returns the id of a row.</param>
        public CardListState(IList<TRow> rows, Func<TRow, string> getRowId)
        {
            ArgumentNullException.ThrowIfNull(rows);
            ArgumentNullException.ThrowIfNull(getRowId);
            _rows = rows;
            _getRowId = getRowId;
        }

        /// <summary>Gets a value indicating whether the cards hold as many rows as they may.</summary>
        public bool IsAtLimit => _rows.Count >= MaxRows;

        /// <summary>
        /// Takes in a page of rows: page 1 replaces the cards, and a later page extends them.
        /// </summary>
        /// <param name="page">The 1-based page.</param>
        /// <param name="rows">The rows of the page.</param>
        public void ShowPage(int page, IEnumerable<TRow> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);
            if (page == 1)
            {
                _rows.Clear();
                _rowIds.Clear();
            }

            foreach (var row in rows)
            {
                if (IsAtLimit) break;
                if (_rowIds.Add(_getRowId(row)))
                    _rows.Add(row);
            }
        }

        /// <summary>Determines whether the next page should be read.</summary>
        /// <param name="paging">The paging position of the list.</param>
        /// <returns>Whether there is a next page and room for it.</returns>
        public bool CanLoadMore(ListPagingState paging)
        {
            ArgumentNullException.ThrowIfNull(paging);
            return paging.CanGoNext && !IsAtLimit;
        }
    }
}
