using System.Data;
using System.Globalization;
using Polhem.Definition;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.UI.Core;
using Polhem.UI.Core.Connectors;
using Polhem.UI.Core.Fields;

namespace Polhem.Web.Blazor.Components
{
    /// <summary>
    /// The compact surface of the list: a card per row, reading the next page as the user scrolls near the end instead
    /// of showing page buttons, as the Avalonia head's <c>ListView</c> does.
    /// </summary>
    public abstract partial class FormPageBase
    {
        // Every card stays rendered. On Blazor Server the circuit holds it, so a long scroll costs traffic and server
        // memory; on WebAssembly it costs the browser's memory and render time. ADR-060 has the measurements.
        private const int MaxCardRows = 1000;

        private readonly List<DataRow> _cardRows = [];
        private CardListState<DataRow>? _cardState;
        private bool _scrollCardsToTop;

        private CardListState<DataRow> CardState => _cardState ??= new CardListState<DataRow>(_cardRows,
            row => Convert.ToString(row[SysFields.RowId], CultureInfo.InvariantCulture) ?? string.Empty, MaxCardRows);

        /// <summary>Gets the rows the cards hold.</summary>
        protected IReadOnlyList<DataRow> CardRows => _cardRows;

        /// <summary>Gets the columns a card shows.</summary>
        protected IEnumerable<LayoutColumn> CardColumns => _listLayout?.Columns?.Where(c => c.Visible) ?? [];

        /// <summary>Gets a value indicating whether the next page of the cards can be read.</summary>
        protected bool CanLoadMoreCards => CardState.CanLoadMore(_paging);

        /// <summary>Gets the note that the cards stopped at their limit, or <c>null</c>.</summary>
        protected string? CardLimitNotice => CardState.IsAtLimit
            ? string.Format(CultureInfo.CurrentCulture, Text(PolhemUIText.ListRowsLimited), MaxCardRows)
            : null;

        private void ShowCardPage(int page, DataTable? table)
        {
            if (page == 1) _selectedRowId = null;
            CardState.ShowPage(page, table?.Rows.Cast<DataRow>() ?? []);
        }

        /// <summary>
        /// Returns the cards to their first card when page 1 replaces them. Left at the old position, the end of the
        /// shorter list would be in view and read the next page at once. A UI package overrides this.
        /// </summary>
        /// <remarks>
        /// A read of page 1 calls this before the new cards render. Called after they render instead, it reaches the
        /// browser a round trip after the cards (Blazor Server runs <c>OnAfterRenderAsync</c> once the browser has
        /// acknowledged the render), and undoes a scroll the user made in between, so the end they scrolled to never
        /// reads the next page.
        /// </remarks>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected virtual Task ScrollCardsToTopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>Reads the next page of the cards, unless a call runs or there is nothing more to read.</summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task LoadMoreCardsAsync(CancellationToken cancellationToken = default)
        {
            if (!EffectiveIsCompact || IsListLocked || _isShowingRecord || !CanLoadMoreCards) return;
            await RunGuardedAsync(() => LoadListPageAsync(_paging.Page + 1, cancellationToken)).ConfigureAwait(true);
        }

        /// <summary>
        /// Records what the viewport reports. The two surfaces show different amounts of the list, so crossing the width
        /// starts again from the first page in the same order.
        /// </summary>
        /// <param name="compact">Whether the viewport is narrower than the threshold.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task OnCompactChangedAsync(bool compact, CancellationToken cancellationToken = default)
        {
            if (compact == _isViewportCompact) return;
            _isViewportCompact = compact;
            if (IsCompact is not null || _dataObject is null) return;
            // Not through the guard, which would skip this while a page is being read and leave the new surface without
            // rows. The paging state drops the response of whichever read this overtakes.
            try
            {
                await LoadListPageAsync(1, cancellationToken).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _notice = ex.Message;
            }
        }

        /// <summary>Selects a card.</summary>
        /// <param name="row">The card's row.</param>
        protected void SelectCard(DataRow row)
        {
            if (FormDataGuard.TryGetRowId(row, out var rowId)) _selectedRowId = rowId;
        }

        /// <summary>Opens a card's row read-only.</summary>
        /// <param name="row">The card's row.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected Task ActivateCardAsync(DataRow row, CancellationToken cancellationToken = default)
            => FormDataGuard.TryGetRowId(row, out var rowId) ? OnRowActivatedAsync(rowId, cancellationToken) : Task.CompletedTask;

        /// <summary>Determines whether a card is the selected one.</summary>
        /// <param name="row">The card's row.</param>
        /// <returns>Whether it is selected.</returns>
        protected bool IsCardSelected(DataRow row)
            => FormDataGuard.TryGetRowId(row, out var rowId) && rowId == _selectedRowId;

        /// <summary>Formats a card's value the way the grid formats its cell.</summary>
        /// <param name="row">The card's row.</param>
        /// <param name="column">The column.</param>
        /// <returns>The text.</returns>
        protected string FormatCardValue(DataRow row, LayoutColumn column)
            => GridCellFormatter.FormatColumnText(row, column, _schema?.MasterTable?.Fields is { } fields && fields.Contains(column.FieldName) ? fields[column.FieldName] : null,
                _roundingContext?.CurrencySettings, _roundingContext?.Company?.DefaultCurrency ?? string.Empty, _roundingContext?.UnitSettings,
                flag => Text(flag ? PolhemUIText.True : PolhemUIText.False));

        /// <summary>Determines whether a check column is checked on a card.</summary>
        /// <param name="row">The card's row.</param>
        /// <param name="column">The column.</param>
        /// <returns>Whether it is checked.</returns>
        protected static bool IsCardChecked(DataRow row, LayoutColumn column)
            => row.Table.Columns.Contains(column.FieldName)
                && string.Equals(FormValueBinding.ToBindingString(row[column.FieldName]), bool.TrueString, StringComparison.OrdinalIgnoreCase);
    }
}
