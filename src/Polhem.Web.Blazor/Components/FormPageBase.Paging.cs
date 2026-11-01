using System.Globalization;
using Polhem.Definition.Language;
using Polhem.UI.Core.Connectors;
using Polhem.Web.Blazor.Controls;

namespace Polhem.Web.Blazor.Components
{
    /// <summary>
    /// The list's paging: reading a page of rows and what the page buttons below the list act on.
    /// </summary>
    public abstract partial class FormPageBase
    {
        /// <summary>Gets the page sizes a list offers.</summary>
        protected static IReadOnlyList<int> PageSizes { get; } = [ListPagingState.DefaultPageSize, 100, 200];

        private ListPagingState _paging = new();
        private ListFilterState _filters = new();
        private bool _isListPaged;

        /// <summary>Gets a value indicating whether the list shows its page buttons.</summary>
        protected bool ShowsPager => !_isShowingRecord && !EffectiveIsCompact && _isListPaged && _listRows is { Rows.Count: > 0 };

        /// <summary>Gets the localized text that says which page is on screen.</summary>
        protected string PagePosition => _paging.TotalPages is { } totalPages
            ? string.Format(CultureInfo.CurrentCulture, Text(PolhemUIText.PageNumberOfTotal),
                _paging.Page, totalPages, _paging.TotalCount)
            : string.Format(CultureInfo.CurrentCulture, Text(PolhemUIText.PageNumber), _paging.Page);

        // The data may have changed, so the row count is read again.
        private Task ReloadListAsync(CancellationToken cancellationToken)
        {
            _paging.InvalidateTotalCount();
            // The cards hold every page read so far, and a changed row shifts the later pages, so they start over.
            return LoadListPageAsync(EffectiveIsCompact ? 1 : _paging.Page, cancellationToken);
        }

        private async Task LoadListPageAsync(int page, CancellationToken cancellationToken)
        {
            var version = _loadVersion;
            var request = _paging.BeginRequest();
            var response = await Client.Form(ProgId)
                .GetListAsync(_schema is null ? string.Empty : ListSelectFields.For(_schema), filter: _filters.CreateFilter(),
                    sortFields: _paging.CreateSort(), paging: _paging.CreateOptions(page), cancellationToken: cancellationToken)
                .ConfigureAwait(true);
            if (version != _loadVersion || !_paging.IsCurrent(request)) return;

            var rowCount = response.Table?.Rows.Count ?? 0;
            _paging.Apply(page, response.Paging);
            if (EffectiveIsCompact)
            {
                ShowCardPage(page, response.Table);
                _isListPaged = response.Paging is not null;
                return;
            }

            if (_paging.TryStepBackFromEmptyPage(rowCount))
            {
                await LoadListPageAsync(_paging.Page, cancellationToken).ConfigureAwait(true);
                return;
            }

            _listRows = response.Table;
            _isListPaged = response.Paging is not null;
        }

        /// <summary>Reads another page of the list and clears the selection.</summary>
        /// <param name="page">The 1-based page.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task GoToPageAsync(int page, CancellationToken cancellationToken = default)
        {
            await RunGuardedAsync(async () =>
            {
                await LoadListPageAsync(page, cancellationToken).ConfigureAwait(true);
                _selectedRowId = null;
            }).ConfigureAwait(true);
        }

        /// <summary>Moves the list through a field's sort cycle and reads the first page.</summary>
        /// <param name="fieldName">The field the user chose.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task OnSortRequestedAsync(string fieldName, CancellationToken cancellationToken = default)
        {
            if (IsListLocked) return;

            _paging.ToggleSort(fieldName);
            await GoToPageAsync(_paging.Page, cancellationToken).ConfigureAwait(true);
        }

        /// <summary>Gets the column filters of the list.</summary>
        protected ListFilterState Filters => _filters;

        /// <summary>Applies a column's filter and reads the first page.</summary>
        /// <param name="change">The column and what the user entered, which <see cref="ListFilterState.TrySet"/> has accepted.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task OnFilterChangedAsync(ColumnFilterChange change, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(change);
            if (IsListLocked) return;
            if (!_filters.TrySet(change.Field, change.Kind, change.First, change.Second)) return;
            await ReadFilteredListAsync(cancellationToken).ConfigureAwait(true);
        }

        /// <summary>Removes every filter and reads the first page.</summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task ClearFiltersAsync(CancellationToken cancellationToken = default)
        {
            if (IsListLocked) return;
            _filters.Clear();
            await ReadFilteredListAsync(cancellationToken).ConfigureAwait(true);
        }

        private Task ReadFilteredListAsync(CancellationToken cancellationToken)
        {
            _paging.InvalidateTotalCount();
            return RunGuardedAsync(async () =>
            {
                await LoadListPageAsync(1, cancellationToken).ConfigureAwait(true);
                _selectedRowId = null;
            });
        }

        /// <summary>Changes the rows per page and reads the first page.</summary>
        /// <param name="pageSize">The rows per page.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task SetPageSizeAsync(int pageSize, CancellationToken cancellationToken = default)
        {
            _paging.SetPageSize(pageSize);
            await GoToPageAsync(_paging.Page, cancellationToken).ConfigureAwait(true);
        }
    }
}
