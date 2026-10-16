using System.Globalization;
using Polhem.Api.Client;
using Polhem.Api.Client.Definitions;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.UI.Core.Definitions;

namespace Polhem.UI.ApiBridge.Definitions
{
    /// <summary>
    /// Reads form definitions from a Polhem backend.
    /// </summary>
    /// <remarks>
    /// With a definition loader, the schema is localized to <see cref="CultureInfo.CurrentUICulture"/> and the
    /// layout is the runtime layout with the tenant's customization; without one, both are returned as stored.
    /// Both accessors are delegates because the head replaces them on an endpoint change or a new sign-in.
    /// </remarks>
    public sealed class ApiFormDefinitionSource : IFormDefinitionSource
    {
        private readonly Func<ClientDefineAccess> _defineAccess;
        private readonly Func<FormDefinitionLoader?> _loader;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiFormDefinitionSource"/> class.
        /// </summary>
        /// <param name="defineAccess">Returns the definition accessor, read on every call.</param>
        /// <param name="loader">Returns the definition loader, or <c>null</c> to use the stored definitions.</param>
        public ApiFormDefinitionSource(Func<ClientDefineAccess> defineAccess, Func<FormDefinitionLoader?> loader)
        {
            ArgumentNullException.ThrowIfNull(defineAccess);
            ArgumentNullException.ThrowIfNull(loader);
            _defineAccess = defineAccess;
            _loader = loader;
        }

        /// <inheritdoc/>
        public Task<FormSchema> GetFormSchemaAsync(string progId, CancellationToken cancellationToken = default)
            => _loader() is { } loader
                ? loader.GetLocalizedSchemaAsync(progId, CultureInfo.CurrentUICulture.Name, cancellationToken)
                : _defineAccess().GetFormSchemaAsync(progId, cancellationToken);

        /// <inheritdoc/>
        public async Task<FormLayout> GetFormLayoutAsync(string progId, FormSchema schema, CancellationToken cancellationToken = default)
        {
            if (_loader() is { } loader)
            {
                return await loader.GetRuntimeLayoutAsync(progId, schema, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            // The definition cache hands back a shared instance, and a view degrades its layout in place, so the
            // stored path clones what it fetched. The loader assembles a new instance already.
            var definition = await _defineAccess().GetFormLayoutAsync(progId, cancellationToken).ConfigureAwait(false);
            return definition?.Clone()
                ?? throw new InvalidOperationException(
                    $"No FormLayout definition found for '{progId}'. Author one at design time and save it as "
                    + $"'FormLayout/{progId}.FormLayout.xml' under the definition path.");
        }

        /// <inheritdoc/>
        public async Task<CurrencySettings?> GetCurrencySettingsAsync(CancellationToken cancellationToken = default)
            => await _defineAccess().GetCurrencySettingsAsync(cancellationToken).ConfigureAwait(false);

        /// <inheritdoc/>
        public async Task<UnitSettings?> GetUnitSettingsAsync(CancellationToken cancellationToken = default)
            => await _defineAccess().GetUnitSettingsAsync(cancellationToken).ConfigureAwait(false);
    }
}
