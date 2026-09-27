using System.ComponentModel;
using Polhem.Api.Client.Connectors;
using Polhem.Definition;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Tests for the async typed access, caching and exception propagation of <see cref="ClientDefineAccess"/>.
    /// It is built on a local <see cref="SystemApiConnector"/>, which dispatches to
    /// <see cref="Polhem.Tests.Shared.TestProcessBootstrap.LocalServices"/>, wired up once by the constructor of
    /// <see cref="Polhem.Tests.Shared.PolhemTestFixture"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class **does not touch the database**: all definitions come from files, and the token comes from
    /// <see cref="Polhem.Tests.Shared.TestSessionFactory.CreateAccessToken"/>, which writes the SessionInfo
    /// straight into the session cache. When the server finds it there, it does not need to query <c>st_session</c>.
    /// </para>
    /// <para>
    /// NOTE: each test used to call the local <c>GetDefine</c> with a bare <c>Guid.NewGuid()</c>. That method
    /// requires an authenticated identity, so the server-side session cache missed and took the rebuild path,
    /// reading <c>st_session</c>. The fix at the time was to attach <c>SharedDbFixture</c> so there was a table
    /// to read, which made a set of tests about definition file access and caching need a database container.
    /// The right fix is not to create that dependency at all.
    /// </para>
    /// </remarks>
    public class ClientDefineAccessTests : IClassFixture<Polhem.Tests.Shared.PolhemTestFixture>
    {
        private readonly Polhem.Tests.Shared.PolhemTestFixture _fx;

        public ClientDefineAccessTests(Polhem.Tests.Shared.PolhemTestFixture fx)
        {
            // The fixture triggers the wiring of `TestProcessBootstrap.LocalServices` and is the target for
            // seeding the session. The test methods themselves use the process-wide `LocalServices`.
            _fx = fx;
        }

        private ClientDefineAccess CreateAccess()
        {
            var connector = new SystemApiConnector(Polhem.Tests.Shared.TestProcessBootstrap.LocalServices, Polhem.Tests.Shared.TestSessionFactory.CreateAccessToken(_fx));
            return new ClientDefineAccess(connector);
        }

        [Fact]
        [DisplayName("ClientDefineAccess constructor does not throw when given a SystemApiConnector")]
        public void Constructor_WithConnector_DoesNotThrow()
        {
            var connector = new SystemApiConnector(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid());
            var access = new ClientDefineAccess(connector);
            Assert.NotNull(access);
        }

        [Fact]
        [DisplayName("ClientDefineAccess.GetSystemSettingsAsync returns the system settings over a local connection")]
        public async Task GetSystemSettingsAsync_LocalConnector_ReturnsSettings()
        {
            var access = CreateAccess();

            var settings = await access.GetSystemSettingsAsync();

            Assert.NotNull(settings);
        }

        [Fact]
        [DisplayName("ClientDefineAccess.GetDatabaseSettingsAsync returns the database settings over a local connection")]
        public async Task GetDatabaseSettingsAsync_LocalConnector_ReturnsSettings()
        {
            var access = CreateAccess();

            var settings = await access.GetDatabaseSettingsAsync();

            Assert.NotNull(settings);
        }

        [Fact]
        [DisplayName("ClientDefineAccess.GetDbCategorySettingsAsync returns the database category settings over a local connection")]
        public async Task GetDbCategorySettingsAsync_LocalConnector_ReturnsSettings()
        {
            var access = CreateAccess();

            var settings = await access.GetDbCategorySettingsAsync();

            Assert.NotNull(settings);
        }

        [Fact]
        [DisplayName("ClientDefineAccess.GetFormSchemaAsync returns the form schema over a local connection")]
        public async Task GetFormSchemaAsync_LocalConnector_ReturnsFormSchema()
        {
            var access = CreateAccess();

            var schema = await access.GetFormSchemaAsync("Employee");

            Assert.NotNull(schema);
        }

        [Fact]
        [DisplayName("ClientDefineAccess.GetCurrencySettingsAsync does not throw over a local connection (returns null when not deployed)")]
        public async Task GetCurrencySettingsAsync_LocalConnector_DoesNotThrow()
        {
            var access = CreateAccess();

            // The currency master is not necessarily deployed in the test Define fixture. Whether it returns a value
            // or null, the access path must not throw.
            var exception = await Record.ExceptionAsync(() => access.GetCurrencySettingsAsync());

            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("ClientDefineAccess.GetUnitSettingsAsync does not throw over a local connection (returns null when not deployed)")]
        public async Task GetUnitSettingsAsync_LocalConnector_DoesNotThrow()
        {
            var access = CreateAccess();

            var exception = await Record.ExceptionAsync(() => access.GetUnitSettingsAsync());

            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("ClientDefineAccess.GetTableSchemaAsync returns the table schema over a local connection")]
        public async Task GetTableSchemaAsync_LocalConnector_ReturnsTableSchema()
        {
            var access = CreateAccess();

            var schema = await access.GetTableSchemaAsync("common", "st_user");

            Assert.NotNull(schema);
        }

        [Fact]
        [DisplayName("ClientDefineAccess.GetProgramSettingsAsync returns the program settings")]
        public async Task GetProgramSettingsAsync_ReturnsProgramSettings()
        {
            var access = new ClientDefineAccess(new CountingConnector());

            var result = await access.GetProgramSettingsAsync();

            Assert.NotNull(result);
            Assert.IsType<ProgramSettings>(result);
        }

        [Fact]
        [DisplayName("ClientDefineAccess.GetPermissionModelsAsync returns the permission models")]
        public async Task GetPermissionModelsAsync_ReturnsPermissionModels()
        {
            var access = new ClientDefineAccess(new CountingConnector());

            var result = await access.GetPermissionModelsAsync();

            Assert.NotNull(result);
            Assert.IsType<PermissionModels>(result);
        }

        [Fact]
        [DisplayName("ClientDefineAccess.GetFormLayoutAsync returns the form layout")]
        public async Task GetFormLayoutAsync_ReturnsFormLayout()
        {
            var access = new ClientDefineAccess(new CountingConnector());

            var result = await access.GetFormLayoutAsync("Employee");

            Assert.NotNull(result);
            Assert.IsType<FormLayout>(result);
        }

        [Fact]
        [DisplayName("ClientDefineAccess.GetLanguageAsync returns the language resource")]
        public async Task GetLanguageAsync_ReturnsLanguageResource()
        {
            var access = new ClientDefineAccess(new CountingConnector());

            var result = await access.GetLanguageAsync("en", "core");

            Assert.NotNull(result);
            Assert.IsType<LanguageResource>(result);
        }

        [Fact]
        [DisplayName("ClientDefineAccess returns the same cached object when the same key is read again")]
        public async Task GetSystemSettingsAsync_SecondCall_ReturnsCachedObject()
        {
            var access = CreateAccess();

            var result1 = await access.GetSystemSettingsAsync();
            var result2 = await access.GetSystemSettingsAsync();

            Assert.NotNull(result1);
            Assert.Same(result1, result2);
        }

        [Fact]
        [DisplayName("Concurrent misses on the same key are deduplicated into a single connector fetch")]
        public async Task GetFormLayoutAsync_ConcurrentMiss_DeduplicatesToSingleFetch()
        {
            var connector = new GatedConnector();
            var access = new ClientDefineAccess(connector);

            // Two concurrent misses: the first inserts the in-flight task synchronously, and the second hits that task.
            var t1 = access.GetFormLayoutAsync("L1");
            var t2 = access.GetFormLayoutAsync("L1");
            connector.Release();
            await Task.WhenAll(t1, t2);

            Assert.Equal(1, connector.GetDefineCallCount);
        }

        [Fact]
        [DisplayName("After ClearCache the same key is fetched from the connector again (switching tenants does not return the old overlay)")]
        public async Task ClearCache_AfterClear_RefetchesFromConnector()
        {
            var connector = new CountingConnector();
            var access = new ClientDefineAccess(connector);

            await access.GetFormLayoutAsync("L1");
            await access.GetFormLayoutAsync("L1");
            Assert.Equal(1, connector.GetDefineCallCount);

            // Simulates the cache clear after `EnterCompany` switches company and the customizeId changes.
            access.ClearCache();

            await access.GetFormLayoutAsync("L1");
            Assert.Equal(2, connector.GetDefineCallCount);
        }

        [Fact]
        [DisplayName("ClearCache does not break caching afterwards")]
        public async Task ClearCache_DoesNotBreakSubsequentCaching()
        {
            var connector = new CountingConnector();
            var access = new ClientDefineAccess(connector);

            await access.GetFormLayoutAsync("L1");
            access.ClearCache();
            await access.GetFormLayoutAsync("L1");
            await access.GetFormLayoutAsync("L1");
            Assert.Equal(2, connector.GetDefineCallCount);
        }

        [Fact]
        [DisplayName("A failed fetch does not poison the cache and the next read retries")]
        public async Task GetFormLayoutAsync_FailedFetch_IsEvictedAndRetried()
        {
            var connector = new FlakyConnector();
            var access = new ClientDefineAccess(connector);

            // The first fetch throws, and the faulted task must not be cached.
            await Assert.ThrowsAsync<InvalidOperationException>(() => access.GetFormLayoutAsync("L1"));
            var result = await access.GetFormLayoutAsync("L1");

            Assert.NotNull(result);
            Assert.Equal(2, connector.GetDefineCallCount);
        }

        [Fact]
        [DisplayName("ClientDefineAccess propagates the underlying connector exception as is, not as AggregateException")]
        public async Task GetSystemSettingsAsync_PropagatesConnectorException()
        {
            // Points at an unreachable endpoint, so the HTTP call fails and the connector throws.
            var connector = new SystemApiConnector("http://127.0.0.1:1/", Guid.NewGuid());
            var access = new ClientDefineAccess(connector);

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => access.GetSystemSettingsAsync());
            Assert.IsNotType<AggregateException>(ex);
        }

        /// <summary>
        /// Spy connector that counts <see cref="SystemApiConnector.GetDefineAsync{T}"/> calls and
        /// returns a fresh instance, so cache hits (no call) vs misses (call) are observable without
        /// touching a real server.
        /// </summary>
        private sealed class CountingConnector : SystemApiConnector
        {
            public CountingConnector() : base(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid()) { }

            public int GetDefineCallCount { get; private set; }

            public override Task<T> GetDefineAsync<T>(DefineType defineType, string[]? keys = null)
            {
                GetDefineCallCount++;
                return Task.FromResult(Activator.CreateInstance<T>());
            }
        }

        /// <summary>
        /// Spy connector whose <see cref="GetDefineAsync{T}"/> blocks until <see cref="Release"/> is
        /// called, so two concurrent misses can be observed sharing a single in-flight fetch.
        /// </summary>
        private sealed class GatedConnector : SystemApiConnector
        {
            private readonly TaskCompletionSource<bool> _gate =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public GatedConnector() : base(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid()) { }

            public int GetDefineCallCount { get; private set; }

            public void Release() => _gate.TrySetResult(true);

            public override async Task<T> GetDefineAsync<T>(DefineType defineType, string[]? keys = null)
            {
                GetDefineCallCount++;
                await _gate.Task.ConfigureAwait(false);
                return Activator.CreateInstance<T>();
            }
        }

        /// <summary>
        /// Spy connector that throws on its first call and succeeds afterwards, so failure eviction
        /// (a faulted fetch must not poison the cache) is observable.
        /// </summary>
        private sealed class FlakyConnector : SystemApiConnector
        {
            public FlakyConnector() : base(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid()) { }

            public int GetDefineCallCount { get; private set; }

            public override Task<T> GetDefineAsync<T>(DefineType defineType, string[]? keys = null)
            {
                GetDefineCallCount++;
                if (GetDefineCallCount == 1)
                    throw new InvalidOperationException("Simulated transient fetch failure.");
                return Task.FromResult(Activator.CreateInstance<T>());
            }
        }
    }
}
