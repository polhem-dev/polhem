using System.ComponentModel;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Settings;

namespace Polhem.Db.UnitTests.Manager
{
    /// <summary>
    /// A connection info build overlapped by a settings change must not be cached, because
    /// <see cref="DbConnectionManagerService"/>'s cache never expires on its own.
    /// </summary>
    public sealed class DbConnectionManagerInvalidationRaceTests
    {
        /// <summary>
        /// Returns the settings it holds, and runs a callback on the first read. The callback runs after
        /// the build has started and before it stores anything, which is the window of the race.
        /// </summary>
        private sealed class InterleavingSettingsProvider : IDatabaseSettingsProvider
        {
            private int _reads;

            public DatabaseSettings Settings { get; } = new();

            public Action? OnFirstRead { get; set; }

            public DatabaseSettings Get()
            {
                if (Interlocked.Increment(ref _reads) == 1)
                    OnFirstRead?.Invoke();
                return Settings;
            }

            public DatabaseItem GetItem(string databaseId) => Settings.Items![databaseId];

            public void ValidateRequired()
            {
                // Not exercised by these tests.
            }
        }

        private static DatabaseItem NewItem(string id) => new()
        {
            Id = id,
            DatabaseType = DatabaseType.SQLServer,
            ConnectionString = "Server=old;Database=db;"
        };

        [Fact]
        [DisplayName("A connection info built while Clear runs is returned but not cached")]
        public void GetConnectionInfo_ClearDuringBuild_DoesNotCacheResult()
        {
            TestDbProviders.EnsureSqlServerRegistered();
            string id = $"polhem_race_{Guid.NewGuid():N}";
            var provider = new InterleavingSettingsProvider();
            provider.Settings.Items!.Add(NewItem(id));
            using var manager = new DbConnectionManagerService(provider);
            provider.OnFirstRead = manager.Clear;

            var info = manager.GetConnectionInfo(id);

            Assert.Contains("old", info.ConnectionString, StringComparison.Ordinal);
            Assert.False(manager.Contains(id));
        }

        [Fact]
        [DisplayName("After a build overlapped by Clear, the next read builds from the current settings")]
        public void GetConnectionInfo_AfterOverlappedBuild_ReadsCurrentSettings()
        {
            TestDbProviders.EnsureSqlServerRegistered();
            string id = $"polhem_race_{Guid.NewGuid():N}";
            var provider = new InterleavingSettingsProvider();
            provider.Settings.Items!.Add(NewItem(id));
            using var manager = new DbConnectionManagerService(provider);
            provider.OnFirstRead = manager.Clear;
            _ = manager.GetConnectionInfo(id);

            provider.Settings.Items![id].ConnectionString = "Server=new;Database=db;";
            var info = manager.GetConnectionInfo(id);

            Assert.Contains("new", info.ConnectionString, StringComparison.Ordinal);
            Assert.True(manager.Contains(id));
        }
    }
}
