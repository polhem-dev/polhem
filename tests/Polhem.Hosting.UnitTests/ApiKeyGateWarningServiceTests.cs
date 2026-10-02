using System.ComponentModel;
using System.Data.Common;
using Polhem.Definition.Security;
using Polhem.Hosting.ApiKeys;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// At startup the host reports a deployment with no issued API key, because the <c>X-Api-Key</c> header is then
    /// checked for presence only.
    /// </summary>
    public sealed class ApiKeyGateWarningServiceTests
    {
        [Fact]
        [DisplayName("StartAsync logs nothing while the API key gate is in force")]
        public async Task StartAsync_GateInForce_LogsNothing()
        {
            var logger = new ListLogger();

            await CreateService(() => new ApiKeyGateState { InForce = true }, Environments.Production, logger)
                .StartAsync(CancellationToken.None);

            Assert.Empty(logger.Entries);
        }

        [Fact]
        [DisplayName("StartAsync logs an error outside Development while no API key is enabled")]
        public async Task StartAsync_NoKeyInProduction_LogsError()
        {
            var logger = new ListLogger();

            await CreateService(() => new ApiKeyGateState { InForce = false }, Environments.Production, logger)
                .StartAsync(CancellationToken.None);

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Contains("No enabled API key exists", entry.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("StartAsync treats a missing key store like a store with no keys")]
        public async Task StartAsync_NoKeyStore_LogsError()
        {
            var logger = new ListLogger();

            await CreateService(() => null, Environments.Production, logger).StartAsync(CancellationToken.None);

            Assert.Equal(LogLevel.Error, Assert.Single(logger.Entries).Level);
        }

        [Fact]
        [DisplayName("StartAsync logs only a warning in Development while no API key is enabled")]
        public async Task StartAsync_NoKeyInDevelopment_LogsWarning()
        {
            var logger = new ListLogger();

            await CreateService(() => new ApiKeyGateState { InForce = false }, Environments.Development, logger)
                .StartAsync(CancellationToken.None);

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("expected in Development", entry.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("StartAsync reports an unreachable key store as unreachable, not as an open gate")]
        public async Task StartAsync_StoreUnreachable_LogsUnreachableWarning()
        {
            var logger = new ListLogger();

            await CreateService(() => throw new FakeDbException(), Environments.Production, logger)
                .StartAsync(CancellationToken.None);

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("Could not determine", entry.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("AddPolhemApiKeyGateCheck registers the startup check once however often it is called")]
        public void AddPolhemApiKeyGateCheck_CalledTwice_RegistersOneHostedService()
        {
            var services = new ServiceCollection();

            services.AddPolhemApiKeyGateCheck().AddPolhemApiKeyGateCheck();

            Assert.Single(services, d => d.ServiceType == typeof(IHostedService)
                && d.ImplementationType == typeof(ApiKeyGateWarningService));
        }

        private static ApiKeyGateWarningService CreateService(
            Func<ApiKeyGateState?> getState, string environmentName, ListLogger logger)
            => new(new FakeGateStateProvider(getState), new FakeHostEnvironment(environmentName), logger);

        private sealed class FakeGateStateProvider(Func<ApiKeyGateState?> getState) : IApiKeyGateStateProvider
        {
            public ApiKeyGateState? GetState() => getState();
        }

        private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = environmentName;
            public string ApplicationName { get; set; } = "Polhem.Hosting.UnitTests";
            public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }

        private sealed class FakeDbException : DbException
        {
            public FakeDbException() : base("The key store is unreachable.") { }
        }

        private sealed class ListLogger : ILogger<ApiKeyGateWarningService>
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
