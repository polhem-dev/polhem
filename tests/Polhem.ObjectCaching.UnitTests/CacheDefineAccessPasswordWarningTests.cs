using System.ComponentModel;
using Polhem.Base.Security;
using Polhem.Definition;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Microsoft.Extensions.Logging;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// Tests the warning <see cref="CacheDefineAccess"/> logs when database passwords exist but no configuration
    /// encryption key is configured, so they are stored and read in clear text.
    /// </summary>
    public class CacheDefineAccessPasswordWarningTests
    {
        private static CacheDefineAccess CreateAccess(PathOptions paths, byte[] configKey, ILogger logger)
        {
            var storage = new FileDefineStorage(paths);
            // A private cache prefix keeps this container's entries apart from any other on the process-wide provider.
            var cache = new CacheContainerService(storage, paths, "pwwarn-" + Guid.NewGuid().ToString("N"));
            return new CacheDefineAccess(storage, paths, cache, configKey, null, logger);
        }

        private static DatabaseSettings SettingsWithPassword()
        {
            var settings = new DatabaseSettings();
            settings.Servers!.Add(new DatabaseServer { Id = "s1", Password = "plain-pw" });
            return settings;
        }

        [Fact]
        [DisplayName("SaveDatabaseSettings without a ConfigEncryptionKey logs a warning when a password is present")]
        public void SaveDatabaseSettings_NoKeyWithPassword_LogsWarning()
        {
            using var temp = TempDir.Create();
            var logger = new ListLogger();

            CreateAccess(temp.Options, [], logger).SaveDatabaseSettings(SettingsWithPassword());

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("ConfigEncryptionKey", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("plain-pw", entry.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("SaveDatabaseSettings with a ConfigEncryptionKey encrypts the password and logs nothing")]
        public void SaveDatabaseSettings_WithKey_LogsNothing()
        {
            using var temp = TempDir.Create();
            var logger = new ListLogger();
            var settings = SettingsWithPassword();

            CreateAccess(temp.Options, AesCbcHmacKeyGenerator.GenerateCombinedKey(), logger).SaveDatabaseSettings(settings);

            Assert.Empty(logger.Entries);
            Assert.StartsWith("enc:", settings.Servers!["s1"].Password, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("SaveDatabaseSettings without a key logs nothing when no password is present")]
        public void SaveDatabaseSettings_NoKeyNoPassword_LogsNothing()
        {
            using var temp = TempDir.Create();
            var logger = new ListLogger();

            CreateAccess(temp.Options, [], logger).SaveDatabaseSettings(new DatabaseSettings());

            Assert.Empty(logger.Entries);
        }

        [Fact]
        [DisplayName("GetDatabaseSettings without a key warns once about stored passwords, not on every read")]
        public void GetDatabaseSettings_NoKeyWithPassword_WarnsOnce()
        {
            using var temp = TempDir.Create();
            CreateAccess(temp.Options, [], new ListLogger()).SaveDatabaseSettings(SettingsWithPassword());
            var logger = new ListLogger();
            var access = CreateAccess(temp.Options, [], logger);

            access.GetDatabaseSettings();
            access.GetDatabaseSettings();

            Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
        }

        private sealed class TempDir : IDisposable
        {
            private readonly string _path;
            public PathOptions Options { get; }

            private TempDir(string path)
            {
                _path = path;
                Options = new PathOptions { DefinePath = path };
            }

            public static TempDir Create()
            {
                var dir = Path.Combine(Path.GetTempPath(), $"polhem-pwwarn-{Guid.NewGuid():N}");
                Directory.CreateDirectory(dir);
                return new TempDir(dir);
            }

            public void Dispose()
            {
                try { Directory.Delete(_path, recursive: true); }
                catch (IOException) { /* best effort */ }
            }
        }

        private sealed class ListLogger : ILogger
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
                => Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
