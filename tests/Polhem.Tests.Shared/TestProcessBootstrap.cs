using Polhem.Core;
using Polhem.Definition;
using Polhem.Definition.Storage;
using Polhem.Hosting;
using Polhem.ObjectCaching;
using Microsoft.Extensions.DependencyInjection;

namespace Polhem.Tests.Shared
{
    /// <summary>
    /// Process-wide test bootstrap: wires up <see cref="SysInfo"/>, the in-process backend exposed as
    /// <see cref="LocalServices"/> and <see cref="SharedDatabaseState.EnsureRegistered"/> once.
    /// </summary>
    /// <remarks>
    /// <c>ICacheContainer</c> and <c>IDefineAccess</c> are provided by the DI container, so the bootstrap no longer
    /// initializes the <c>CacheContainer</c> static facade (which has been removed).
    /// </remarks>
    public static class TestProcessBootstrap
    {
        private static readonly object s_initLock = new();
        private static bool s_initialized;
        private static string? s_sharedDefinePath;
        private static string? s_sharedCustomizePath;
        private static IServiceProvider? s_localServices;

        /// <summary>
        /// Hard-coded Base64 AES-CBC-HMAC combined key (64 bytes) used by the test
        /// process when <c>POLHEM_MASTER_KEY</c> is not set in the environment. Kept
        /// independent from <c>DemoCredentials.DemoMasterKey</c> so test runs and
        /// sample runs cannot leak encrypted state into one another.
        /// </summary>
        private const string TestMasterKey =
            "oQGvs51A0u5Rn8RPJPkQ9xqXevf451mDHpsaJR7nN8WCM0X0zskVqTqDQBtSpSq8MdvmfKUPKAulOJShd9KDXg==";

        /// <summary>
        /// Process-wide shared define directory: <c>tests/Define</c> (test-specific
        /// fixtures) merged with <see cref="Polhem.Definition.Defaults"/> (framework
        /// defaults embedded in <c>Polhem.Definition.dll</c>). Created once on first
        /// <see cref="EnsureInitialized"/>; cleaned up on process exit.
        /// </summary>
        /// <remarks>
        /// Read-only by convention. Tests that need writable define storage opt in
        /// via <see cref="PolhemTestFixtureBuilder.UseTempDefinePath"/>, which copies
        /// this directory into a per-class temp directory.
        /// </remarks>
        public static string SharedDefinePath
        {
            get
            {
                EnsureInitialized();
                return s_sharedDefinePath!;
            }
        }

        /// <summary>
        /// Process-wide tenant customization root (<c>PathOptions.CustomizePath</c>). Created empty
        /// on first <see cref="EnsureInitialized"/> and cleaned up on process exit.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The directory exists but holds nothing, so every session still resolves against the base
        /// layer: the customization reader is reached only for a non-empty
        /// <c>SessionInfo.CustomizeId</c>, and an id with no folder here yields <c>null</c>. A test
        /// that wants a tenant to have overrides writes them under
        /// <c>{SharedCustomizePath}/{its own customization code}/</c> and picks a code no other test
        /// class uses.
        /// </para>
        /// <para>
        /// Rooted here rather than per fixture because the near-end API path
        /// (<see cref="LocalServices"/>) runs on the bootstrap container: a fixture-only
        /// customization root would leave every call made through a connector reading a different
        /// root from the one the test wrote to.
        /// </para>
        /// </remarks>
        public static string SharedCustomizePath
        {
            get
            {
                EnsureInitialized();
                return s_sharedCustomizePath!;
            }
        }

        /// <summary>
        /// The process-wide backend container that local connectors dispatch to: pass it to the
        /// local constructors of <c>SystemApiConnector</c>, <c>FormApiConnector</c> and the others.
        /// </summary>
        public static IServiceProvider LocalServices
        {
            get
            {
                EnsureInitialized();
                return s_localServices!;
            }
        }

        /// <summary>
        /// Runs the process-wide static wire-up on the first call; later calls return immediately.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (s_initialized) return;
            lock (s_initLock)
            {
                if (s_initialized) return;
                InitializeOnce();
                s_initialized = true;
            }
        }

