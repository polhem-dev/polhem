using Polhem.Core;

namespace Polhem.ObjectCaching
{
    /// <summary>
    /// Holds the single process-wide sentinel used by <see cref="KeyObjectCache{T}"/>
    /// to mark negative cache entries. Placed in a non-generic type so every closed
    /// type of <see cref="KeyObjectCache{T}"/> shares the same reference.
    /// </summary>
    internal static class KeyObjectCacheSentinel
    {
        /// <summary>
        /// Sentinel stored in the cache to mark keys whose
        /// <see cref="KeyObjectCache{T}.CreateInstance"/> returned <c>null</c>.
        /// Distinguished from cached values by reference equality.
        /// </summary>
        public static readonly object MissMarker = new();
    }

    /// <summary>
    /// Base class for caching same-type objects accessed by key.
    /// </summary>
    public abstract class KeyObjectCache<T> where T : class
    {
        private readonly string _cachePrefix;

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyObjectCache{T}"/> class.
        /// </summary>
        /// <param name="cachePrefix">
        /// Per-owner namespace prepended to <see cref="GetCacheKey"/>. Allows separate
        /// <see cref="CacheContainerService"/> instances (e.g. per-fixture test containers)
        /// to share the process-wide <see cref="CacheInfo.Provider"/> without colliding.
        /// Empty for the shared unprefixed namespace.
        /// </param>
        protected KeyObjectCache(string cachePrefix = "")
        {
            _cachePrefix = cachePrefix ?? string.Empty;
        }

        #endregion

        /// <summary>
        /// Gets the cache item expiration policy.
        /// </summary>
        /// <param name="key">The member key.</param>
        protected virtual CacheItemPolicy GetPolicy(string key)
        {
            // Default: sliding expiration of 20 minutes
            var policy = new CacheItemPolicy(CacheTimeKind.SlidingTime, 20);
            return policy;
        }

        /// <summary>
        /// Gets the cache item expiration policy applied when <see cref="CreateInstance"/>
        /// returns <c>null</c> (negative caching).
        /// </summary>
        /// <remarks>
        /// Returning a non-null policy stores a sentinel marker for the key, so subsequent
        /// <see cref="Get"/> calls within the policy's lifetime return <c>null</c> without
        /// invoking <see cref="CreateInstance"/> again. This guards against repeated lookups
        /// of keys whose underlying data does not exist (cache penetration).
        /// Return <c>null</c> to disable negative caching for this cache type.
        /// </remarks>
        /// <param name="key">The member key.</param>
        protected virtual CacheItemPolicy? GetNegativePolicy(string key)
        {
            // Default: 5-minute absolute expiration, shorter than the positive policy so
            // real data created externally becomes visible within a bounded delay.
            return new CacheItemPolicy(CacheTimeKind.AbsoluteTime, 5);
        }

        /// <summary>
        /// Gets the maximum number of miss markers this cache keeps, or zero to store them in the
        /// shared cache provider with no limit of their own.
        /// </summary>
        /// <remarks>
        /// Override with a positive value when the keys come from the caller rather than from the
        /// framework — an access token, an API key identifier — so a stream of distinct unknown keys
        /// cannot grow the cache without bound. Markers are then held per cache instance, capped at
        /// this many; once full, further misses go uncached until markers expire, which costs the
        /// data-source read that negative caching would have saved and nothing more. Only the
        /// absolute or sliding lifetime of <see cref="GetNegativePolicy"/> and its notify key apply to
        /// such markers.
        /// </remarks>
        protected virtual int MaxNegativeEntries => 0;

        private BoundedMissMarkers? _boundedMisses;

        /// <summary>
        /// Gets the capped miss-marker set, created on first use; <c>null</c> when
        /// <see cref="MaxNegativeEntries"/> is zero.
        /// </summary>
        private BoundedMissMarkers? BoundedMisses
        {
            get
            {
                int capacity = MaxNegativeEntries;
                if (capacity <= 0) { return null; }
                return LazyInitializer.EnsureInitialized(ref _boundedMisses,
                    () => new BoundedMissMarkers(capacity, TimeProvider.System));
            }
        }

