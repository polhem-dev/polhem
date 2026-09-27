using System.ComponentModel;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// A cache fill overlapped by an invalidation must not keep what it loaded. Each test injects the
    /// invalidation from inside <c>CreateInstance</c>, which is exactly the window of the race: the old
    /// data has been read, the value has not been stored yet.
    /// </summary>
    public class CacheFillInvalidationRaceTests
    {
        private sealed class Payload
        {
            public string Value { get; init; } = string.Empty;
        }

        /// <summary>
        /// A keyed cache whose first load runs a caller-supplied invalidation before returning the old value.
        /// </summary>
        private sealed class RacingKeyCache : KeyObjectCache<Payload>
        {
            private readonly string _prefix = Guid.NewGuid().ToString("N");
            private readonly Action<RacingKeyCache, string> _duringFirstLoad;
            private readonly bool _firstLoadMisses;

            public RacingKeyCache(Action<RacingKeyCache, string> duringFirstLoad, bool firstLoadMisses = false)
            {
                _duringFirstLoad = duringFirstLoad;
                _firstLoadMisses = firstLoadMisses;
            }

            public int Loads { get; private set; }

            public string? NotifyKey { get; init; }

            public string? WatchedFile { get; init; }

            protected override string GetCacheKey(string key) => ("race_" + _prefix + "_" + key).ToLowerInvariant();

            protected override CacheItemPolicy GetPolicy(string key)
            {
                var policy = new CacheItemPolicy(CacheTimeKind.SlidingTime, 20) { ChangeNotifyKey = NotifyKey };
                if (WatchedFile != null)
                    policy.ChangeMonitorFilePaths = [WatchedFile];
                return policy;
            }

            protected override Payload? CreateInstance(string key)
            {
                Loads++;
                if (Loads > 1)
                    return new Payload { Value = "new" };

                _duringFirstLoad(this, key);
                return _firstLoadMisses ? null : new Payload { Value = "old" };
            }
        }

        private sealed class RacingSingleCache : ObjectCache<Payload>
        {
            private readonly string _prefix = Guid.NewGuid().ToString("N");

            public int Loads { get; private set; }

            protected override string GetKey() => "race_single_" + _prefix;

            protected override Payload? CreateInstance()
            {
                Loads++;
                if (Loads > 1)
                    return new Payload { Value = "new" };

                Remove();
                return new Payload { Value = "old" };
            }
        }

        [Fact]
        [DisplayName("A keyed fill overlapped by Remove is not kept, so the next read loads the new value")]
        public void KeyedGet_RemoveDuringLoad_DoesNotPinOldValue()
        {
            var cache = new RacingKeyCache((c, key) => c.Remove(key));

            var first = cache.Get("k");
            var second = cache.Get("k");

            Assert.Equal("old", first!.Value);
            Assert.Equal("new", second!.Value);
            Assert.Equal(2, cache.Loads);
        }

        [Fact]
        [DisplayName("A single-object fill overlapped by Remove is not kept, so the next read loads the new value")]
        public void SingleGet_RemoveDuringLoad_DoesNotPinOldValue()
        {
            var cache = new RacingSingleCache();

            _ = cache.Get();
            var second = cache.Get();

            Assert.Equal("new", second!.Value);
            Assert.Equal(2, cache.Loads);
        }

        [Fact]
        [DisplayName("A fill overlapped by a cache-notify version bump is evicted on the next read")]
        public void KeyedGet_NotifyBumpDuringLoad_DoesNotPinOldValue()
        {
            string notifyKey = $"RaceGroup:{Guid.NewGuid():N}";
            var cache = new RacingKeyCache((_, _) => CacheInfo.NotifyVersions.SetVersion(notifyKey, 7))
            {
                NotifyKey = notifyKey
            };

            _ = cache.Get("k");
            var second = cache.Get("k");

            Assert.Equal("new", second!.Value);
            Assert.Equal(2, cache.Loads);
        }

        [Fact]
        [DisplayName("A fill overlapped by a rewrite of its watched file is evicted on the next read")]
        public void KeyedGet_FileRewrittenDuringLoad_DoesNotPinOldValue()
        {
            string path = Path.Combine(Path.GetTempPath(), $"polhem-race-{Guid.NewGuid():N}.xml");
            File.WriteAllText(path, "old");
            File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            try
            {
                var cache = new RacingKeyCache((_, _) =>
                    File.SetLastWriteTimeUtc(path, new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc)))
                {
                    WatchedFile = path
                };

                _ = cache.Get("k");
                var second = cache.Get("k");

                Assert.Equal("new", second!.Value);
                Assert.Equal(2, cache.Loads);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        [DisplayName("A miss overlapped by Remove is not recorded as a miss marker, so the next read loads again")]
        public void KeyedGet_RemoveDuringMissLookup_DoesNotCacheTheMiss()
        {
            var cache = new RacingKeyCache((c, key) => c.Remove(key), firstLoadMisses: true);

            var first = cache.Get("k");
            var second = cache.Get("k");

            Assert.Null(first);
            Assert.Equal("new", second!.Value);
            Assert.Equal(2, cache.Loads);
        }

        [Fact]
        [DisplayName("A fill that no invalidation overlaps is still cached and served without reloading")]
        public void KeyedGet_NoInvalidation_CachesValue()
        {
            var cache = new RacingKeyCache((_, _) => { });

            _ = cache.Get("k");
            var second = cache.Get("k");

            Assert.Equal("old", second!.Value);
            Assert.Equal(1, cache.Loads);
        }
    }
}