        private static void InitializeOnce()
        {
            // Set `POLHEM_MASTER_KEY` before any test class is constructed. `SystemSettings` defaults to
            // `MasterKeySource.Type=Environment`, and `MasterKeyProvider` throws when the variable is unset.
            // Production-like environments inject it from outside; this fallback applies only in the test process.
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POLHEM_MASTER_KEY")))
            {
                Environment.SetEnvironmentVariable("POLHEM_MASTER_KEY", TestMasterKey);
            }

            s_sharedDefinePath = CreateSharedDefinePath();
            s_sharedCustomizePath = CreateSharedCustomizePath();
            var pathOptions = new PathOptions
            {
                DefinePath = s_sharedDefinePath,
                CustomizePath = s_sharedCustomizePath
            };

            // A temporary define access lets `SharedDatabaseState.EnsureRegistered` write `DatabaseSettings.Items`
            // before `AddPolhemFramework` runs. It lives only inside `InitializeOnce` and is not exposed; the DI
            // container gets its own `IDefineAccess` from `AddPolhemFramework`, sharing the cache through the same
            // `PathOptions`.
            var bootstrapStorage = new FileDefineStorage(pathOptions);
            var bootstrapAccess = new CacheDefineAccess(bootstrapStorage, pathOptions);

            SharedDatabaseState.EnsureRegistered(bootstrapAccess);

            // Boot-time settings are read through `SystemSettingsLoader`, which does not depend on `IDefineAccess`.
            // `tests/Define/SystemSettings.xml` points `MasterKeySource` at the `POLHEM_MASTER_KEY` environment
            // variable, which the start of this method sets, so payloads can be decrypted.
            var settings = SystemSettingsLoader.Load(pathOptions);
            SysInfo.Initialize(settings.CommonConfiguration);

            // The framework has no process-wide static facades; every service, including `IDbConnectionManager`,
            // is resolved through constructor injection from this container.
            var services = new ServiceCollection();
            services.AddPolhemFramework(settings.BackendConfiguration, pathOptions, autoCreateMasterKey: true);
            s_localServices = services.BuildServiceProvider();
        }

        /// <summary>
        /// Creates the process-wide shared define directory: a temp dir populated
        /// first with the contents of <c>tests/Define</c> (test-specific fixtures
        /// win on conflict), then with the framework defaults from
        /// <see cref="Defaults.MaterializeTo"/> (skip-existing). Registers a
        /// process-exit cleanup hook.
        /// </summary>
        private static string CreateSharedDefinePath()
        {
            var repoRoot = RepoRoot.Find();
            var testsDefine = Path.Combine(repoRoot, "tests", "Define");

            var sharedDir = Path.Combine(
                Path.GetTempPath(),
                $"polhem-tests-define-{Environment.ProcessId}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(sharedDir);

            // Step 1: copy tests/Define first. Test-specific fixtures (DbCategorySettings
            // with ft_project, Project FormSchema/Layout/Language, PermGateForm,
            // SystemSettings, DatabaseSettings) win on conflict so they shadow any
            // framework default with the same relative path.
            CopyDirectory(testsDefine, sharedDir);

            // Step 2: materialise framework defaults from Polhem.Definition.dll's embedded
            // resources. Overwrite=false so step 1's test-specific files (e.g. the
            // extended DbCategorySettings.xml) are preserved.
            Defaults.MaterializeTo(sharedDir, MaterializeOptions.Default);

            // Best-effort cleanup on process exit. xunit's parallel runners do not
            // share processes, so each runner cleans its own dir.
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                try { Directory.Delete(sharedDir, recursive: true); }
                catch (IOException) { /* best effort */ }
                catch (UnauthorizedAccessException) { /* best effort */ }
            };

            return sharedDir;
        }

        /// <summary>
        /// Creates the process-wide customization root as an empty directory and registers a
        /// process-exit cleanup hook. See <see cref="SharedCustomizePath"/> for why it is
        /// process-wide rather than per fixture.
        /// </summary>
        private static string CreateSharedCustomizePath()
        {
            var customizeDir = Path.Combine(
                Path.GetTempPath(),
                $"polhem-tests-customize-{Environment.ProcessId}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(customizeDir);

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                try { Directory.Delete(customizeDir, recursive: true); }
                catch (IOException) { /* best effort */ }
                catch (UnauthorizedAccessException) { /* best effort */ }
            };

            return customizeDir;
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
