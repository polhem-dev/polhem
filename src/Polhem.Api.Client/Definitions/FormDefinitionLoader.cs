using Polhem.Definition.Customization;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;

namespace Polhem.Api.Client.Definitions
{
    /// <summary>
    /// Assembles the runtime form definitions a UI needs — a localized <see cref="FormSchema"/> and
    /// a <see cref="FormLayout"/> — out of the raw definitions the server serves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The APIs hand out definitions exactly as stored: no localization, no customization overlay,
    /// no generation. Assembling them is the caller's job, and doing it here rather than in each UI
    /// head means every head performs the same steps in the same order:
    /// </para>
    /// <list type="number">
    ///   <item><description>fetch the raw schema;</description></item>
    ///   <item><description>fetch both language layers for every namespace the schema references, and localize the schema through <see cref="FormSchemaLocalizer"/>;</description></item>
    ///   <item><description>bake the company's number formats onto the schema through <see cref="NumberFormatApplier"/>;</description></item>
    ///   <item><description>fetch both layout layers and pick between them with <see cref="CustomizeOverlay"/>, failing when neither exists;</description></item>
    ///   <item><description>take the layout's captions from the localized schema, so a layout file describes structure only.</description></item>
    /// </list>
    /// <para>
    /// Which tenant's customization arrives is never asked for here — the server derives it from the
    /// session. This class only decides how the two layers combine.
    /// </para>
    /// <para>
    /// Every call localizes once, in the language it is given, and returns a copy. Nothing re-localizes
    /// a definition already handed out, so a view built from one keeps its language after the user's
    /// culture changes; the new language shows in views loaded afterwards.
    /// </para>
    /// </remarks>
    public sealed class FormDefinitionLoader
    {
        private readonly ClientDefineAccess _defineAccess;
        private readonly string? _defaultLanguage;

        /// <summary>
        /// Initializes a new <see cref="FormDefinitionLoader"/>.
        /// </summary>
        /// <param name="defineAccess">The client define access used to fetch raw definitions.</param>
        /// <param name="defaultLanguage">
        /// The default language, the last hop of the fall-back chain. <c>null</c> — the default —
        /// reads <see cref="ApiClientInfo.DefaultLanguage"/>, the value the server advertised, at
        /// each call; an empty string drops the hop.
        /// </param>
        public FormDefinitionLoader(ClientDefineAccess defineAccess, string? defaultLanguage = null)
        {
            _defineAccess = defineAccess ?? throw new ArgumentNullException(nameof(defineAccess));
            _defaultLanguage = defaultLanguage;
        }

        private string DefaultLanguage => _defaultLanguage ?? ApiClientInfo.DefaultLanguage;

        /// <summary>
        /// Gets the accessor supplying the company whose decimal places the number formats are baked
        /// from. <c>null</c> — the default — bakes the framework defaults.
        /// </summary>
        /// <remarks>
        /// A delegate rather than a <see cref="CompanyInfo"/> value on purpose: the entered company
        /// changes over a session's life (<c>EnterCompany</c> / <c>LeaveCompany</c>), and a value
        /// captured at construction would keep baking the previous tenant's decimals. Heads set
        /// <c>CompanyAccessor = () =&gt; ClientInfo.Company</c>.
        /// </remarks>
        public Func<CompanyInfo?>? CompanyAccessor { get; init; }