        /// <summary>
        /// Gets the number of capped miss markers currently held. Exposed for tests.
        /// </summary>
        internal int BoundedNegativeCount => _boundedMisses?.Count ?? 0;

        /// <summary>
        /// Gets the cache key, normalized to lowercase to avoid case-sensitivity issues.
        /// </summary>
        /// <param name="key">The member key.</param>
        protected virtual string GetCacheKey(string key)
        {
            string suffix = (typeof(T).Name + "_" + key).ToLowerInvariant();
            return string.IsNullOrEmpty(_cachePrefix) ? suffix : _cachePrefix + "_" + suffix;
        }

        /// <summary>
        /// Creates an instance for the specified key.
        /// </summary>
        /// <param name="key">The member key.</param>
        protected virtual T? CreateInstance(string key)
        {
            return default;
        }

        /// <summary>
        /// Gets the object associated with the specified member key.
        /// </summary>
        /// <remarks>
        /// Concurrent misses on the same key produce one instance, not one per caller. That matters
        /// beyond the wasted work: <see cref="Polhem.Definition.Identity.SessionInfo"/> is cached here, and two callers holding
        /// different instances of the same session means a write through one — <c>EnterCompany</c>,
        /// for example — is invisible to the other.
        /// <para>
        /// A fill that overlaps an invalidation — <see cref="Remove"/>, a notify version bump or a
        /// rewrite of a watched file — does not keep what it loaded; see
        /// <see cref="CacheInvalidation"/> and <see cref="CacheItemPolicy"/>.
        /// </para>
        /// </remarks>
        /// <param name="key">The member key.</param>
        public virtual T? Get(string key)
        {
            string cacheKey = GetCacheKey(key);
            var misses = BoundedMisses;
            if (TryAnswerFromCache(cacheKey, misses, out var cached))
                return cached;

            // Another flight may have completed between the read above and the one inside.
            return CacheSingleFlight<T>.GetOrCreate(cacheKey, () =>
                TryAnswerFromCache(cacheKey, misses, out var fresh) ? fresh : LoadAndStore(key, cacheKey, misses));
        }

        /// <summary>
        /// Answers from what the cache already holds: a value, or a recorded miss of either kind.
        /// </summary>
        /// <returns><c>false</c> when the cache holds nothing for the key and the value must be loaded.</returns>
        private static bool TryAnswerFromCache(string cacheKey, BoundedMissMarkers? misses, out T? value)
        {
            value = null;
            if (misses != null && misses.Contains(cacheKey))
                return true;

            var cached = CacheInfo.Provider.Get(cacheKey);
            // Negative cache hit: answered without invoking CreateInstance.
            if (ReferenceEquals(cached, KeyObjectCacheSentinel.MissMarker))
                return true;

            value = cached as T;
            return value != null;
        }

        /// <summary>
        /// Loads the value for <paramref name="key"/> and stores it, or its absence, unless an
        /// invalidation lands while it loads.
        /// </summary>
        private T? LoadAndStore(string key, string cacheKey, BoundedMissMarkers? misses)
        {
            // Every invalidation source is read before the load, so one that lands during the
            // load is detected instead of being mistaken for the state the value was read at.
            long generation = CacheInvalidation.Read(cacheKey);
            var policy = BuildPolicy(key);
            policy.CaptureChangeBaseline();
            var negPolicy = BuildNegativePolicy(key);
            negPolicy?.CaptureChangeBaseline();

            var value = CreateInstance(key);
            if (value != null)
                CacheInvalidation.StoreIfCurrent(cacheKey, generation, value, policy);
            else if (negPolicy != null)
                StoreMiss(cacheKey, generation, negPolicy, misses);
            return value;
        }

