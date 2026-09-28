using Polhem.Definition.Settings;

namespace Polhem.Definition
{
    /// <summary>
    /// Provides access to the current <see cref="DatabaseSettings"/> snapshot
    /// (with <see cref="DatabaseSettings.Items"/> populated). Resolved through
    /// DI ctor injection.
    /// </summary>
    public interface IDatabaseSettingsProvider
    {
        /// <summary>Returns the current database settings snapshot.</summary>
        DatabaseSettings Get();

        /// <summary>Looks up the database item for the given identifier.</summary>
        /// <exception cref="ArgumentNullException">When <paramref name="databaseId"/> is null or whitespace.</exception>
        /// <exception cref="KeyNotFoundException">When no matching item exists.</exception>
        DatabaseItem GetItem(string databaseId);

        /// <summary>
        /// Validates that the settings contain the framework-required
        /// <c>common</c> database item; throws if missing.
        /// </summary>
        /// <remarks>
        /// A host built with <c>AddPolhemFramework</c> calls this at startup and does not start when it throws.
        /// </remarks>
        /// <exception cref="InvalidOperationException">When the required item is missing.</exception>
        void ValidateRequired();
    }
}
