using System.Globalization;
using Polhem.Api.Contracts.Form;
using Polhem.Definition.Language;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Core.Criteria;

namespace Polhem.UI.Avalonia.Views
{
    /// <summary>
    /// Saved-query half of <see cref="ListView"/>: the <see cref="UserQueryBar"/> at the top of the query pane, applying
    /// a saved query, and opening the list with the user's default one.
    /// </summary>
    /// <remarks>
    /// The bar is shown only when <see cref="FormConnector"/> also implements <see cref="IUserQueryConnector"/> and the
    /// form offers fields to query.
    /// </remarks>
    public partial class ListView
    {
        private UserQueryBar? _userQueryBar;

        /// <summary>Gets the saved-query bar, or <c>null</c> when the list offers none.</summary>
        internal UserQueryBar? UserQueryBar => _userQueryBar;

        private async Task AttachUserQueriesAsync(CancellationToken cancellationToken)
        {
            if (FormConnector is not IUserQueryConnector connector || _criteriaPanel.Fields.Count == 0) return;

            var bar = new UserQueryBar(connector, () => UserQueryCodec.Serialize(_criteria));
            bar.ApplyRequested += (_, entry) => ApplyUserQuery(entry, search: true);
            bar.Failed += (_, ex) => ReportError(ex);
            _criteriaPanel.CriteriaChanged += (_, _) => bar.RefreshModified();
            _userQueryBar = bar;
            _criteriaPanel.HeaderContent = bar;

            UserQueryEntry? defaultQuery;
            try
            {
                defaultQuery = await bar.LoadAsync(cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Boundary: saved queries that cannot be read are reported, and the list still opens without them.
                ReportError(ex);
                return;
            }

            if (defaultQuery is not null && ApplyUserQuery(defaultQuery, search: false))
                ApplyActiveFilter();
        }

        /// <summary>
        /// Replaces the conditions with a saved query's, telling the user what no longer applies, and runs the query
        /// when <paramref name="search"/> is set.
        /// </summary>
        /// <param name="entry">The saved query.</param>
        /// <param name="search">Whether to run the query.</param>
        /// <returns><see langword="false"/> when the stored criteria could not be read and nothing changed.</returns>
        internal bool ApplyUserQuery(UserQueryEntry entry, bool search)
        {
            var fields = _criteriaPanel.Fields;
            var loaded = UserQueryCodec.Deserialize(entry.Criteria, fields);
            if (!loaded.IsSupported)
            {
                _userQueryBar?.SetCurrent(null);
                _criteriaPanel.ShowWarning(UIText.Get(PolhemUIText.UserQueryUnreadable));
                return false;
            }

            _criteria = loaded.Criteria;
            _criteriaPanel.Bind(fields, _criteria);
            _userQueryBar?.SetCurrent(entry);
            if (loaded.Skipped.Count > 0)
            {
                var names = loaded.Skipped.Select(skip =>
                    fields.FirstOrDefault(f => string.Equals(f.FieldName, skip.FieldName, StringComparison.OrdinalIgnoreCase))?.Caption
                    ?? skip.FieldName);
                _criteriaPanel.ShowWarning(string.Format(CultureInfo.CurrentCulture, UIText.Get(PolhemUIText.UserQuerySkipped),
                    string.Join(UIText.Get(PolhemUIText.CriteriaValueSeparator), names.Distinct(StringComparer.Ordinal))));
            }
            if (search)
                _ = SearchAsync(keepPaneOpen: loaded.Skipped.Count > 0);
            return true;
        }
    }
}