        /// <summary>
        /// Fetches the raw schema for <paramref name="progId"/> and returns a localized copy.
        /// </summary>
        /// <param name="progId">The program identifier.</param>
        /// <param name="lang">
        /// The BCP-47 language code. The captions and option sets resolve through
        /// <see cref="LanguageFallback.GetChain"/>: this culture, its parents, then the default
        /// language; a key no culture declares keeps the schema's own base text. Empty starts the
        /// chain at the default language.
        /// </param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>A schema safe to mutate — the cached instance is never handed out.</returns>
        /// <remarks>
        /// <para>
        /// WARNING: The blank-language path clones too, and must keep doing so. It used to return the
        /// cached instance directly, which made the sentence above false for exactly the case that
        /// reaches it most often: <c>CultureInfo.InvariantCulture.Name</c> is the empty string, so a
        /// caller passing the current UI culture lands here whenever no culture is set. Callers were
        /// told the result was safe to mutate and mutated a process-wide shared schema.
        /// </para>
        /// <para>
        /// Number formats are baked on both paths, not just the localized one: the format a numeric
        /// field renders with has nothing to do with which language the captions are in.
        /// </para>
        /// </remarks>
        public async Task<FormSchema> GetLocalizedSchemaAsync(string progId, string lang, CancellationToken cancellationToken = default)
        {
            var raw = await _defineAccess.GetFormSchemaAsync(progId, cancellationToken).ConfigureAwait(false);
            // The client define cache hands back a shared instance; every path clones before returning.
            var schema = raw.Clone();
            string defaultLanguage = DefaultLanguage;
            if (LanguageFallback.GetChain(lang, defaultLanguage).Count > 0)
            {
                var languageService = await BuildLanguageServiceAsync(schema, lang, defaultLanguage, cancellationToken).ConfigureAwait(false);
                new FormSchemaLocalizer(languageService).Localize(schema, lang);
            }

            // The server serves definitions exactly as stored, so the company's decimal places
            // are applied here. Amounts and quantities/weights are left for the UI to resolve per
            // row from their currency or unit. `Bake` skips those itself. A schema with no numeric
            // field skips the bake and the company lookup with it; the clone above stays, because
            // the result must be safe to mutate either way.
            if (NumberFormatApplier.HasNumericField(schema))
            {
                NumberFormatApplier.Bake(schema, CompanyAccessor?.Invoke());
            }
            return schema;
        }

        /// <summary>
        /// Returns the runtime layout for <paramref name="progId"/>: the tenant's layout definition
        /// when it has one, else the base definition — with the captions taken from
        /// <paramref name="localizedSchema"/> either way.
        /// </summary>
        /// <param name="progId">The program identifier.</param>
        /// <param name="localizedSchema">The localized schema, from <see cref="GetLocalizedSchemaAsync"/>.</param>
        /// <param name="layoutId">The layout identifier; empty resolves to <paramref name="progId"/>.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when neither layer stores a layout definition. Layouts are authored at design time
        /// and saved as definition files; the runtime never generates one from the schema.
        /// </exception>
        public async Task<FormLayout> GetRuntimeLayoutAsync(string progId, FormSchema localizedSchema, string layoutId = "",
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(localizedSchema);

            string effectiveLayoutId = string.IsNullOrWhiteSpace(layoutId) ? progId : layoutId;
            var customize = await _defineAccess.GetCustomizeFormLayoutAsync(progId, effectiveLayoutId, cancellationToken).ConfigureAwait(false);
            var @base = await _defineAccess.GetFormLayoutAsync(effectiveLayoutId, cancellationToken).ConfigureAwait(false);

            var definition = CustomizeOverlay.PickFormLayout(customize, @base)
                ?? throw new InvalidOperationException(
                    $"No FormLayout definition found for layout '{effectiveLayoutId}' (progId '{progId}'), "
                    + $"in either the tenant customization layer or the base layer. Author one at design "
                    + $"time and save it as 'FormLayout/{effectiveLayoutId}.FormLayout.xml' under the "
                    + $"definition path.");

            // Definitions come from the client define cache, so clone before the applier mutates.
            var layout = definition.Clone();
            FormLayoutCaptionApplier.Apply(layout, localizedSchema);
            return layout;
        }

