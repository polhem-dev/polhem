using System.ComponentModel;

namespace Polhem.ObjectCaching.UnitTests
{
    public class ObjectCacheTests
    {
        private sealed class TestPayload
        {
            public string Value { get; set; } = string.Empty;
        }

        // The key suffix comes from the constructor so that every test uses its own cache key.
        private sealed class StubObjectCache : ObjectCache<TestPayload>
        {
            private readonly string _suffix;
            private readonly Func<TestPayload?> _factory;

            public StubObjectCache(string suffix, Func<TestPayload?> factory)
            {
                _suffix = suffix;
                _factory = factory;
            }

            public int CreateInstanceCallCount { get; private set; }

            protected override string GetKey() => "ObjectCacheTests_" + _suffix;

            protected override CacheItemPolicy GetPolicy()
                => new CacheItemPolicy(CacheTimeKind.SlidingTime, 1);

            protected override TestPayload? CreateInstance()
            {
                CreateInstanceCallCount++;
                return _factory();
            }
        }

        [Fact]
        [DisplayName("The first Get calls CreateInstance and later calls are served from the cache")]
        public void Get_FirstCall_CreatesAndCaches_SubsequentCallsHit()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var cache = new StubObjectCache(suffix, () => new TestPayload { Value = "hello" });

            var first = cache.Get();
            var second = cache.Get();

            Assert.NotNull(first);
            Assert.Equal("hello", first!.Value);
            Assert.Same(first, second);
            Assert.Equal(1, cache.CreateInstanceCallCount);

            cache.Remove();
        }

        [Fact]
        [DisplayName("When CreateInstance returns null nothing is cached and every Get calls it again")]
        public void Get_CreateInstanceReturnsNull_DoesNotCache()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var cache = new StubObjectCache(suffix, () => null);

            Assert.Null(cache.Get());
            Assert.Null(cache.Get());
            Assert.Equal(2, cache.CreateInstanceCallCount);
        }

        [Fact]
        [DisplayName("Get returns the object after Set and rebuilds it after Remove")]
        public void Set_ThenRemove_BehavesCorrectly()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var stub = new TestPayload { Value = "manual" };
            var cache = new StubObjectCache(suffix, () => new TestPayload { Value = "factory" });

            cache.Set(stub);
            Assert.Same(stub, cache.Get());
            Assert.Equal(0, cache.CreateInstanceCallCount);

            cache.Remove();
            var rebuilt = cache.Get();
            Assert.NotNull(rebuilt);
            Assert.Equal("factory", rebuilt!.Value);
            Assert.Equal(1, cache.CreateInstanceCallCount);

            cache.Remove();
        }

        // Does not override `GetPolicy`, so the base class default runs.
        private sealed class DefaultPolicyCache : ObjectCache<TestPayload>
        {
            private readonly string _suffix;
            private readonly TestPayload? _payload;

            public DefaultPolicyCache(string suffix, TestPayload? payload)
            {
                _suffix = suffix;
                _payload = payload;
            }

            protected override string GetKey() => "ObjectCacheTests_Default_" + _suffix;

            // With a payload, `CreateInstance` returns it, which exercises the default `GetPolicy`.
            // Without one, it defers to the base `CreateInstance`, which returns the default value.
            protected override TestPayload? CreateInstance()
            {
                return _payload ?? base.CreateInstance();
            }
        }

        // Overrides only `GetKey`, which covers the base `CreateInstance` returning the default value.
        private sealed class BareBonesCache : ObjectCache<TestPayload>
        {
            private readonly string _suffix;
            public BareBonesCache(string suffix) { _suffix = suffix; }
            protected override string GetKey() => "ObjectCacheTests_Bare_" + _suffix;
        }

        [Fact]
        [DisplayName("Get caches the value through the default GetPolicy when GetPolicy is not overridden")]
        public void Get_UsesDefaultGetPolicy_WhenNotOverridden()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var payload = new TestPayload { Value = "default-policy" };
            var cache = new DefaultPolicyCache(suffix, payload);

            var first = cache.Get();
            Assert.Same(payload, first);

            // The second Get is served from the cache, which proves the value was written with the default policy.
            var second = cache.Get();
            Assert.Same(payload, second);

            cache.Remove();
        }

        [Fact]
        [DisplayName("Get returns null and caches nothing when CreateInstance is not overridden")]
        public void Get_UsesDefaultCreateInstance_ReturnsNull()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var cache = new BareBonesCache(suffix);

            var result = cache.Get();

            Assert.Null(result);

            cache.Remove();
        }
    }
}
