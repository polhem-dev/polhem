using System.Globalization;
using Microsoft.AspNetCore.Components;
using Polhem.Definition.Language;
using Polhem.UI.Core.Connectors;

namespace Polhem.Web.Blazor.Server.Components
{
    /// <summary>
    /// The list's paging: reading a page of rows and the page buttons below the list.
    /// </summary>
    public sealed partial class FormPage
    {
        private static readonly int[] s_pageSizes = [ListPagingState.DefaultPageSize, 100, 200];

        private ListPagingState _paging = new();
        private bool _isListPaged;

        private bool ShowsPager => !_isShowingRecord && _isListPaged && _listRows is { Rows.Count: > 0 };

        private string PagePosition => _paging.TotalPages is { } totalPages
            ? string.Format(CultureInfo.CurrentCulture, Text(PolhemUIText.PageNumberOfTotal),
                _paging.Page, totalPages, _paging.TotalCount)
            : string.Format(CultureInfo.CurrentCulture, Text(PolhemUIText.PageNumber), _paging.Page);

        // The data may have changed, so the row count is read again.
        private Task ReloadListAsync()
        {
            _paging.InvalidateTotalCount();
            return LoadListPageAsync(_paging.Page);
        }

        private async Task LoadListPageAsync(int page)
        {
            var version = _loadVersion;
            var request = _paging.BeginRequest();
            var response = await Client.Form(ProgId)
                .GetListAsync(_schema is null ? string.Empty : ListSelectFields.For(_schema), paging: _paging.CreateOptions(page))
                .ConfigureAwait(true);
            if (version != _loadVersion || !_paging.IsCurrent(request)) return;

            var rowCount = response.Table?.Rows.Count ?? 0;
            _paging.Apply(page, response.Paging);
            if (_paging.TryStepBackFromEmptyPage(rowCount))
            {
                await LoadListPageAsync(_paging.Page).ConfigureAwait(true);
                return;
            }

            _listRows = response.Table;
            _isListPaged = response.Paging is not null;
        }

        private async Task GoToPageAsync(int page)
        {
            await RunGuardedAsync(async () =>
            {
                await LoadListPageAsync(page).ConfigureAwait(true);
                _selectedRowId = null;
            }).ConfigureAwait(true);
        }

        private async Task OnPageSizeChangedAsync(ChangeEventArgs e)
        {
            if (!int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pageSize)) return;

            _paging.SetPageSize(pageSize);
            await GoToPageAsync(_paging.Page).ConfigureAwait(true);
        }
    }
}
