using Polhem.Definition;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.ObjectCaching.Define
{
    /// <summary>
    /// Program settings cache.
    /// </summary>
    public sealed class ProgramSettingsCache : ObjectCache<ProgramSettings>
    {
        private readonly IDefineStorage _storage;

        /// <summary>
        /// Initializes a new <see cref="ProgramSettingsCache"/>.
        /// </summary>
        /// <param name="storage">The define storage backing this cache.</param>
        /// <param name="cachePrefix">Per-owner cache namespace (see <see cref="ObjectCache{T}"/>).</param>
        public ProgramSettingsCache(IDefineStorage storage, string cachePrefix = "") : base(cachePrefix)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>
        /// Gets the cache item expiration policy.
        /// </summary>
        protected override CacheItemPolicy GetPolicy()
        {
            var policy = new CacheItemPolicy(CacheTimeKind.SlidingTime, 20);
            // The storage decides what to watch: file storage returns its backing file, the DB storage
            // returns nothing and invalidates through the cache-notify table instead.
            var changeSource = _storage.GetChangeSource(DefineType.ProgramSettings);
            policy.ChangeMonitorFilePaths = changeSource.FilePaths;
            policy.ChangeNotifyKey = changeSource.NotifyKey;
            return policy;
        }

        /// <summary>
        /// Creates an instance of the program settings.
        /// </summary>
        protected override ProgramSettings? CreateInstance()
        {
            return _storage.GetProgramSettings();
        }
    }
}
