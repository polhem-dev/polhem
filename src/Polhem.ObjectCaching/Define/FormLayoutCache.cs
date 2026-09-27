using Polhem.Definition;
using Polhem.Definition.Layouts;
using Polhem.Definition.Storage;

namespace Polhem.ObjectCaching.Define
{
    /// <summary>
    /// Form layout cache.
    /// </summary>
    public class FormLayoutCache : KeyObjectCache<FormLayout>
    {
        private readonly IDefineStorage _storage;

        /// <summary>
        /// Initializes a new instance of <see cref="FormLayoutCache"/>.
        /// </summary>
        /// <param name="storage">The define storage backing this cache.</param>
        /// <param name="cachePrefix">Per-owner cache namespace (see <see cref="KeyObjectCache{T}"/>).</param>
        public FormLayoutCache(IDefineStorage storage, string cachePrefix = "") : base(cachePrefix)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>
        /// Gets the cache item expiration policy.
        /// </summary>
        /// <param name="key">The member key.</param>
        protected override CacheItemPolicy GetPolicy(string key)
        {
            // Layout identifier
            string layoutId = key;
            // Default: sliding expiration of 20 minutes
            var policy = new CacheItemPolicy(CacheTimeKind.SlidingTime, 20);
            var changeSource = _storage.GetChangeSource(DefineType.FormLayout, layoutId);
            policy.ChangeMonitorFilePaths = changeSource.FilePaths;
            policy.ChangeNotifyKey = changeSource.NotifyKey;
            return policy;
        }

        /// <summary>
        /// Creates an instance of the form layout.
        /// </summary>
        /// <param name="key">The member key, which is the layout identifier.</param>
        protected override FormLayout? CreateInstance(string key)
        {
            // Layout identifier
            string layoutId = key;
            return _storage.GetFormLayout(layoutId);
        }
    }
}
