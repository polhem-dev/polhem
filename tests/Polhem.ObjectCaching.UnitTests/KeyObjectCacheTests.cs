using System.ComponentModel;
using Polhem.Base;

namespace Polhem.ObjectCaching.UnitTests
{
    public class KeyObjectCacheTests
    {
        // Implements `IKeyObject` so that both `Set(value)` and `Set(key, value)` can be tested.
        private sealed class KeyedPayload : IKeyObject
        {
            public string Id { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;

            public string GetKey() => Id;
        }

        private sealed class StubKeyObjectCache : KeyObjectCache<KeyedPayload>
        {
            private readonly string _prefix;
            private readonly Func<string, KeyedPayload?> _factory;
            private readonly bool _disableNegativeCache;

            public StubKeyObjectCache(
                string prefix,
                Func<string, KeyedPayload?> factory,
                bool disableNegativeCache = false)
            {
                _prefix = prefix;
                _factory = factory;
                _disableNegativeCache = disableNegativeCache;
            }

            public int CreateInstanceCallCount { get; private set; }

            protected override string GetCacheKey(string key)
                => ("KeyObjectCacheTests_" + _prefix + "_" + key).ToLowerInvariant();

            protected override KeyedPayload? CreateInstance(string key)
            {
                CreateInstanceCallCount++;
                return _factory(key);
            }

            protected override CacheItemPolicy? GetNegativePolicy(string key)
                => _disableNegativeCache ? null : base.GetNegativePolicy(key);
        }

        // Does not implement `IKeyObject`, to test the exception path of `Set(value)`.
        private sealed class PlainCache : KeyObjectCache<string>
        {
            protected override string GetCacheKey(string key)
                => "KeyObjectCacheTests_plain_" + key;
        }

        [Fact]
        [DisplayName("Get calls CreateInstance the first time and serves the second call from the cache")]
        public void Get_CachesAfterFirstCall()
        {
            var prefix = Guid.NewGuid().ToString("N");
            var cache = new StubKeyObjectCache(prefix, key => new KeyedPayload { Id = key, Value = key + "_v" });

            var first = cache.Get("alpha");
            var second = cache.Get("alpha");

            Assert.NotNull(first);
            Assert.Same(first, second);
            Assert.Equal(1, cache.CreateInstanceCallCount);

            cache.Remove("alpha");
        }

        [Fact]
        [DisplayName("When CreateInstance returns null, the second Get hits the negative cache and does not call CreateInstance")]
        public void Get_CreateInstanceReturnsNull_CachesNegativeMarker()
        {
            var prefix = Guid.NewGuid().ToString("N");
            var cache = new StubKeyObjectCache(prefix, _ => null);

            Assert.Null(cache.Get("missing"));
            Assert.Null(cache.Get("missing"));
            Assert.Equal(1, cache.CreateInstanceCallCount);

            cache.Remove("missing");
        }

        [Fact]
        [DisplayName("When GetNegativePolicy returns null the negative cache is disabled and every Get calls CreateInstance again")]
        public void Get_GetNegativePolicyReturnsNull_DoesNotCacheMiss()
        {
            var prefix = Guid.NewGuid().ToString("N");
            var cache = new StubKeyObjectCache(prefix, _ => null, disableNegativeCache: true);

            Assert.Null(cache.Get("missing"));
            Assert.Null(cache.Get("missing"));
            Assert.Equal(2, cache.CreateInstanceCallCount);
        }

        [Fact]
        [DisplayName("Set overwrites the negative marker and a later Get returns the real object")]
        public void Set_OverwritesNegativeMarker()
        {
            var prefix = Guid.NewGuid().ToString("N");
            var cache = new StubKeyObjectCache(prefix, _ => null);
            var payload = new KeyedPayload { Id = "key", Value = "v" };

            Assert.Null(cache.Get("key"));
            Assert.Equal(1, cache.CreateInstanceCallCount);

            cache.Set("key", payload);
            Assert.Same(payload, cache.Get("key"));
            Assert.Equal(1, cache.CreateInstanceCallCount);

            cache.Remove("key");
        }

        [Fact]
        [DisplayName("Remove clears the negative marker and the next Get calls CreateInstance again")]
        public void Remove_ClearsNegativeMarker()
        {
            var prefix = Guid.NewGuid().ToString("N");
            var cache = new StubKeyObjectCache(prefix, _ => null);

            Assert.Null(cache.Get("key"));
            Assert.Equal(1, cache.CreateInstanceCallCount);

            cache.Remove("key");

            Assert.Null(cache.Get("key"));
            Assert.Equal(2, cache.CreateInstanceCallCount);
        }

        [Fact]
        [DisplayName("Set(value) writes the entry under the key from IKeyObject.GetKey")]
        public void Set_WithIKeyObject_UsesGetKey()
        {
            var prefix = Guid.NewGuid().ToString("N");
            var cache = new StubKeyObjectCache(prefix, _ => null);
            var payload = new KeyedPayload { Id = "beta", Value = "B" };

            cache.Set(payload);

            Assert.Same(payload, cache.Get("beta"));
            cache.Remove("beta");
        }

        [Fact]
        [DisplayName("Set(string, value) and Remove(string) work correctly")]
        public void Set_WithExplicitKey_AndRemove_Works()
        {
            var prefix = Guid.NewGuid().ToString("N");
            var cache = new StubKeyObjectCache(prefix, _ => null);
            var payload = new KeyedPayload { Id = "ignored-by-test", Value = "v" };

            cache.Set("manual", payload);
            Assert.Same(payload, cache.Get("manual"));

            cache.Remove("manual");
            Assert.Null(cache.Get("manual"));
        }

        [Fact]
        [DisplayName("Set(value) throws for an object that does not implement IKeyObject")]
        public void Set_WithoutIKeyObject_Throws()
        {
            var cache = new PlainCache();
            Assert.Throws<InvalidOperationException>(() => cache.Set("non-key-object"));
        }
    }
}
