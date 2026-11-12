using Polhem.Core;
using Polhem.Definition;
using Polhem.Definition.Language;
using Polhem.Definition.Storage;

namespace Polhem.ObjectCaching.Define
{
    /// <summary>
    /// Language resource cache, keyed by <c>"{lang}.{namespace}"</c> composite.
    /// One cache entry per (language, namespace) pair — invalidating a single namespace
    /// in one language does not affect the others.
    /// </summary>
    public sealed class LanguageResourceCache : KeyObjectCache<LanguageResource>
    {
        private readonly IDefineStorage _storage;

        /// <summary>
        /// Upper bound on the number of missing language resources remembered at once.
        /// </summary>
        /// <remarks>
        /// The language and namespace of a lookup come from the caller (<c>System.GetLanguage</c> takes them from any
        /// signed-in user), so the miss markers are capped rather than kept in the shared cache provider. The resources
        /// an application really looks up and lacks are a handful per language; past the cap a miss goes uncached and
        /// costs one storage read.
        /// </remarks>
        internal static readonly int MaxNegativeLanguages = 10_000;

        /// <summary>
        /// Initializes a new instance of <see cref="LanguageResourceCache"/>.
        /// </summary>
        /// <param name="storage">The define storage backing this cache.</param>
        /// <param name="cachePrefix">Per-owner cache namespace (see <see cref="KeyObjectCache{T}"/>).</param>
        public LanguageResourceCache(IDefineStorage storage, string cachePrefix = "") : base(cachePrefix)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <inheritdoc/>
        protected override int MaxNegativeEntries => MaxNegativeLanguages;

        /// <summary>
        /// Gets the cache item expiration policy.
        /// </summary>
        /// <param name="key">The member key in the format <c>"{lang}.{namespace}"</c>.</param>
        protected override CacheItemPolicy GetPolicy(string key)
        {
            // Parse the member key to extract the language code and namespace.
            key.SplitLeft(".", out string lang, out string ns);

            // Default: sliding expiration of 20 minutes (matches other Define caches).
            var policy = new CacheItemPolicy(CacheTimeKind.SlidingTime, 20);
            var changeSource = _storage.GetChangeSource(DefineType.Language, lang, ns);
            policy.ChangeMonitorFilePaths = changeSource.FilePaths;
            policy.ChangeNotifyKey = changeSource.NotifyKey;
            return policy;
        }

        /// <summary>
        /// Creates an instance of the language resource.
        /// </summary>
        /// <param name="key">The member key in the format <c>"{lang}.{namespace}"</c>.</param>
        protected override LanguageResource? CreateInstance(string key)
        {
            key.SplitLeft(".", out string lang, out string ns);
            return _storage.GetLanguage(lang, ns);
        }

        /// <summary>
        /// Gets the language resource for the specified language and namespace.
        /// </summary>
        /// <param name="lang">The BCP-47 language code.</param>
        /// <param name="ns">The resource namespace.</param>
        public LanguageResource? Get(string lang, string ns)
        {
            string key = $"{lang}.{ns}";
            return base.Get(key);
        }

        /// <summary>
        /// Removes the language resource entry from the cache.
        /// </summary>
        /// <param name="lang">The BCP-47 language code.</param>
        /// <param name="ns">The resource namespace.</param>
        public void Remove(string lang, string ns)
        {
            string key = $"{lang}.{ns}";
            base.Remove(key);
        }
    }
}