        /// <summary>
        /// Fetches the <c>Menu</c> language namespace for <paramref name="lang"/> and returns a
        /// localizer that resolves menu captions from it.
        /// </summary>
        /// <param name="lang">The BCP-47 culture; empty starts the chain at the default language.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>
        /// A localizer over the fetched resources; pass the same <paramref name="lang"/> to
        /// <see cref="MenuLocalizer.GetCaption"/>.
        /// </returns>
        /// <remarks>
        /// Both language layers are fetched in every culture of the fall-back chain up front, so
        /// rendering the menu costs no further round trips. The menu itself is fetched separately,
        /// through <see cref="ClientDefineAccess.GetMenuSettingsAsync"/>, and is not modified.
        /// </remarks>
        public async Task<MenuLocalizer> GetMenuLocalizerAsync(string lang, CancellationToken cancellationToken = default)
        {
            string defaultLanguage = DefaultLanguage;
            var snapshot = await FetchLayersAsync([MenuLocalizer.Namespace], lang, defaultLanguage, cancellationToken)
                .ConfigureAwait(false);
            return new MenuLocalizer(new SnapshotLanguageService(snapshot, defaultLanguage));
        }

        /// <summary>
        /// Fetches both language layers for every namespace <paramref name="schema"/> reads from and
        /// wraps them in a synchronous service.
        /// </summary>
        /// <remarks>
        /// The namespaces are the schema's own <c>ProgId</c> plus any namespace named by a
        /// fully-qualified <see cref="FormField.LangEnumName"/> (<c>"Common.Gender"</c>), which is how a
        /// schema borrows a shared option set. Each is fetched in every culture of the fall-back
        /// chain, so the service applies the same chain the server does.
        /// </remarks>
        private async Task<SnapshotLanguageService> BuildLanguageServiceAsync(FormSchema schema, string lang,
            string defaultLanguage, CancellationToken cancellationToken)
        {
            var snapshot = await FetchLayersAsync(CollectNamespaces(schema), lang, defaultLanguage, cancellationToken)
                .ConfigureAwait(false);
            return new SnapshotLanguageService(snapshot, defaultLanguage);
        }

        /// <summary>
        /// Fetches the base and customization layers of <paramref name="namespaces"/> in every
        /// culture of the fall-back chain.
        /// </summary>
        private async Task<Dictionary<string, LanguageLayers>> FetchLayersAsync(IEnumerable<string> namespaces,
            string lang, string defaultLanguage, CancellationToken cancellationToken)
        {
            var snapshot = new Dictionary<string, LanguageLayers>(StringComparer.Ordinal);
            foreach (string language in LanguageFallback.GetChain(lang, defaultLanguage))
            {
                foreach (string ns in namespaces)
                {
                    var customize = await _defineAccess.GetCustomizeLanguageAsync(language, ns, cancellationToken).ConfigureAwait(false);
                    var @base = await SafeGetLanguageAsync(language, ns, cancellationToken).ConfigureAwait(false);
                    snapshot[SnapshotLanguageService.BuildKey(language, ns)] = new LanguageLayers(@base, customize);
                }
            }
            return snapshot;
        }

        private static HashSet<string> CollectNamespaces(FormSchema schema)
        {
            var namespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(schema.ProgId))
                namespaces.Add(schema.ProgId);

            if (schema.Tables == null)
                return namespaces;

            var enumNames = schema.Tables
                .Where(table => table.Fields != null)
                .SelectMany(table => table.Fields!)
                .Select(field => field.LangEnumName)
                .Where(name => !string.IsNullOrWhiteSpace(name));
            foreach (string name in enumNames)
            {
                int dot = name.IndexOf('.');
                // A bare name resolves against the schema's own namespace, already added above.
                if (dot > 0) namespaces.Add(name.Substring(0, dot));
            }
            return namespaces;
        }

        /// <summary>
        /// A namespace with no base resource is normal (a schema may be translated only in the
        /// customization layer, or not at all), so a missing file is an answer rather than a fault.
        /// </summary>
        private async Task<LanguageResource?> SafeGetLanguageAsync(string lang, string ns, CancellationToken cancellationToken)
        {
            try
            {
                return await _defineAccess.GetLanguageAsync(lang, ns, cancellationToken).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }
    }
}
