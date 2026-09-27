using Polhem.Base;
using Polhem.Definition.Settings;
using Polhem.ObjectCaching.Providers;

namespace Polhem.ObjectCaching
{
    /// <summary>
    /// Provides a static interface for accessing the cache provider.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="MemoryCacheProvider"/>. Host startup may call
    /// <see cref="Initialize"/> with the backend configuration to switch to a
    /// user-specified provider.
    /// </remarks>
    public static class CacheInfo
    {
        /// <summary>
        /// Gets or sets the cache provider instance.
        /// </summary>
        /// <value>
        /// Defaults to <see cref="MemoryCacheProvider"/>; can be overridden via
        /// <see cref="Initialize"/> based on the backend configuration.
        /// </value>
        public static ICacheProvider Provider { get; set; } = new MemoryCacheProvider();

        /// <summary>
        /// Gets or sets the store of observed cache-notify versions.
        /// </summary>
        /// <value>
        /// Defaults to an in-memory <see cref="CacheNotifyVersionStore"/>. The cache-notify poller
        /// writes into it; entries carrying a <see cref="CacheItemPolicy.ChangeNotifyKey"/> read from
        /// it to detect out-of-process changes. Settable so tests can isolate their own instance.
        /// </value>
        public static ICacheNotifyVersionStore NotifyVersions { get; set; } = new CacheNotifyVersionStore();

        /// <summary>
        /// Initializes the cache provider from the backend configuration.
        /// Called directly by <c>AddPolhemFramework</c> after settings are loaded.
        /// </summary>
        /// <remarks>
        /// Only replaces <see cref="Provider"/> when the configured type differs from the
        /// current provider's runtime type. This preserves cached entries when host startup
        /// invokes <see cref="Initialize"/> after the default provider has already received
        /// data (e.g. test fixtures that pre-populate <see cref="DatabaseSettings.Items"/>).
        /// </remarks>
        /// <param name="configuration">The backend configuration.</param>
        /// <exception cref="InvalidOperationException">
        /// <see cref="BackendComponents.CacheProvider"/> names a type that cannot be loaded or that
        /// does not implement <see cref="ICacheProvider"/>. <see cref="Provider"/> is left unchanged.
        /// </exception>
        public static void Initialize(BackendConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            var configured = configuration.Components.CacheProvider;
            if (string.IsNullOrWhiteSpace(configured)) return;

            var newType = Type.GetType(configured);
            if (newType != null && newType == Provider.GetType()) return;

            // A wrong name used to leave `Provider` null, and the failure surfaced as a
            // NullReferenceException on the first cache access of some later request. It is
            // reported here instead, at startup, naming the setting to fix.
            object? created;
            try
            {
                created = AssemblyLoader.CreateInstance(configured);
            }
            catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                throw new InvalidOperationException(
                    BeeNameHint.AppendTo($"BackendComponents.CacheProvider names '{configured}', whose assembly could not be loaded.", configured), ex);
            }

            Provider = created switch
            {
                ICacheProvider provider => provider,
                null => throw new InvalidOperationException(
                    BeeNameHint.AppendTo($"BackendComponents.CacheProvider names '{configured}', which was not found in its assembly.", configured)),
                _ => throw new InvalidOperationException(
                    $"BackendComponents.CacheProvider names '{configured}', which does not implement {nameof(ICacheProvider)}."),
            };
        }

    }
}
