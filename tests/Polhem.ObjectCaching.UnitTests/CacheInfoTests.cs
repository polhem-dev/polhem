using System.ComponentModel;
using Polhem.Definition.Settings;
using Polhem.ObjectCaching.Providers;

namespace Polhem.ObjectCaching.UnitTests
{
    public class CacheInfoTests
    {
        [Fact]
        [DisplayName("Initialize keeps the existing Provider instance when the configured type matches it (covers the type comparison)")]
        public void Initialize_SameProviderType_DoesNotReplaceProvider()
        {
            var configuration = new BackendConfiguration();
            configuration.Components.CacheProvider =
                "Polhem.ObjectCaching.Providers.MemoryCacheProvider, Polhem.ObjectCaching";
            var originalProvider = CacheInfo.Provider;

            CacheInfo.Initialize(configuration);

            // The type is the same, so `Initialize` returns early and keeps the instance.
            Assert.Same(originalProvider, CacheInfo.Provider);
        }

        [Fact]
        [DisplayName("Initialize throws ArgumentNullException for null")]
        public void Initialize_NullConfiguration_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => CacheInfo.Initialize(null!));
        }

        [Fact]
        [DisplayName("Initialize returns early without replacing the Provider when CacheProvider is an empty string")]
        public void Initialize_EmptyCacheProvider_ReturnsEarlyWithoutChange()
        {
            var config = new BackendConfiguration();
            config.Components.CacheProvider = string.Empty;
            var originalProvider = CacheInfo.Provider;

            CacheInfo.Initialize(config);

            Assert.Same(originalProvider, CacheInfo.Provider);
        }

        [Fact]
        [DisplayName("Initialize creates a new Provider instance when the configured type differs from the current one")]
        public void Initialize_DifferentProviderType_ReplacesProvider()
        {
            // Uses the default `MemoryCacheProvider` setting, but first swaps the static provider for a different type.
            var config = new BackendConfiguration();
            var originalProvider = CacheInfo.Provider;
            CacheInfo.Provider = new FakeCacheProvider();
            try
            {
                CacheInfo.Initialize(config);
                // The type differs, so there is no early return and a new `MemoryCacheProvider` is created.
                Assert.IsType<MemoryCacheProvider>(CacheInfo.Provider);
            }
            finally
            {
                CacheInfo.Provider = originalProvider;
            }
        }

        [Theory]
        [InlineData("Polhem.ObjectCaching.Providers.NoSuchProvider, Polhem.NoSuchAssembly")]
        [InlineData("Polhem.ObjectCaching.Providers.NoSuchProvider, Polhem.ObjectCaching")]
        [InlineData("Polhem.ObjectCaching.CacheItemPolicy, Polhem.ObjectCaching")]
        [DisplayName("Initialize rejects an unusable CacheProvider name at startup, naming the setting, and keeps the current Provider")]
        public void Initialize_UnusableCacheProvider_ThrowsAndKeepsProvider(string typeName)
        {
            var config = new BackendConfiguration();
            config.Components.CacheProvider = typeName;
            var originalProvider = CacheInfo.Provider;

            var ex = Assert.Throws<InvalidOperationException>(() => CacheInfo.Initialize(config));

            Assert.Contains("BackendComponents.CacheProvider", ex.Message, StringComparison.Ordinal);
            Assert.Contains(typeName, ex.Message, StringComparison.Ordinal);
            Assert.Same(originalProvider, CacheInfo.Provider);
        }

        private sealed class FakeCacheProvider : ICacheProvider
        {
            public bool Contains(string key) => false;
            public void Set(string key, object value, CacheItemPolicy policy) { }
            public object? Get(string key) => null;
            public void Remove(string key) { }
            public long GetCount() => 0;
        }
    }
}
