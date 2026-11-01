using System.Globalization;
using Polhem.Definition.Language;
using Polhem.UI.Core.Connectors;

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
        private bool _isListPaged;

        /// <summary>Gets a value indicating whether the list shows its page buttons.</summary>
        protected bool ShowsPager => !_isShowingRecord && _isListPaged && _listRows is { Rows.Count: > 0 };

        /// <summary>Gets the localized text that says which page is on screen.</summary>
        protected string PagePosition => _paging.TotalPages is { } totalPages
            ? string.Format(CultureInfo.CurrentCulture, Text(PolhemUIText.PageNumberOfTotal),
                _paging.Page, totalPages, _paging.TotalCount)
            : string.Format(CultureInfo.CurrentCulture, Text(PolhemUIText.PageNumber), _paging.Page);

        // The data may have changed, so the row count is read again.
        private Task ReloadListAsync(CancellationToken cancellationToken)
        {
            _paging.InvalidateTotalCount();
            return LoadListPageAsync(_paging.Page, cancellationToken);
        }

        private async Task LoadListPageAsync(int page, CancellationToken cancellationToken)
        {
            var version = _loadVersion;
            var request = _paging.BeginRequest();
            var response = await Client.Form(ProgId)
                .GetListAsync(_schema is null ? string.Empty : ListSelectFields.For(_schema),
                    sortFields: _paging.CreateSort(), paging: _paging.CreateOptions(page), cancellationToken: cancellationToken)
                .ConfigureAwait(true);
            if (version != _loadVersion || !_paging.IsCurrent(request)) return;

            var rowCount = response.Table?.Rows.Count ?? 0;
            _paging.Apply(page, response.Paging);
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
