using Polhem.Api.Contracts.Form;
using Polhem.Core.Exceptions;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Core;
using Polhem.UI.Core.Definitions;

namespace Polhem.UI.Avalonia.Views
{
    /// <summary>
    /// The overridable resolution hooks: schema, layout, connector, user context and rounding context.
    /// </summary>
    /// <remarks>
    /// Every member here is `protected virtual` and exists for a host to replace. Grouping them means a
    /// subclass author sees the whole substitution surface at once instead of hunting through the view.
    /// </remarks>
    public partial class FormView
    {
        /// <summary>
        /// Resolves the <see cref="FormSchema"/> for <paramref name="progId"/> when the host did
        /// not pre-set <see cref="Schema"/>. Defaults to the effective definition source, which decides
        /// the language; returns <c>null</c> when there is none.
        /// </summary>
        /// <param name="progId">The program identifier.</param>
        /// <param name="cancellationToken">A token that cancels the fetch.</param>
        protected virtual async Task<FormSchema?> ResolveSchemaAsync(string progId, CancellationToken cancellationToken)
            => EffectiveDefinitionSource is { } source
                ? await source.GetFormSchemaAsync(progId, cancellationToken).ConfigureAwait(false)
                : null;

        /// <summary>
        /// Resolves the <see cref="FormLayout"/> the record renders from: the <see cref="Layout"/> the host
        /// set, else the one the effective definition source returns.
        /// </summary>
        /// <param name="progId">The program identifier, which doubles as the layout identifier.</param>
        /// <param name="cancellationToken">A token that cancels the fetch.</param>
        /// <returns>A layout this view owns: the view degrades it in place for the user's permissions.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the host set no layout and there is no definition source to fetch one from. Layouts are
        /// authored at design time and saved as definition files; this view does not generate one from the schema.
        /// </exception>
        /// <remarks>
        /// IMPORTANT: the view hides and marks read-only the fields the user may not read or update by
        /// writing into the returned layout, so every path returns an instance of its own. An override
        /// must do the same; returning a shared instance lets one view's permissions leak into another.
        /// The host-supplied path is pinned by
        /// <c>FormViewTests.EnsureDataObject_HostSuppliedLayout_LeavesHostInstanceUnchanged</c>, and
        /// <see cref="IFormDefinitionSource.GetFormLayoutAsync"/> requires a source to return the caller's own instance.
        /// </remarks>
        protected virtual async Task<FormLayout> ResolveLayoutAsync(string progId, CancellationToken cancellationToken)
        {
            // The host may hand the same layout to several views, or keep using it, so the view
            // degrades a copy rather than the host's instance.
            if (Layout is not null) return Layout.Clone();

            if (EffectiveDefinitionSource is { } source)
                return await source.GetFormLayoutAsync(progId, Schema!, cancellationToken).ConfigureAwait(false);

            throw new InvalidOperationException(
                $"No FormLayout for '{progId}': set {nameof(FormView)}.{nameof(Layout)} before loading the form, "
                + $"or register an {nameof(IFormDefinitionSource)}.");
        }

        /// <summary>
        /// Gets or sets the source the view fetches its definitions from, for this view only. <c>null</c>, the
        /// default, uses the <see cref="IFormDefinitionSource"/> registered in <see cref="UIServices.Provider"/>;
        /// with neither, the view is purely local and renders the <see cref="Schema"/> and <see cref="Layout"/> the
        /// host set.
        /// </summary>
        /// <remarks>
        /// The definitions are fetched once, when the view loads. Switching the language does not re-localize a
        /// view already on screen; reopen it to see the new language.
        /// </remarks>
        public IFormDefinitionSource? DefinitionSource { get; set; }

        /// <summary>
        /// The source this view fetches through: its own, else the registered one, else none.
        /// </summary>
        private IFormDefinitionSource? EffectiveDefinitionSource
            => DefinitionSource ?? UIServices.Get<IFormDefinitionSource>();

        /// <summary>
        /// Resolves the connector for the load / save round-trips when the host did not set
        /// <see cref="FormConnector"/>. Defaults to the <see cref="IFormApiConnectorFactory"/> registered in
        /// <see cref="UIServices.Provider"/>; returns <c>null</c> when there is none.
        /// </summary>
        /// <param name="progId">The program identifier.</param>
        protected virtual IFormApiConnector? ResolveFormConnector(string progId)
            => UIServices.Get<IFormApiConnectorFactory>()?.Create(progId);

        /// <summary>
        /// Resolves the signed-in user's context: the capabilities that degrade the layout and the toolbar, and the
        /// entered company. Defaults to the <see cref="IUserContext"/> registered in <see cref="UIServices.Provider"/>;
        /// <c>null</c> treats the user as unrestricted.
        /// </summary>
        protected virtual IUserContext? ResolveUserContext() => UIServices.Get<IUserContext>();

        /// <summary>
        /// Resolves the rounding context used to round live-preview computed fields and format
        /// amount/quantity cells (Tier 2). The default reads the currency and unit masters from the effective
        /// definition source and the company from <see cref="ResolveUserContext"/>; each part is optional and
        /// degrades to framework-default decimal places when absent. Override to supply a context of your own
        /// (the unit tests do).
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the fetches.</param>
        protected virtual async Task<RoundingContext> ResolveRoundingContextAsync(CancellationToken cancellationToken)
        {
            var source = EffectiveDefinitionSource;
            return new RoundingContext
            {
                Company = ResolveUserContext()?.Company,
                CurrencySettings = source is null ? null
                    : await TryResolveSettingAsync(() => source.GetCurrencySettingsAsync(cancellationToken)).ConfigureAwait(true),
                UnitSettings = source is null ? null
                    : await TryResolveSettingAsync(() => source.GetUnitSettingsAsync(cancellationToken)).ConfigureAwait(true),
            };
        }

        // Best-effort fetch of an optional definition master: a missing master already returns null, and
        // a permission/API error must not break the form — live preview simply falls back to
        // framework-default decimals for that kind (the server still rounds authoritatively on save).
        private static async Task<T?> TryResolveSettingAsync<T>(Func<Task<T?>> fetch) where T : class
        {
            try
            {
                return await fetch().ConfigureAwait(true);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (ForbiddenException)
            {
                return null;
            }
        }

        /// <summary>
        /// Called after the form mode changed and was broadcast to the scope. Refreshes the
        /// mode-dependent toolbar.
        /// </summary>
        /// <param name="formMode">The new form mode.</param>
        protected virtual void OnFormModeChanged(SingleFormMode formMode) => UpdateToolbarState();
    }
}
