using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace Polhem.ObjectCaching.Providers
{
    /// <summary>
    /// Cache provider implementation backed by <see cref="IMemoryCache"/>.
    /// </summary>
    public sealed class MemoryCacheProvider : ICacheProvider, IDisposable
    {
        private readonly MemoryCache _memoryCache;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryCacheProvider"/> class
        /// using a dedicated <see cref="MemoryCache"/> with default options.
        /// </summary>
        public MemoryCacheProvider()
            : this(new MemoryCache(new MemoryCacheOptions()))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryCacheProvider"/> class with the specified <see cref="MemoryCache"/>.
        /// </summary>
        /// <param name="memoryCache">The memory cache instance to use.</param>
        public MemoryCacheProvider(MemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        /// <summary>
        /// Normalizes the cache key for case-insensitive comparison.
        /// </summary>
        /// <param name="key">The original key.</param>
        private static string GetCacheKey(string key)
        {
            return key.ToLowerInvariant();
        }

        /// <summary>
        /// Determines whether a cache entry with the specified key exists in the cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        public bool Contains(string key)
        {
            return _memoryCache.TryGetValue(GetCacheKey(key), out _);
        }

        /// <summary>
        /// Inserts a cache entry into the cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The object to insert into the cache.</param>
        /// <param name="policy">The expiration policy for the cache entry.</param>
        public void Set(string key, object value, CacheItemPolicy policy)
        {
            var cacheKey = GetCacheKey(key);
            var options = CreateEntryOptions(policy);
            _memoryCache.Set(cacheKey, value, options);
        }

        /// <summary>
        /// Returns the cache entry for the specified key, or <c>null</c> if the key is not present.
        /// </summary>
        /// <param name="key">The cache key.</param>
        public object? Get(string key)
        {
            return _memoryCache.Get(GetCacheKey(key));
        }

        /// <summary>
        /// Removes the cache entry with the specified key.
        /// </summary>
        /// <param name="key">The cache key.</param>
        public void Remove(string key)
        {
            _memoryCache.Remove(GetCacheKey(key));
        }

        /// <summary>
        /// Returns the total number of cache entries in the cache.
        /// </summary>
        public long GetCount()
        {
            return _memoryCache.Count;
        }

        /// <summary>
        /// Maps a Polhem <see cref="CacheItemPolicy"/> to a <see cref="MemoryCacheEntryOptions"/>,
        /// translating absolute / sliding expirations and file watch tokens.
        /// </summary>
        private static MemoryCacheEntryOptions CreateEntryOptions(CacheItemPolicy policy)
        {
            var options = new MemoryCacheEntryOptions();
            if (policy.AbsoluteExpiration != DateTimeOffset.MaxValue)
                options.AbsoluteExpiration = policy.AbsoluteExpiration;
            if (policy.SlidingExpiration != TimeSpan.Zero)
                options.SlidingExpiration = policy.SlidingExpiration;

            if (policy.ChangeMonitorFilePaths != null)
            {
                foreach (var path in policy.ChangeMonitorFilePaths)
                {
                    if (string.IsNullOrEmpty(path))
                        continue;
                    DateTime? baseline = policy.FileWriteTimeBaselines != null
                        && policy.FileWriteTimeBaselines.TryGetValue(path, out var written) ? written : null;
                    options.AddExpirationToken(new FileModificationToken(path, baseline));
                }
            }

            if (!string.IsNullOrEmpty(policy.ChangeNotifyKey))
                options.AddExpirationToken(new CacheNotifyToken(policy.ChangeNotifyKey, policy.NotifyVersionBaseline));

            return options;
        }

        /// <summary>
        /// Releases the underlying <see cref="MemoryCache"/>.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _memoryCache.Dispose();
            _disposed = true;
        }

        /// <summary>
        /// Lazy file-modification change token: compares current LastWriteTimeUtc against the
        /// policy's pre-load baseline, or a snapshot taken at construction when the policy has none.
        /// No background timer avoids the race condition
        /// where an immediately-firing polling timer evicts entries before they can be read.
        /// </summary>
        /// <remarks>
        /// <see cref="MemoryCache"/> reads <see cref="HasChanged"/> on every lookup, and a file-backed
        /// definition is looked up several times per request. Stat'ing the file each time cost about a
        /// microsecond on a local SSD and far more on a network share, so the file is re-read at most
        /// once per <see cref="FileWriteTime.RecheckInterval"/>. The first check is not deferred (the
        /// next-check time starts at zero), which is what catches a rewrite that landed while the value
        /// was being loaded; <c>CacheFillInvalidationRaceTests</c> pins that case.
        /// </remarks>
        private sealed class FileModificationToken : IChangeToken
        {
            private readonly string _filePath;
            private readonly DateTime _initialWriteTime;
            private volatile bool _hasChanged;
            private long _nextCheckAt;

            public FileModificationToken(string filePath, DateTime? baseline)
            {
                _filePath = filePath;
                _initialWriteTime = baseline ?? FileWriteTime.Get(filePath);
            }

            public bool HasChanged
            {
                get
                {
                    if (_hasChanged) return true;

                    long now = Environment.TickCount64;
                    if (now < Interlocked.Read(ref _nextCheckAt)) return false;
                    Interlocked.Exchange(ref _nextCheckAt, now + (long)FileWriteTime.RecheckInterval.TotalMilliseconds);

                    _hasChanged = FileWriteTime.Get(_filePath) != _initialWriteTime;
                    return _hasChanged;
                }
            }

            public bool ActiveChangeCallbacks => false;

            public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
                => CancellationToken.None.Register(callback!, state);
        }

        /// <summary>
        /// Lazy cache-notify change token: compares the current observed version for a notify key
        /// against the policy's pre-load baseline, or a snapshot taken at construction when it has none. Deliberately mirrors
        /// <see cref="FileModificationToken"/> — no background timer, detection happens on read —
        /// so notification-backed entries behave exactly like file-backed ones.
        /// </summary>
        private sealed class CacheNotifyToken : IChangeToken
        {
            private readonly string _notifyKey;
            private readonly long _initialVersion;
            private volatile bool _hasChanged;

            public CacheNotifyToken(string notifyKey, long? baseline)
            {
                _notifyKey = notifyKey;
                _initialVersion = baseline ?? CacheInfo.NotifyVersions.GetVersion(notifyKey);
            }

            public bool HasChanged
            {
                get
                {
                    if (_hasChanged) return true;
                    _hasChanged = CacheInfo.NotifyVersions.GetVersion(_notifyKey) != _initialVersion;
                    return _hasChanged;
                }
            }

            public bool ActiveChangeCallbacks => false;

            public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
                => CancellationToken.None.Register(callback!, state);
        }
    }
}
