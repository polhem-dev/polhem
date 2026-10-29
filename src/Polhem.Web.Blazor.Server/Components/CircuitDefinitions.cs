using System.Globalization;
using Polhem.Api.Client;
using Polhem.Api.Client.Definitions;
using Polhem.Core.Exceptions;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.DependencyInjection;

namespace Polhem.Web.Blazor.Server.Components
{
    /// <summary>
    /// Loads definitions the way every component of this package does: through a
    /// <see cref="FormDefinitionLoader"/> when the host keeps <see cref="PolhemBlazorOptions.UseDefinitionLoader"/>
    /// on, so captions follow the circuit's UI culture, and exactly as stored otherwise.
    /// </summary>
    /// <remarks>
    /// <see cref="FormPage"/> and <see cref="Polhem.Web.Blazor.Server.Controls.Editors.LookupDialog"/> both load
    /// through here, so a lookup's title and columns read in the same language as the form that opened it.
    /// </remarks>
    internal static class CircuitDefinitions
    {
        /// <summary>
        /// Returns the loader a component uses: <paramref name="componentLoader"/> when set, else one over the
        /// circuit's client when the host enables it, else <c>null</c>.
        /// </summary>
        /// <param name="client">The circuit's client.</param>
        /// <param name="options">The package options.</param>
        /// <param name="componentLoader">A loader passed to the component, or <c>null</c>.</param>
        internal static FormDefinitionLoader? CreateLoader(
            PolhemApiClient client, PolhemBlazorOptions options, FormDefinitionLoader? componentLoader)
            => componentLoader
                ?? (options.UseDefinitionLoader
                    ? new FormDefinitionLoader(new ClientDefineAccess(client.System), options.DefaultLanguage)
                    : null);

        /// <summary>
        /// Loads the schema of <paramref name="progId"/>: localized through <paramref name="loader"/> when one
        /// is given, exactly as stored otherwise.
        /// </summary>
        /// <param name="client">The circuit's client.</param>
        /// <param name="loader">The loader, or <c>null</c>.</param>
        /// <param name="progId">The program identifier.</param>
        /// <returns>The schema, or <c>null</c> when none is stored under that program.</returns>
        internal static async Task<FormSchema?> LoadSchemaAsync(
            PolhemApiClient client, FormDefinitionLoader? loader, string progId)
        {
            if (loader is null)
                return await client.System.GetDefineAsync<FormSchema>(DefineType.FormSchema, [progId]).ConfigureAwait(true);
            return await loader.GetLocalizedSchemaAsync(progId, CultureInfo.CurrentUICulture.Name).ConfigureAwait(true);
        }

        /// <summary>
        /// Loads the layout a record of <paramref name="progId"/> renders from: assembled through
        /// <paramref name="loader"/> when one is given, so the tenant's customization and the number formats apply,
        /// exactly as stored otherwise. Either way the caller owns the returned instance and may degrade it in place.
        /// </summary>
        /// <param name="client">The circuit's client.</param>
        /// <param name="loader">The loader, or <c>null</c>.</param>
        /// <param name="progId">The program identifier.</param>
        /// <param name="schema">The program's schema, as <see cref="LoadSchemaAsync"/> returned it.</param>
        /// <exception cref="InvalidOperationException">No layout is stored under <paramref name="progId"/>.</exception>
        internal static async Task<FormLayout> LoadFormLayoutAsync(
            PolhemApiClient client, FormDefinitionLoader? loader, string progId, FormSchema schema)
        {
            if (loader is not null)
                return await loader.GetRuntimeLayoutAsync(progId, schema).ConfigureAwait(true);

            // A layout is authored at design time, so a missing one is a configuration error rather than a cue to
            // derive one from the schema.
            return await client.System
                .GetDefineAsync<FormLayout>(DefineType.FormLayout, [progId])
                .ConfigureAwait(true)
                ?? throw new InvalidOperationException(
                    $"No FormLayout definition found for '{progId}'. Author one at design time "
                    + $"and save it as 'FormLayout/{progId}.FormLayout.xml' under the definition path.");
        }

        /// <summary>
        /// Builds the rounding context a form previews numbers with: the entered company from the loader's
        /// <see cref="FormDefinitionLoader.CompanyAccessor"/> or else the circuit's user context, and the currency and unit masters, as the
        /// Avalonia <c>FormView</c> does.
        /// </summary>
        /// <remarks>
        /// The masters are optional and fetched best effort: a missing master is <c>null</c>, and a refused
        /// or failed fetch must not break the form. The preview then falls back to framework-default
        /// decimals for that kind, and the server still rounds authoritatively on save.
        /// </remarks>
        /// <param name="client">The circuit's client.</param>
        /// <param name="loader">The loader, or <c>null</c>.</param>
        /// <param name="company">The company the circuit's user entered, used when the loader names none.</param>
        internal static async Task<RoundingContext> LoadRoundingContextAsync(PolhemApiClient client, FormDefinitionLoader? loader,
            CompanyInfo? company)
        {
            var access = new ClientDefineAccess(client.System);
            return new RoundingContext
            {
                Company = loader?.CompanyAccessor?.Invoke() ?? company,
                CurrencySettings = await TryLoadAsync(() => access.GetCurrencySettingsAsync()).ConfigureAwait(true),
                UnitSettings = await TryLoadAsync(() => access.GetUnitSettingsAsync()).ConfigureAwait(true),
            };
        }

        private static async Task<T?> TryLoadAsync<T>(Func<Task<T>> fetch) where T : class
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
    }
}
