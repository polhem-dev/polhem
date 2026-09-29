using System.Globalization;
using Polhem.Api.Client.Definitions;
using Polhem.Api.Client.Connectors;
using Polhem.Core.Exceptions;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Core;

namespace Polhem.UI.Avalonia.Views
{
    /// <summary>
    /// The overridable resolution hooks: schema, layout, language, connector, access token and rounding context.
    /// </summary>
    /// <remarks>
    /// Every member here is `protected virtual` and exists for a host to replace. Grouping them means a
    /// subclass author sees the whole substitution surface at once instead of hunting through the view.
    /// </remarks>
    public partial class FormView
    {
        /// <summary>
        /// Resolves the <see cref="FormSchema"/> for <paramref name="progId"/> when the host did
        /// not pre-set <see cref="Schema"/>. Defaults to the cached <see cref="ClientInfo.DefineAccess"/>;
        /// override to supply a schema without touching the static <see cref="ClientInfo"/>.
        /// </summary>
        /// <param name="progId">The program identifier.</param>
        /// <param name="cancellationToken">A token that cancels the fetch.</param>
        protected virtual async Task<FormSchema?> ResolveSchemaAsync(string progId, CancellationToken cancellationToken)
            => EffectiveDefinitionLoader is { } loader
                ? await loader.GetLocalizedSchemaAsync(progId, ResolveLang(), cancellationToken).ConfigureAwait(false)
                : await ClientInfo.DefineAccess.GetFormSchemaAsync(progId, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Resolves the <see cref="FormLayout"/> the record renders from, in three steps: the
        /// <see cref="Layout"/> the host set, else the effective definition loader's assembled
        /// runtime layout, else the stored base definition fetched through
        /// <see cref="ClientInfo.DefineAccess"/>.
        /// </summary>
        /// <param name="progId">The program identifier, which doubles as the layout identifier.</param>
        /// <param name="cancellationToken">A token that cancels the fetch.</param>
        /// <returns>A layout this view owns: the view degrades it in place for the user's permissions.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no layout definition is stored under <paramref name="progId"/>. Layouts are
        /// authored at design time and saved as definition files; neither this view nor the loader
        /// generates one from the schema.
        /// </exception>
        /// <remarks>
        /// IMPORTANT: the view hides and marks read-only the fields the user may not read or update by
        /// writing into the returned layout, so every path returns an instance of its own. An override
        /// must do the same; returning a shared instance lets one view's permissions leak into another.
        /// The host-supplied path is pinned by
        /// <c>FormViewTests.EnsureDataObject_HostSuppliedLayout_LeavesHostInstanceUnchanged</c>.
        /// </remarks>
        protected virtual async Task<FormLayout> ResolveLayoutAsync(string progId, CancellationToken cancellationToken)
        {
            // The host may hand the same layout to several views, or keep using it, so the view
            // degrades a copy rather than the host's instance.
            if (Layout is not null) return Layout.Clone();

            if (EffectiveDefinitionLoader is { } loader)
            {
                return await loader
                    .GetRuntimeLayoutAsync(progId, Schema!, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            // The define cache hands back a shared instance, and the capability applier mutates the
            // layout in place, so this path clones what it fetched. The loader path clones already.
            var definition = await ClientInfo.DefineAccess.GetFormLayoutAsync(progId, cancellationToken).ConfigureAwait(false);
            return definition?.Clone()
                ?? throw new InvalidOperationException(
                    $"No FormLayout definition found for '{progId}'. Author one at design time and save "
                    + $"it as 'FormLayout/{progId}.FormLayout.xml' under the definition path, or set "
                    + $"{nameof(FormView)}.{nameof(Layout)} before loading the form.");
        }

        /// <summary>
        /// Gets or sets the assembler that turns the raw definitions the server serves into a
        /// localized schema and a runtime layout, for this view only. <c>null</c> — the default —
        /// uses <see cref="ClientInfo.DefinitionLoader"/>, which is itself <c>null</c> only when the
        /// host turned off <see cref="ClientInfo.UseDefinitionLoader"/>; with neither, the view is
        /// purely local: the schema is fetched as stored, and the layout comes from
        /// <see cref="Layout"/> or the stored definition.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Assembling the runtime definitions costs API round trips (both language layers, both
        /// layout layers). A host that supplies <see cref="Schema"/> and <see cref="Layout"/> with no
        /// backend behind them, or wants the stored definitions as they are, turns the client-wide
        /// switch off once rather than unwiring every view.
        /// </para>
        /// <para>
        /// A loader enables customized layouts, localized captions and company number formats.
        /// A per-view loader is built the way the client-wide one is:
        /// <c>new FormDefinitionLoader(ClientInfo.DefineAccess) { CompanyAccessor = () =&gt; ClientInfo.Company }</c>.
        /// Pass the company accessor as a delegate, not a value — the entered company changes over
        /// the session's life and a captured value would keep baking the previous tenant's decimals.
        /// </para>
        /// <para>
        /// The definitions are localized once, when the view loads. Switching the language does not
        /// re-localize a view already on screen; reopen it to see the new language.
        /// </para>
        /// </remarks>
        public FormDefinitionLoader? DefinitionLoader { get; set; }

        /// <summary>
        /// The loader this view assembles through: its own, else the client-wide one, else none.
        /// </summary>
        private FormDefinitionLoader? EffectiveDefinitionLoader => DefinitionLoader ?? ClientInfo.DefinitionLoader;

        /// <summary>
        /// Resolves the language the form renders in. Defaults to the UI culture, which
        /// <see cref="ClientInfo.ApplyLoginResult"/> sets to the signed-in user's culture, and which
        /// <see cref="Polhem.Definition.Language.LanguageResourceStringLocalizer{T}"/> falls back to as
        /// well. Only consulted when a definition loader is in effect.
        /// </summary>
        protected virtual string ResolveLang() => CultureInfo.CurrentUICulture.Name;

        /// <summary>
        /// Resolves the <see cref="FormApiConnector"/> for the load / save round-trips.
        /// Override to bypass <see cref="ClientInfo"/>.
        /// </summary>
        protected virtual FormApiConnector ResolveFormConnector(string progId)
            => ClientInfo.CreateFormApiConnector(progId);

        /// <summary>Resolves the access token. Override to plug in a different session source.</summary>
        protected virtual Guid ResolveAccessToken() => ClientInfo.AccessToken;

        /// <summary>
        /// Resolves the rounding context used to round live-preview computed fields and format
        /// amount/quantity cells (Tier 2). The default pulls the currency/unit masters through the cached
        /// <see cref="ClientInfo.DefineAccess"/> and the company from <see cref="ClientInfo.Company"/>;
        /// each part is optional and degrades to framework-default decimal places when absent. Override
        /// to supply a context without touching the static <see cref="ClientInfo"/> (the unit tests do).
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the fetches.</param>
        protected virtual async Task<RoundingContext> ResolveRoundingContextAsync(CancellationToken cancellationToken)
        {
            return new RoundingContext
            {
                Company = ClientInfo.Company,
                CurrencySettings = await TryResolveSettingAsync(() => ClientInfo.DefineAccess.GetCurrencySettingsAsync(cancellationToken))
                    .ConfigureAwait(true),
                UnitSettings = await TryResolveSettingAsync(() => ClientInfo.DefineAccess.GetUnitSettingsAsync(cancellationToken))
                    .ConfigureAwait(true),
            };
        }

        // Best-effort fetch of an optional definition master: a missing master already returns null, and
        // a permission/API error must not break the form — live preview simply falls back to
        // framework-default decimals for that kind (the server still rounds authoritatively on save).
        private static async Task<T?> TryResolveSettingAsync<T>(Func<Task<T>> fetch) where T : class
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
