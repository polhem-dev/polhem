using System.ComponentModel;
using Polhem.Definition.Identity;
using Polhem.Definition.Storage;
using Polhem.ObjectCaching;
using Polhem.Tests.Shared;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Smoke tests for <see cref="PolhemTestFixture"/>. Verifies the per-class fixture builds
    /// its own <see cref="IServiceProvider"/>, resolves framework services, and (for
    /// <c>UseTempDefinePath</c>) provides a writable per-fixture <see cref="PathOptions"/>.
    /// </summary>
    [Collection(ProcessWideStateCollection.Name)]
    public class PolhemTestFixtureSmokeTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public PolhemTestFixtureSmokeTests(PolhemTestFixture fx)
        {
            _fx = fx;
        }

        [Fact]
        [DisplayName("PolhemTestFixture points by default to the process-wide shared define path of TestProcessBootstrap")]
        public void DefaultFixture_PointsToSharedDefine()
        {
            Assert.NotNull(_fx.PathOptions);
            Assert.False(string.IsNullOrEmpty(_fx.DefinePath));
            // Since the framework defaults moved to src/Polhem.Definition/Defaults/, the default fixture points to
            // `TestProcessBootstrap.SharedDefinePath`: a process-wide temp directory holding tests/Define plus the framework
            // defaults materialized from the resources embedded in Polhem.Definition.dll.
            Assert.Equal(TestProcessBootstrap.SharedDefinePath, _fx.DefinePath);
            Assert.True(File.Exists(_fx.PathOptions.GetSystemSettingsFilePath()));
            // After the merge, that path resolves both framework files (such as st_user.TableSchema.xml) and the tests' own
            // files (such as ft_project.TableSchema.xml).
            Assert.True(File.Exists(Path.Combine(_fx.DefinePath, "TableSchema", "common", "st_user.TableSchema.xml")));
            Assert.True(File.Exists(Path.Combine(_fx.DefinePath, "TableSchema", "company", "ft_project.TableSchema.xml")));
        }

        [Fact]
        [DisplayName("PolhemTestFixture.GetRequiredService resolves IDefineAccess")]
        public void GetRequiredService_IDefineAccess_Succeeds()
        {
            var access = _fx.GetRequiredService<IDefineAccess>();
            Assert.NotNull(access);
            var settings = access.GetSystemSettings();
            Assert.NotNull(settings.BackendConfiguration);
        }

        [Fact]
        [DisplayName("IDefineAccess.GetCurrencySettings reads back the framework default currency master (the full storage, cache and access chain)")]
        public void GetCurrencySettings_ReturnsFrameworkCurrencyMaster()
        {
            var access = _fx.GetRequiredService<IDefineAccess>();

            // `SharedDefinePath` is materialized from the embedded framework defaults, which include a curated CurrencySettings.xml.
            var currencies = access.GetCurrencySettings();

            Assert.NotNull(currencies);
            Assert.NotEmpty(currencies);
            Assert.Equal(2, currencies.GetDecimals("USD"));
            Assert.Equal(0, currencies.GetDecimals("JPY"));
            Assert.Equal(3, currencies.GetDecimals("BHD"));
        }

        [Fact]
        [DisplayName("IDefineAccess.GetUnitSettings reads back the framework default unit master (the full storage, cache and access chain)")]
        public void GetUnitSettings_ReturnsFrameworkUnitMaster()
        {
            var access = _fx.GetRequiredService<IDefineAccess>();

            // `SharedDefinePath` is materialized from the embedded framework defaults, which include a curated UnitSettings.xml.
            var units = access.GetUnitSettings();

            Assert.NotNull(units);
            Assert.NotEmpty(units);
            Assert.Equal(3, units.GetDecimals("KG"));
            Assert.Equal(0, units.GetDecimals("PCS"));
        }

        [Fact]
        [DisplayName("PolhemTestFixture.GetRequiredService resolves the PathOptions singleton")]
        public void GetRequiredService_PathOptions_MatchesFixture()
        {
            var paths = _fx.GetRequiredService<PathOptions>();
            Assert.Same(_fx.PathOptions, paths);
        }

        [Fact]
        [DisplayName("PolhemTestFixture.GetRequiredService resolves ICacheContainer")]
        public void GetRequiredService_ICacheContainer_Succeeds()
        {
            var cache = _fx.GetRequiredService<ICacheContainer>();
            Assert.NotNull(cache);
            Assert.NotNull(cache.SystemSettings);
            Assert.NotNull(cache.SessionInfo);
        }
    }

    /// <summary>
    /// Verifies <see cref="PolhemTestFixtureBuilder.UseTempDefinePath"/> creates an isolated
    /// per-fixture <see cref="PathOptions"/> with a seeded copy of <c>tests/Define</c>.
    /// </summary>
    public class PolhemTestFixtureTempDefineSmokeTests : IClassFixture<PolhemTestFixtureTempDefineSmokeTests.WritableFixture>
    {
        private readonly WritableFixture _fx;

        public PolhemTestFixtureTempDefineSmokeTests(WritableFixture fx)
        {
            _fx = fx;
        }

        public sealed class WritableFixture : PolhemTestFixture
        {
            public WritableFixture() : base(b => b.UseTempDefinePath()) { }
        }

        [Fact]
        [DisplayName("UseTempDefinePath gives each fixture its own temp directory")]
        public void TempDefine_IsolatedDirectory()
        {
            Assert.NotEqual(string.Empty, _fx.DefinePath);
            Assert.True(Directory.Exists(_fx.DefinePath));
            Assert.DoesNotContain(Path.Combine("tests", "Define"), _fx.DefinePath);
            Assert.True(File.Exists(_fx.PathOptions.GetSystemSettingsFilePath()));
        }

        [Fact]
        [DisplayName("IDefineAccess of a UseTempDefinePath fixture resolves from DI")]
        public void TempDefine_AccessResolvable()
        {
            // NOTE: The underlying cache store (`CacheInfo.Provider`) is process-wide, so `IDefineAccess.GetSystemSettings()`
            // can still return the shared fixture's instance through the cache. This test therefore only verifies that
            // `IDefineAccess` and `PathOptions` resolve from DI; the per-fixture write path is not verified here.
            var access = _fx.GetRequiredService<IDefineAccess>();
            var paths = _fx.GetRequiredService<PathOptions>();
            Assert.NotNull(access);
            Assert.Equal(_fx.DefinePath, paths.DefinePath);
        }
    }

    /// <summary>
    /// PolhemTestFixture deliberately applies no per-fixture prefix to <see cref="ICacheContainer"/> keys: with the cache
    /// taking PathOptions through its constructor, a prefix would make the fixture's keys diverge from the bootstrap path of
    /// SharedDatabaseState and leave DatabaseSettings.Items inconsistent. Sessions stay isolated because the production
    /// code generates random Guid access tokens. These tests verify that each fixture still holds its own
    /// IServiceProvider and service instances.
    /// </summary>
    public class PolhemTestFixturePerInstanceIsolationTests
    {
        [Fact]
        [DisplayName("Two PolhemTestFixture instances have independent ISessionInfoService instances")]
        public void TwoFixtures_HaveIndependentSessionServices()
        {
            using var fxA = new PolhemTestFixture();
            using var fxB = new PolhemTestFixture();

            var svcA = fxA.GetRequiredService<ISessionInfoService>();
            var svcB = fxB.GetRequiredService<ISessionInfoService>();

            Assert.NotSame(svcA, svcB);
        }

        [Fact]
        [DisplayName("Two PolhemTestFixture instances have independent ICacheContainer instances")]
        public void TwoFixtures_HaveIndependentCacheContainers()
        {
            using var fxA = new PolhemTestFixture();
            using var fxB = new PolhemTestFixture();

            var cacheA = fxA.GetRequiredService<ICacheContainer>();
            var cacheB = fxB.GetRequiredService<ICacheContainer>();

            Assert.NotSame(cacheA, cacheB);
            Assert.NotSame(cacheA.SessionInfo, cacheB.SessionInfo);
        }
    }
}
