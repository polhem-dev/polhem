using System.Globalization;
using Avalonia.Controls;
using Polhem.Core;
using Polhem.Definition.Filters;
using Polhem.Definition.Language;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Core.Criteria;
using Polhem.UI.Core.Permissions;

namespace Polhem.UI.Avalonia.Views
{
    /// <summary>
    /// Query-condition half of <see cref="ListView"/>: the toolbar's query button, the <see cref="CriteriaPanel"/>
    /// it shows, and the filter the list reads its pages with.
    /// </summary>
    public partial class ListView
    {
        private readonly QueryCriteria _criteria = new();
        private Button _queryButton = null!;
        private CriteriaPanel _criteriaPanel = null!;
        private FilterNode? _activeFilter;
        private int _activeConditions;

        /// <summary>Gets the conditions the user builds; a list starts with none.</summary>
        internal QueryCriteria Criteria => _criteria;

        /// <summary>Gets the panel that edits <see cref="Criteria"/>.</summary>
        internal CriteriaPanel CriteriaPanel => _criteriaPanel;

        /// <summary>Gets the toolbar button that shows the panel.</summary>
        internal Button QueryButton => _queryButton;

        private void CreateCriteriaChrome()
        {
            _criteriaPanel = new CriteriaPanel { IsVisible = false, Today = ResolveToday };
            _criteriaPanel.SearchRequested += (_, _) => _ = RunQueryAsync();

            _queryButton = new Button { Content = UIText.Get(PolhemUIText.Query), IsVisible = false };
            _queryButton.Click += (_, _) => _criteriaPanel.IsVisible = !_criteriaPanel.IsVisible;
        }

        // The fields "add condition" offers: the schema's query fields a condition can be entered for, less the
        // sensitive ones the user cannot read (the list hides those columns as well).
        private void AttachCriteria()
        {
            var schema = Schema!;
            var capabilities = ResolveUserContext()?.Capabilities;
            var tableName = schema.MasterTable?.TableName ?? string.Empty;
            var fields = schema.GetQueryFields()
                .Where(f => ElementCapabilityResolver.Default.ResolveField(schema, f.FieldName, tableName, capabilities).Visible)
                .Select(CriteriaField.FromField)
                .OfType<CriteriaField>()
                .ToArray();
            _criteriaPanel.Bind(fields, _criteria);
            _queryButton.IsVisible = fields.Length > 0;
        }

        /// <summary>
        /// Converts the conditions into the list's filter and reads the first page with it. The filter stays until
        /// the next query, so paging and sorting read the same rows even while the user edits the conditions.
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
            _paging.InvalidateTotalCount();
            await LoadPageAsync(1, CancellationToken.None).ConfigureAwait(true);
        }

        // Relative dates resolve against the signed-in user's today, the zone the request's dates are read in.
        private DateOnly ResolveToday()
            => ResolveUserContext()?.User?.TimeZone is { Length: > 0 } zone
                ? FrameworkClock.Today(zone)
                : DateOnly.FromDateTime(DateTime.Now);
    }
}
