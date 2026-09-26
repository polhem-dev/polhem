using System.ComponentModel;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// Single-flight behavior of the cache base classes under concurrent misses.
    /// </summary>
    /// <remarks>
    /// The assertions deliberately check for <b>the same instance</b> rather than "none is null". The consequence to
    /// prevent is two callers each getting their own object for the same key; for <c>SessionInfo</c>, an
    /// <c>EnterCompany</c> done through one copy is then invisible to the other. Asserting only non-null would also
    /// pass before the fix.
    /// </remarks>
    public class CacheSingleFlightTests
    {
        private const int Threads = 32;

        private sealed class Payload
        {
            public string Key { get; init; } = string.Empty;
        }

        /// <summary>Creation is deliberately slow so that the concurrent calls really overlap.</summary>
        private sealed class SlowObjectCache : ObjectCache<Payload>
        {
            private readonly string _key;
            private int _createCount;

            public SlowObjectCache(string key) { _key = key; }

            public int CreateCount => Volatile.Read(ref _createCount);

            protected override string GetKey() => _key;

            protected override Payload? CreateInstance()
            {
                Interlocked.Increment(ref _createCount);
                Thread.Sleep(50);
                return new Payload { Key = _key };
            }
        }

        private sealed class SlowKeyObjectCache : KeyObjectCache<Payload>
        {
            private readonly string _prefix;
            private readonly Func<string, Payload?> _factory;
            private int _createCount;

            public SlowKeyObjectCache(string prefix, Func<string, Payload?>? factory = null)
            {
                _prefix = prefix;
                _factory = factory ?? (k => new Payload { Key = k });
            }

            public int CreateCount => Volatile.Read(ref _createCount);

            protected override string GetCacheKey(string key)
                => ("CacheSingleFlightTests_" + _prefix + "_" + key).ToLowerInvariant();

            protected override Payload? CreateInstance(string key)
            {
                Interlocked.Increment(ref _createCount);
                Thread.Sleep(50);
                return _factory(key);
            }
        }

        /// <summary>
        /// Lets every thread enter <paramref name="body"/> at the same moment so the misses really overlap.
        /// </summary>
        private static TResult[] RunConcurrently<TResult>(int count, Func<int, TResult> body)
        {
            var results = new TResult[count];
            var barrier = new Barrier(count);
            var threads = new Thread[count];

            for (int i = 0; i < count; i++)
            {
                int index = i;
                threads[i] = new Thread(() =>
                {
                    barrier.SignalAndWait();
                    results[index] = body(index);
                });
                threads[i].Start();
            }
            foreach (var t in threads) { t.Join(); }
            return results;
        }

        [Fact]
        [DisplayName("Concurrent first reads of ObjectCache.Get create the instance once and every caller gets the same instance")]
        public void ObjectCache_ConcurrentMiss_CreatesOnce()
        {
            var cache = new SlowObjectCache("CacheSingleFlightTests_single_" + Guid.NewGuid().ToString("N"));

            var results = RunConcurrently(Threads, _ => cache.Get());

            Assert.Equal(1, cache.CreateCount);
            Assert.All(results, r => Assert.NotNull(r));
            Assert.All(results, r => Assert.Same(results[0], r));
        }

        [Fact]
        [DisplayName("Concurrent first reads of the same key in KeyObjectCache.Get create the instance once and return the same instance")]
        public void KeyObjectCache_ConcurrentMiss_SameKey_CreatesOnce()
        {
            var cache = new SlowKeyObjectCache(Guid.NewGuid().ToString("N"));

            var results = RunConcurrently(Threads, _ => cache.Get("token-a"));

            Assert.Equal(1, cache.CreateCount);
            Assert.All(results, r => Assert.NotNull(r));
            Assert.All(results, r => Assert.Same(results[0], r));
        }

        [Fact]
        [DisplayName("Different keys do not block each other in single-flight and each creates its own instance")]
        public void KeyObjectCache_DifferentKeys_EachCreatedIndependently()
        {
            var cache = new SlowKeyObjectCache(Guid.NewGuid().ToString("N"));

            // The same threads alternate between two keys. If single-flight worked per cache instead of per key,
            // one creation would be missing and both keys would get the same object.
            var results = RunConcurrently(Threads, i => cache.Get(i % 2 == 0 ? "key-x" : "key-y"));

            Assert.Equal(2, cache.CreateCount);
            Assert.Equal("key-x", results[0]!.Key);
            Assert.Equal("key-y", results[1]!.Key);
            Assert.All(results.Where((_, i) => i % 2 == 0), r => Assert.Same(results[0], r));
            Assert.All(results.Where((_, i) => i % 2 == 1), r => Assert.Same(results[1], r));
        }

        [Fact]
        [DisplayName("The negative cache still holds when CreateInstance returns null, so the second Get does not call CreateInstance")]
        public void KeyObjectCache_NegativeCache_StillShortCircuits()
        {
            var cache = new SlowKeyObjectCache(Guid.NewGuid().ToString("N"), _ => null);

            Assert.Null(cache.Get("missing"));
            Assert.Null(cache.Get("missing"));

            Assert.Equal(1, cache.CreateCount);
        }

        [Fact]
        [DisplayName("Single-flight clears its in-flight table after completion, so a later miss creates the instance again")]
        public void SingleFlight_DoesNotPinEntriesAfterCompletion()
        {
            var cache = new SlowKeyObjectCache(Guid.NewGuid().ToString("N"));

            var first = cache.Get("evictable");
            Assert.NotNull(first);
            cache.Remove("evictable");
            var second = cache.Get("evictable");

            // If the in-flight table were not cleared in `finally`, the second call would get the cached result
            // of the first `Lazy`.
            Assert.Equal(2, cache.CreateCount);
            Assert.NotSame(first, second);
        }
    }
}
