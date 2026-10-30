using Polhem.Definition.Paging;

namespace Polhem.UI.Core.Connectors
{
    /// <summary>
    /// The paging position of a form list on screen, shared by the UI heads so each one pages the same way.
    /// </summary>
    /// <remarks>
    /// The position changes only when a response arrives through <see cref="Apply"/>, so a failed request leaves the
    /// state on the page still showing. A head wraps each request in <see cref="BeginRequest"/> and
    /// <see cref="IsCurrent"/> so a slow response cannot overwrite a newer one.
    /// </remarks>
    public sealed class ListPagingState
    {
        /// <summary>The rows per page of a new state.</summary>
        public const int DefaultPageSize = 50;

        private int _requestId;

        /// <summary>Gets the 1-based page on screen.</summary>
        public int Page { get; private set; } = 1;

        /// <summary>Gets the rows per page.</summary>
        public int PageSize { get; private set; } = DefaultPageSize;

        /// <summary>Gets a value indicating whether rows exist beyond the current page.</summary>
        public bool HasMore { get; private set; }

        /// <summary>Gets the matching row count, or <c>null</c> when it is not known.</summary>
        public int? TotalCount { get; private set; }

        /// <summary>Gets the number of pages, or <c>null</c> when the row count is not known.</summary>
        public int? TotalPages => TotalCount is { } total ? Math.Max(1, (total + PageSize - 1) / PageSize) : null;

        /// <summary>Gets a value indicating whether a previous page exists.</summary>
        public bool CanGoPrevious => Page > 1;

        /// <summary>Gets a value indicating whether a next page exists.</summary>
        public bool CanGoNext => HasMore;

        /// <summary>
        /// Creates the paging options that request <paramref name="page"/>. The row count is requested only while it
        /// is not known, because counting is an extra query on every request.
        /// </summary>
        /// <param name="page">The 1-based page to request.</param>
        public PagingOptions CreateOptions(int page)
            => new() { Page = page, PageSize = PageSize, IncludeTotalCount = TotalCount is null };

        /// <summary>Starts a request and returns the id to pass to <see cref="IsCurrent"/> when it completes.</summary>
        public int BeginRequest() => ++_requestId;

        /// <summary>Returns whether <paramref name="requestId"/> is still the latest request.</summary>
        /// <param name="requestId">The id <see cref="BeginRequest"/> returned.</param>
        public bool IsCurrent(int requestId) => requestId == _requestId;

        /// <summary>Records the response to a request for <paramref name="page"/>.</summary>
        /// <param name="page">The page that was requested.</param>
        /// <param name="paging">The response's paging metadata, or <c>null</c> when the rows are not paged.</param>
        public void Apply(int page, PagingInfo? paging)
        {
            Page = page;
            HasMore = paging?.HasMore ?? false;
            TotalCount = paging?.TotalCount ?? TotalCount;
        }

        /// <summary>Changes the rows per page and returns to the first page.</summary>
        /// <param name="pageSize">The rows per page, at least 1.</param>
        public void SetPageSize(int pageSize)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
            PageSize = pageSize;
            Page = 1;
            TotalCount = null;
        }

        /// <summary>Forgets the row count, so the next request counts again after the data may have changed.</summary>
        public void InvalidateTotalCount() => TotalCount = null;

        /// <summary>
        /// Moves back when the page on screen came back empty, as it does after the last row of the last page is
        /// deleted, and returns whether the head should request the new page.
        /// </summary>
        /// <param name="rowCount">The rows the page returned.</param>
        public bool TryStepBackFromEmptyPage(int rowCount)
        {
            if (rowCount > 0 || Page <= 1) { return false; }
            Page = Math.Min(Page - 1, TotalPages ?? int.MaxValue);
            return true;
        }
    }
}
