using System.Collections.Concurrent;

namespace Polhem.ObjectCaching
{
    /// <summary>
    /// A capped, per-cache record of keys whose lookup found nothing, used by
    /// <see cref="KeyObjectCache{T}"/> in place of miss markers in the shared cache provider when a
    /// subclass sets <see cref="KeyObjectCache{T}.MaxNegativeEntries"/>.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: the point is the cap. A cache whose keys come from the caller (an access token, an
    /// API key identifier) can be probed with endless distinct keys, and one marker per probe in an
    /// unbounded provider is memory the caller chooses the size of. When the set is full and a sweep
    /// of expired markers frees nothing, a new miss is simply not recorded: the next lookup of that key
    /// reads the data source again, which is exactly what it would cost with no negative caching.
    /// <para>
    /// A marker also lapses when the cache-notify version of its key moves, matching what the provider
    /// does for markers stored there, so an entry created on another node becomes visible immediately.
    /// </para>
    /// </remarks>
    internal sealed class BoundedMissMarkers
    {
        private readonly ConcurrentDictionary<string, Marker> _markers = new(StringComparer.Ordinal);
        private readonly int _capacity;
        private readonly TimeProvider _timeProvider;
        private long _nextSweepTicks;

        /// <summary>
        /// The shortest interval between two sweeps. A full set under a stream of new keys would
        /// otherwise scan every marker on every miss.
        /// </summary>
        private static readonly TimeSpan s_sweepInterval = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Initializes a new <see cref="BoundedMissMarkers"/>.
        /// </summary>
        /// <param name="capacity">The maximum number of markers held at once.</param>
        /// <param name="timeProvider">The clock used to expire markers.</param>
        public BoundedMissMarkers(int capacity, TimeProvider timeProvider)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
            ArgumentNullException.ThrowIfNull(timeProvider);
            _capacity = capacity;
            _timeProvider = timeProvider;
        }

        /// <summary>
        /// Gets the number of markers currently held, including any not yet swept after expiring.
        /// </summary>
        public int Count => _markers.Count;

        /// <summary>
        /// Determines whether a live marker exists for <paramref name="cacheKey"/>.
        /// </summary>
        /// <param name="cacheKey">The normalized cache key.</param>
        public bool Contains(string cacheKey)
        {
            if (!_markers.TryGetValue(cacheKey, out var marker)) { return false; }

            if (IsLive(marker, _timeProvider.GetUtcNow())) { return true; }

            _markers.TryRemove(new KeyValuePair<string, Marker>(cacheKey, marker));
            return false;
        }

        /// <summary>
        /// Records a miss for <paramref name="cacheKey"/>, unless the set is full of live markers.
        /// </summary>
        /// <param name="cacheKey">The normalized cache key.</param>
        /// <param name="expiresAt">When the marker lapses.</param>
        /// <param name="notifyKey">The cache-notify key whose version bump also clears the marker.</param>
        public void Add(string cacheKey, DateTimeOffset expiresAt, string? notifyKey)
        {
            var now = _timeProvider.GetUtcNow();
            if (expiresAt <= now) { return; }

            if (_markers.Count >= _capacity && !_markers.ContainsKey(cacheKey))
            {
                long due = Interlocked.Read(ref _nextSweepTicks);
                if (now.UtcTicks >= due
                    && Interlocked.CompareExchange(ref _nextSweepTicks, (now + s_sweepInterval).UtcTicks, due) == due)
                {
                    SweepExpired(now);
                }
                if (_markers.Count >= _capacity) { return; }
            }

            long version = notifyKey == null ? 0 : CacheInfo.NotifyVersions.GetVersion(notifyKey);
            _markers[cacheKey] = new Marker(expiresAt, notifyKey, version);
        }

        /// <summary>
        /// Removes the marker for <paramref name="cacheKey"/>, if any.
        /// </summary>
        /// <param name="cacheKey">The normalized cache key.</param>
        public void Remove(string cacheKey) => _markers.TryRemove(cacheKey, out _);

        private void SweepExpired(DateTimeOffset now)
        {
            foreach (var pair in _markers)
            {
                if (!IsLive(pair.Value, now))
                    _markers.TryRemove(pair);
            }
        }

        private static bool IsLive(Marker marker, DateTimeOffset now)
            => marker.ExpiresAt > now
               && (marker.NotifyKey == null
                   || CacheInfo.NotifyVersions.GetVersion(marker.NotifyKey) == marker.NotifyVersion);

        private readonly record struct Marker(DateTimeOffset ExpiresAt, string? NotifyKey, long NotifyVersion);
    }
}
