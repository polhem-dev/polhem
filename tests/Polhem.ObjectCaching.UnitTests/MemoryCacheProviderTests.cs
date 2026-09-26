using System.ComponentModel;
using Polhem.ObjectCaching.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;

namespace Polhem.ObjectCaching.UnitTests
{
    public class MemoryCacheProviderTests
    {
        private static MemoryCacheProvider CreateProvider() => new();

        private static CacheItemPolicy DefaultPolicy() =>
            new CacheItemPolicy(CacheTimeKind.SlidingTime, 5);

        [Fact]
        [DisplayName("Contains returns true after Set and false for a key that does not exist")]
        public void Contains_AfterSet_ReturnsTrue_OtherwiseFalse()
        {
            using var provider = CreateProvider();
            provider.Set("foo", "bar", DefaultPolicy());

            Assert.True(provider.Contains("foo"));
            Assert.False(provider.Contains("missing"));
        }

        [Fact]
        [DisplayName("Get returns the value previously Set")]
        public void Get_AfterSet_ReturnsValue()
        {
            using var provider = CreateProvider();
            provider.Set("hello", "world", DefaultPolicy());

            Assert.Equal("world", provider.Get("hello"));
        }

        [Fact]
        [DisplayName("Get returns null for a key that does not exist")]
        public void Get_MissingKey_ReturnsNull()
        {
            using var provider = CreateProvider();
            Assert.Null(provider.Get("not-exists"));
        }

        [Fact]
        [DisplayName("Key comparison is case-insensitive")]
        public void Set_KeyIsCaseInsensitive()
        {
            using var provider = CreateProvider();
            provider.Set("Mixed", 123, DefaultPolicy());

            Assert.True(provider.Contains("mixed"));
            Assert.True(provider.Contains("MIXED"));
            Assert.Equal(123, provider.Get("mIxEd"));
        }

        [Fact]
        [DisplayName("Remove removes the specified cache entry")]
        public void Remove_ExistingKey_RemovesEntry()
        {
            using var provider = CreateProvider();
            provider.Set("k1", "v1", DefaultPolicy());

            provider.Remove("k1");

            Assert.False(provider.Contains("k1"));
        }

        [Fact]
        [DisplayName("Remove does not throw for a key that does not exist")]
        public void Remove_MissingKey_DoesNotThrow()
        {
            using var provider = CreateProvider();
            var exception = Record.Exception(() => provider.Remove("not-exists"));
            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("GetCount reflects the current number of cache entries")]
        public void GetCount_ReflectsCurrentCache()
        {
            using var provider = CreateProvider();
            provider.Set("a", 1, DefaultPolicy());
            provider.Set("b", 2, DefaultPolicy());

            Assert.Equal(2, provider.GetCount());

            provider.Remove("a");
            Assert.Equal(1, provider.GetCount());
        }

        [Fact]
        [DisplayName("Get returns null after the AbsoluteExpiration has passed")]
        public void Set_WithAbsoluteExpiration_EvictsAfterDeadline()
        {
            // A fake clock replaces real waiting. `MemoryCache` decides expiry from its clock, so advancing the clock
            // is enough and there is no need to sleep past the deadline (real waits are unreliable on a loaded CI).
            var clock = new FakeClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            using var provider = new MemoryCacheProvider(
                new MemoryCache(new MemoryCacheOptions { Clock = clock }));
            var policy = new CacheItemPolicy
            {
                AbsoluteExpiration = clock.UtcNow.AddMilliseconds(50)
            };
            provider.Set("k", "v", policy);
            Assert.Equal("v", provider.Get("k"));

            clock.Advance(TimeSpan.FromMilliseconds(51));

            Assert.Null(provider.Get("k"));
        }

        /// <summary>
        /// A fake clock that can be advanced, injected as <see cref="MemoryCacheOptions.Clock"/>
        /// so the expiry test does not depend on the real wall clock.
        /// </summary>
        private sealed class FakeClock : ISystemClock
        {
            public FakeClock(DateTimeOffset start) { UtcNow = start; }

            public DateTimeOffset UtcNow { get; private set; }

            public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
        }

        [Fact]
        [DisplayName("Set with ChangeMonitorFilePaths creates the cache entry")]
        public void Set_WithFileChangeMonitor_DoesNotThrow()
        {
            using var provider = CreateProvider();
            using var watchDir = WatchDirectory.Create();
            var tempFile = Path.Combine(watchDir.Path, "watched.tmp");
            File.WriteAllText(tempFile, "x");

            var policy = new CacheItemPolicy
            {
                ChangeMonitorFilePaths = new[] { tempFile },
                SlidingExpiration = TimeSpan.FromMinutes(1)
            };
            provider.Set("with-monitor", "v", policy);
            Assert.Equal("v", provider.Get("with-monitor"));
        }

        [Fact]
        [DisplayName("A cache entry is evicted after a file watched by ChangeMonitorFilePaths changes")]
        public async Task Set_WithFileChangeMonitor_EvictsOnFileChange()
        {
            using var provider = CreateProvider();
            using var watchDir = WatchDirectory.Create();
            var tempFile = Path.Combine(watchDir.Path, "watched.tmp");
            File.WriteAllText(tempFile, "initial");

            var policy = new CacheItemPolicy
            {
                ChangeMonitorFilePaths = new[] { tempFile },
                SlidingExpiration = TimeSpan.FromMinutes(10)
            };
            provider.Set("watched", "v", policy);
            Assert.True(provider.Contains("watched"));

            File.WriteAllText(tempFile, "changed");

            // FileModificationToken detects changes lazily on each Contains/Get call; poll until evicted.
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (provider.Contains("watched") && DateTime.UtcNow < deadline)
            {
                await Task.Delay(200);
            }

            Assert.False(provider.Contains("watched"));
        }

        /// <summary>
        /// A temporary directory per test that holds the watched file.
        /// </summary>
        /// <remarks>
        /// <see cref="Microsoft.Extensions.FileProviders.PhysicalFileProvider"/> actually watches the parent directory
        /// of a watched file. If that directory is shared by tests running at the same time (such as
        /// <see cref="Path.GetTempPath"/>), another test creating or deleting a file there can trigger the change
        /// token by mistake and evict this test's entry early. A dedicated subdirectory isolates the watcher and
        /// avoids that race.
        /// </remarks>
        private sealed class WatchDirectory : IDisposable
        {
            public string Path { get; }

            private WatchDirectory(string path) { Path = path; }

            public static WatchDirectory Create()
            {
                var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"polhem-mctest-{Guid.NewGuid():N}");
                Directory.CreateDirectory(dir);
                return new WatchDirectory(dir);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Path))
                        Directory.Delete(Path, recursive: true);
                }
                catch (IOException)
                {
                    // best effort
                }
            }
        }
    }
}
