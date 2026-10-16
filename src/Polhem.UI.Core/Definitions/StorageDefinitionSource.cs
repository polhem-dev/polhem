using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.UI.Core.Definitions
{
    /// <summary>
    /// An <see cref="IFormDefinitionSource"/> over an <see cref="IDefineStorage"/>, for a front end without a
    /// Polhem backend; with <see cref="FileDefineStorage"/> it reads the definition files of a local folder.
    /// </summary>
    /// <remarks>
    /// Returns the definitions as stored: it applies no language resources and no tenant customization, which a
    /// backend's definition source assembles.
    /// </remarks>
    public sealed class StorageDefinitionSource : IFormDefinitionSource
    {
        private readonly IDefineStorage _storage;

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageDefinitionSource"/> class.
        /// </summary>
        /// <param name="storage">The storage to read the definitions from.</param>
        public StorageDefinitionSource(IDefineStorage storage)
        {
            ArgumentNullException.ThrowIfNull(storage);
            _storage = storage;
        }

        /// <inheritdoc/>
        public Task<FormSchema> GetFormSchemaAsync(string progId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_storage.GetFormSchema(progId)
                ?? throw new InvalidOperationException($"No FormSchema definition found for '{progId}'."));
        }

        /// <inheritdoc/>
        public Task<FormLayout> GetFormLayoutAsync(string progId, FormSchema schema, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // A storage may hand back a cached instance, and the caller degrades its layout in place.
            var layout = _storage.GetFormLayout(progId)?.Clone()
                ?? throw new InvalidOperationException(
                    $"No FormLayout definition found for '{progId}'. Author one at design time and save it as "
                    + $"'FormLayout/{progId}.FormLayout.xml' under the definition path.");
            return Task.FromResult(layout);
        }

        /// <inheritdoc/>
        public Task<CurrencySettings?> GetCurrencySettingsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_storage.GetCurrencySettings());
        }

        /// <inheritdoc/>
        public Task<UnitSettings?> GetUnitSettingsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_storage.GetUnitSettings());
        }
    }
}
