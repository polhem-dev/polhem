using System.ComponentModel;
using Polhem.ObjectCaching.Providers;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// Tests of how <see cref="CacheItemPolicy.ChangeNotifyKey"/> invalidates entries.
    /// </summary>
    /// <remarks>
    /// Every test generates a unique notify key from a GUID, so even though the process-wide
    /// <see cref="CacheInfo.NotifyVersions"/> is shared, tests do not interfere with each other and no static
    /// state needs to be serialized or swapped.
    /// </remarks>
    public class CacheNotifyTokenTests
    {
        private static string NewNotifyKey() => $"TestGroup:{Guid.NewGuid():N}";

        private static string NewCacheKey() => $"notify-{Guid.NewGuid():N}";

        [Fact]
        [DisplayName("An entry with a ChangeNotifyKey is invalidated after its notify version is bumped")]
        public void Get_AfterVersionBump_EntryIsInvalidated()
        {
            using var provider = new MemoryCacheProvider();
            string notifyKey = NewNotifyKey();
            string cacheKey = NewCacheKey();

            provider.Set(cacheKey, "v1", new CacheItemPolicy { ChangeNotifyKey = notifyKey });
            Assert.Equal("v1", provider.Get(cacheKey));

            // Simulates the poller observing a version bump after another process wrote a definition.
            CacheInfo.NotifyVersions.SetVersion(notifyKey, 1);

            Assert.Null(provider.Get(cacheKey));
        }

        [Fact]
        [DisplayName("An entry is retained while its notify version is unchanged")]
        public void Get_WithoutVersionBump_EntryIsRetained()
        {
            using var provider = new MemoryCacheProvider();
            string cacheKey = NewCacheKey();

            provider.Set(cacheKey, "v1", new CacheItemPolicy { ChangeNotifyKey = NewNotifyKey() });

            Assert.Equal("v1", provider.Get(cacheKey));
        }

        [Fact]
        [DisplayName("Bumping the version of another notify key does not affect an unrelated entry")]
        public void Get_AfterUnrelatedVersionBump_EntryIsRetained()
        {
            using var provider = new MemoryCacheProvider();
            string cacheKey = NewCacheKey();

            provider.Set(cacheKey, "v1", new CacheItemPolicy { ChangeNotifyKey = NewNotifyKey() });
            CacheInfo.NotifyVersions.SetVersion(NewNotifyKey(), 99);

            Assert.Equal("v1", provider.Get(cacheKey));
        }

        [Fact]
        [DisplayName("An entry without a ChangeNotifyKey is not affected by any version bump")]
        public void Get_WithoutNotifyKey_IgnoresVersionBump()
        {
            using var provider = new MemoryCacheProvider();
            string notifyKey = NewNotifyKey();
            string cacheKey = NewCacheKey();

            provider.Set(cacheKey, "v1", new CacheItemPolicy(CacheTimeKind.SlidingTime, 5));
            CacheInfo.NotifyVersions.SetVersion(notifyKey, 1);

            Assert.Equal("v1", provider.Get(cacheKey));
        }

        [Fact]
        [DisplayName("The version store returns 0 for a key it has never observed")]
        public void GetVersion_UnobservedKey_ReturnsZero()
        {
            var store = new CacheNotifyVersionStore();

            Assert.Equal(0L, store.GetVersion(NewNotifyKey()));
        }

        [Fact]
        [DisplayName("The version store keeps the last version written")]
        public void SetVersion_ThenGetVersion_ReturnsLatest()
        {
            var store = new CacheNotifyVersionStore();
            string notifyKey = NewNotifyKey();

            store.SetVersion(notifyKey, 3);
            store.SetVersion(notifyKey, 7);

            Assert.Equal(7L, store.GetVersion(notifyKey));
        }
    }
}
