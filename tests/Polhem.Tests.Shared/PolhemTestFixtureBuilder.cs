using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Settings;
using Polhem.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Polhem.Tests.Shared
{
    /// <summary>
    /// Builder for <see cref="PolhemTestFixture"/>. Customises the <see cref="PathOptions"/>
    /// resolution and the underlying <see cref="BackendConfiguration"/> before the per-fixture
    /// <see cref="IServiceProvider"/> is built.
    /// </summary>
    public sealed class PolhemTestFixtureBuilder
    {
        private bool _useTempDefinePath;
        private bool _useSharedDatabases;
        private Action<BackendConfiguration>? _configureBackend;

        /// <summary>
        /// Redirects the fixture's <see cref="PathOptions.DefinePath"/> to a per-fixture
        /// temporary directory; the directory is seeded with a copy of the shared
        /// <c>tests/Define</c> fixture (so <c>SystemSettings.xml</c> etc. resolve correctly)
        /// and deleted when the fixture is disposed.
        /// </summary>
        public PolhemTestFixtureBuilder UseTempDefinePath()
        {
            _useTempDefinePath = true;
            return this;
        }

        /// <summary>
        /// Opts the fixture into the process-wide shared database setup: registers
        /// ADO.NET providers + dialect factories per <c>DatabaseType</c>, seeds the
        /// matching <c>DatabaseItem</c> entries (when <c>POLHEM_TEST_CONNSTR_*</c> env
        /// vars are set), and creates/upgrades the shared <c>st_user</c>/<c>st_session</c>
        /// schemas plus seed user. Idempotent across the process — use for fixtures
        /// driving <c>[DbFact]</c> integration tests.
        /// </summary>
        public PolhemTestFixtureBuilder UseSharedDatabases()
        {
            _useSharedDatabases = true;
            return this;
        }

        /// <summary>
        /// Applies a callback to the loaded <see cref="BackendConfiguration"/> before the
        /// service provider is built. Useful for switching encryption-key sources, swapping
        /// component types, etc.
        /// </summary>
        /// <param name="configure">The configuration callback.</param>
        public PolhemTestFixtureBuilder ConfigureBackend(Action<BackendConfiguration> configure)
        {
            _configureBackend = configure ?? throw new ArgumentNullException(nameof(configure));
            return this;
        }

        internal PathOptions BuildPathOptions(out string? tempDir)
        {
            // SharedDefinePath: process-wide merged dir (tests/Define + framework
            // defaults materialised from Polhem.Definition.Defaults). Built once by
            // TestProcessBootstrap.EnsureInitialized() before any fixture ctor runs.
            var sharedDefine = TestProcessBootstrap.SharedDefinePath;
            // The customization root is process-wide and empty, so every fixture behaves as a
            // standard non-customized deployment until a test writes a tenant folder into it.
            // Sharing it with the bootstrap container matters: near-end API calls run there, so
            // two different roots would mean a test writes to one and the API reads the other.
            var sharedCustomize = TestProcessBootstrap.SharedCustomizePath;

            if (!_useTempDefinePath)
            {
                tempDir = null;
                return new PathOptions { DefinePath = sharedDefine, CustomizePath = sharedCustomize };
            }

            tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fixture-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            CopyDirectory(sharedDefine, tempDir);
            return new PathOptions { DefinePath = tempDir, CustomizePath = sharedCustomize };
        }

        internal ServiceProvider BuildServiceProvider(PathOptions paths)
        {
            var settings = SystemSettingsLoader.Load(paths);

            // `tests/Define/SystemSettings.xml` points `MasterKeySource` at the `POLHEM_MASTER_KEY` environment
            // variable, and `TestProcessBootstrap` sets it to a hard-coded test key when it is missing.
            // The fixture therefore does not need to override `MasterKeySource`.

            _configureBackend?.Invoke(settings.BackendConfiguration);

            var services = new ServiceCollection();
            services.AddPolhemFramework(settings.BackendConfiguration, paths, autoCreateMasterKey: true);

            // NOTE: there is deliberately no per-fixture cache key prefix. The bootstrap and the fixture have
            // different `ICacheContainer` instances but share the underlying `CacheInfo.Provider`, and a prefix
            // would hide the bootstrap's changes to `DatabaseSettings.Items` from the fixture's cache.
            // Sessions stay isolated because access tokens are random `Guid` values.

            var provider = services.BuildServiceProvider();

            if (_useSharedDatabases)
            {
                // Schema + seed are process-wide (idempotent); resolved IDefineAccess
                // shares the same DatabaseSettings cache that SharedDatabaseState
                // populated via GlobalFixture's bootstrap path.
                var defineAccess = provider.GetRequiredService<Polhem.Definition.Storage.IDefineAccess>();
                // …though "shares the same cache" only holds until someone invalidates that slot,
                // so re-apply the registrations before reading them rather than assuming.
                SharedDatabaseState.EnsureDatabaseSettingsApplied(defineAccess);
                SharedDatabaseState.EnsureSchemaAndSeed(
                    defineAccess,
                    provider.GetRequiredService<IDbConnectionManager>());
            }

            return provider;
        }

        private static void CopyDirectory(string source, string dest)
        {
            foreach (var subdir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(source, subdir);
                Directory.CreateDirectory(Path.Combine(dest, rel));
            }
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(source, file);
                File.Copy(file, Path.Combine(dest, rel), overwrite: true);
            }
        }
    }
}