        /// <summary>
        /// Records that <paramref name="cacheKey"/> has no value: as a marker in the cache provider, or
        /// in the capped miss set when this cache keeps one.
        /// </summary>
        private static void StoreMiss(string cacheKey, long generation, CacheItemPolicy negPolicy, BoundedMissMarkers? misses)
        {
            if (misses == null)
            {
                CacheInvalidation.StoreIfCurrent(cacheKey, generation, KeyObjectCacheSentinel.MissMarker, negPolicy);
                return;
            }

            if (CacheInvalidation.Read(cacheKey) != generation) { return; }
            misses.Add(cacheKey, ExpiryOf(negPolicy), negPolicy.ChangeNotifyKey, negPolicy.NotifyVersionBaseline);
            if (CacheInvalidation.Read(cacheKey) != generation)
                misses.Remove(cacheKey);
        }

        /// <summary>
        /// Stores the object in the cache under the specified key.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <param name="value">The object to store in the cache.</param>
        public virtual void Set(string key, T value)
        {
            string cacheKey = GetCacheKey(key);
            CacheInvalidation.Advance(cacheKey);
            _boundedMisses?.Remove(cacheKey);
            CacheInfo.Provider.Set(cacheKey, value, BuildPolicy(key));
        }

        /// <summary>
        /// Stores the object in the cache. The object must implement <see cref="IKeyObject"/> to provide the member key.
        /// </summary>
        /// <param name="value">The object to store in the cache.</param>
        public virtual void Set(T value)
        {
            if (value is IKeyObject c)
                Set(c.GetKey(), value);
            else
                throw new InvalidOperationException("The value does not implement the IKeyObject interface.");
        }

        /// <summary>
        /// Removes the entry with the specified key from the cache.
        /// </summary>
        /// <param name="key">The member key.</param>
        public virtual void Remove(string key)
        {
            string cacheKey = GetCacheKey(key);
            CacheInvalidation.Advance(cacheKey);
            _boundedMisses?.Remove(cacheKey);
            CacheInfo.Provider.Remove(cacheKey);
        }

        /// <summary>
        /// Gets the cache group forming the prefix of each entry's <c>"group:entity"</c> notify key;
        /// defaults to the cached type's name (e.g. <c>"CompanyInfo"</c>). Override only when the
        /// notification group differs.
        /// </summary>
        public virtual string CacheGroup => typeof(T).Name;

        /// <summary>
        /// Returns the policy from <see cref="GetPolicy(string)"/> with a default cache-notify
        /// dependency applied when the subclass did not set one.
        /// </summary>
        /// <remarks>
        /// The default is the <c>"group:entity"</c> convention writers use with
        /// <c>Touch</c>. A subclass whose storage reports an authoritative key
        /// (the define caches) has already set it, and that value is kept.
        /// </remarks>
        /// <param name="key">The member key.</param>
        private CacheItemPolicy BuildPolicy(string key)
        {
            var policy = GetPolicy(key);
            policy.ChangeNotifyKey ??= CacheGroup + ":" + key;
            return policy;
        }

        /// <summary>
        /// Converts a negative policy into the absolute time a capped marker lapses at.
        /// </summary>
        private static DateTimeOffset ExpiryOf(CacheItemPolicy policy)
        {
            if (policy.AbsoluteExpiration != DateTimeOffset.MaxValue)
                return policy.AbsoluteExpiration;
            return policy.SlidingExpiration > TimeSpan.Zero
                ? DateTimeOffset.UtcNow.Add(policy.SlidingExpiration)
                : DateTimeOffset.MaxValue;
        }

        /// <summary>
        /// Returns the negative (miss-marker) policy with the same default notify dependency, so a
        /// cached miss also clears once the entry is created elsewhere.
        /// </summary>
        /// <param name="key">The member key.</param>
        private CacheItemPolicy? BuildNegativePolicy(string key)
        {
            var policy = GetNegativePolicy(key);
            if (policy != null)
                policy.ChangeNotifyKey ??= CacheGroup + ":" + key;
            return policy;
        }
    }
}
