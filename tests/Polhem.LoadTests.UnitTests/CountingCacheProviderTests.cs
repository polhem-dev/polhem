using System.ComponentModel;
using Polhem.LoadTests.Caching;
using Polhem.ObjectCaching;

namespace Polhem.LoadTests.UnitTests
{
    /// <summary>
    /// Tests for <see cref="CountingCacheProvider"/>.
    /// </summary>
    public class CountingCacheProviderTests
    {
        private static CountingCacheProvider CreateProvider() => new(new InMemoryCacheProvider());

        [Fact]
        [DisplayName("Get counts a found value as a hit and a missing one as a miss")]
        public void Get_CountsHitsAndMisses()
        {
            var provider = CreateProvider();
            provider.Set("a", "value", new CacheItemPolicy());

            provider.Get("a");
            provider.Get("missing");

            var counters = provider.Snapshot();
            Assert.Equal(1, counters.Hits);
            Assert.Equal(1, counters.Misses);
            Assert.Equal(2, counters.Reads);
            Assert.Equal(0.5, counters.HitRate);
        }

        [Fact]
        [DisplayName("Set counts every write, so Writes equals the number of actual builds (evidence of single-flight convergence)")]
        public void Set_CountsWrites()
        {
            var provider = CreateProvider();

            provider.Set("a", 1, new CacheItemPolicy());
            provider.Set("b", 2, new CacheItemPolicy());

            Assert.Equal(2, provider.Snapshot().Writes);
        }

        [Fact]
        [DisplayName("HitRate is zero when there are no reads, so Reads must be checked before interpreting it")]
        public void HitRate_NoReads_ReturnsZero()
        {
            var counters = CreateProvider().Snapshot();

            Assert.Equal(0, counters.Reads);
            Assert.Equal(0, counters.HitRate);
        }

        [Fact]
        [DisplayName("Reset zeroes every counter so counting can restart after warm-up")]
        public void Reset_ClearsAllCounters()
        {
            var provider = CreateProvider();
            provider.Set("a", 1, new CacheItemPolicy());
            provider.Get("a");
            provider.Get("missing");
            provider.Remove("a");

            provider.Reset();

            var counters = provider.Snapshot();
            Assert.Equal(0, counters.Hits);
            Assert.Equal(0, counters.Misses);
            Assert.Equal(0, counters.Writes);
            Assert.Equal(0, counters.Removals);
        }

        [Fact]
        [DisplayName("Reset does not affect the contents of the underlying cache")]
        public void Reset_DoesNotClearUnderlyingCache()
        {
            var provider = CreateProvider();
            provider.Set("a", 1, new CacheItemPolicy());

            provider.Reset();

            Assert.NotNull(provider.Get("a"));
            Assert.Equal(1, provider.GetCount());
        }

        [Fact]
        [DisplayName("Contains and GetCount delegate to the underlying cache without counting")]
        public void PassThroughMembers_DoNotCount()
        {
            var provider = CreateProvider();
            provider.Set("a", 1, new CacheItemPolicy());
            provider.Reset();

            Assert.True(provider.Contains("a"));
            Assert.Equal(1, provider.GetCount());

            var counters = provider.Snapshot();
            Assert.Equal(0, counters.Reads);
        }

        [Fact]
        [DisplayName("Get counts every read under concurrency")]
        public void Get_UnderConcurrency_CountsEveryRead()
        {
            var provider = CreateProvider();
            provider.Set("a", 1, new CacheItemPolicy());
            provider.Reset();

            const int workers = 8;
            const int readsPerWorker = 500;
            Parallel.For(0, workers, _ =>
            {
                for (int i = 0; i < readsPerWorker; i++) { provider.Get("a"); }
            });

            Assert.Equal(workers * readsPerWorker, provider.Snapshot().Hits);
        }

        [Fact]
        [DisplayName("CountingCacheProvider constructor throws ArgumentNullException for a null inner provider")]
        public void Constructor_NullInner_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CountingCacheProvider(null!));
        }
    }
}
