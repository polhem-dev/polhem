using System.Globalization;
using Polhem.Core;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.UI.Core.Criteria;
using Polhem.Web.Blazor.Controls;

namespace Polhem.Web.Blazor.Components
{
    /// <summary>
    /// The list's query conditions: the toolbar's Query button, the pane that edits them, the chips of the conditions in
    /// effect, and the filter the list reads its pages with.
    /// </summary>
    /// <remarks>
    /// The pane edits <see cref="Criteria"/> as the user types, but the list reads with the filter taken when the query
    /// last ran, so paging and sorting read the same rows while the user is still editing; the chips show that filter's
    /// conditions. Running the query reads the first page and counts again. In the compact layout the pane covers the
    /// list, so running the query closes it. The rules match the Avalonia <c>ListView</c>.
    /// </remarks>
    public abstract partial class FormPageBase
    {
        private QueryCriteria _criteria = new();
        private IReadOnlyList<CriteriaField> _criteriaFields = [];
        private FilterNode? _criteriaFilter;
        private IReadOnlyList<CriteriaChip> _criteriaChips = [];
        private bool _isQueryPaneOpen;
        private ColumnFilterCriteria? _columnFilters;

        /// <summary>Gets the conditions the pane edits; a list starts with none.</summary>
        protected QueryCriteria Criteria => _criteria;

        /// <summary>Gets the fields a condition can be added for, which the user may read.</summary>
        protected IReadOnlyList<CriteriaField> CriteriaFields => _criteriaFields;

        /// <summary>Gets the conditions in effect, as the chips above the list show them.</summary>
        protected IReadOnlyList<CriteriaChip> CriteriaChips => _criteriaChips;

        /// <summary>
        /// Gets a value indicating whether the UI package renders the query pane, the chips and the saved queries.
        /// Defaults to <see langword="false"/>: a page that cannot show the conditions does not read the list with a
        /// saved default query the user would not see.
        /// </summary>
        protected virtual bool SupportsQueryPane => false;

        /// <summary>Gets a value indicating whether any condition is in effect, a column filter included.</summary>
        protected bool HasConditions => _criteriaChips.Count > 0;

        /// <summary>Gets a value indicating whether the toolbar offers the Query button: on the list, when a field can take a condition.</summary>
        protected bool ShowsQueryButton => SupportsQueryPane && !_isShowingRecord && _criteriaFields.Count > 0;

        /// <summary>Gets a value indicating whether the query pane is open.</summary>
        protected bool IsQueryPaneOpen => _isQueryPaneOpen && ShowsQueryButton;

        /// <summary>Gets the localized caption of the Query button, with the number of conditions in effect.</summary>
        protected string QueryButtonText => _criteriaChips.Count > 0
            ? string.Format(CultureInfo.CurrentCulture, Text(PolhemUIText.QueryWithCount), _criteriaChips.Count)
            : Text(PolhemUIText.Query);

        /// <summary>Opens the query pane, or closes it when it is open.</summary>
        protected void ToggleQueryPane() => _isQueryPaneOpen = !_isQueryPaneOpen;

        /// <summary>Opens the query pane.</summary>
        protected void OpenQueryPane() => _isQueryPaneOpen = true;

        /// <summary>Closes the query pane; the conditions stay as they were edited.</summary>
        protected void CloseQueryPane() => _isQueryPaneOpen = false;

        /// <summary>
        /// Makes the conditions the list's filter and reads the first page. Every required row must have a value; the
        /// pane checks that before it asks.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task RunQueryAsync(CancellationToken cancellationToken = default)
        {
            if (IsListLocked || !_criteria.IsComplete) return;
            _criteriaNotice = null;
            ApplyCriteriaFilter();
            // The pane covers the list in the compact layout, and would hide the rows the query just read.
            if (EffectiveIsCompact) _isQueryPaneOpen = false;
            await ReadFilteredListAsync(cancellationToken).ConfigureAwait(true);
        }

        /// <summary>Removes the condition behind a chip and runs the query again.</summary>
        /// <param name="row">The row the chip shows.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task RemoveConditionAsync(CriteriaRow row, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(row);
            if (IsListLocked || !_criteria.Remove(row)) return;
            await RunQueryAsync(cancellationToken).ConfigureAwait(true);
        }

        /// <summary>
        /// Removes every condition that is not required and runs the query again; with a required row left empty it
        /// opens the pane instead.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task ClearConditionsAsync(CancellationToken cancellationToken = default)
        {
            if (IsListLocked) return;
            _criteria.Clear();
            if (!_criteria.IsComplete)
            {
                _isQueryPaneOpen = true;
                return;
            }
            await RunQueryAsync(cancellationToken).ConfigureAwait(true);
        }

        /// <summary>
        /// Returns today in the signed-in user's time zone, which the date periods resolve against, as the request's
        /// dates are read in that zone; the server's date when no zone is known.
        /// </summary>
        /// <returns>Today.</returns>
        protected DateOnly ResolveToday()
            => UserContext?.User?.TimeZone is { Length: > 0 } zone
                ? FrameworkClock.Today(zone)
                : DateOnly.FromDateTime(DateTime.Now);

        private void ResetCriteria(FormSchema? schema)
        {
            _criteria = new QueryCriteria();
            _criteriaFields = schema is null || !SupportsQueryPane ? [] : CriteriaField.FromQueryFields(schema, UserContext?.Capabilities);
            _criteriaFilter = null;
            _criteriaChips = [];
            _isQueryPaneOpen = false;
            ResetUserQueries();
        }

        private void ApplyCriteriaFilter()
        {
            _criteriaFilter = GetListFilterConverter.ToFilter(_criteria, ResolveToday(),
                CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);
            _criteriaChips = _criteria.Rows
                .Where(r => r.HasValue)
                .Select(r => new CriteriaChip(r, CriteriaRowSummary.Describe(r, Text)))
                .ToArray();
        }

        private FilterNode? CreateListFilter() => _criteriaFilter;
    }
}
