using System.ComponentModel;
using Polhem.Api.Core;
using Polhem.Business;
using Polhem.Definition;
using Polhem.Definition.Storage;
using Polhem.Hosting.Registry;
using Polhem.ObjectCaching;
using Microsoft.Extensions.Logging;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// At startup the host names the methods that declare replay protection while the wire frame it depends on is
    /// off, because the declaration reads as a guarantee that no call then honours.
    /// </summary>
    /// <remarks>
    /// Reads <see cref="ApiServiceOptions.RequireWireFrame"/> at its default and never writes it; no test in this
    /// assembly changes it.
    /// </remarks>
    public sealed class ReplayProtectionWarningServiceTests : IDisposable
    {
        private readonly string _defineDir;
        private readonly PathOptions _paths;

        public ReplayProtectionWarningServiceTests()
        {
            _defineDir = Path.Combine(Path.GetTempPath(), $"polhem-replay-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_defineDir);
            _paths = new PathOptions { DefinePath = _defineDir };
        }

        public void Dispose()
        {
            try { Directory.Delete(_defineDir, recursive: true); } catch (IOException) { /* best effort */ }
        }

        [Fact]
        [DisplayName("FindReplayProtectedMethods lists the framework's write methods and leaves out the reads")]
        public void FindReplayProtectedMethods_FrameworkBusinessObjects_ListsTheWrites()
        {
            var methods = CreateService(new ListLogger()).FindReplayProtectedMethods();

            Assert.Contains($"{SysProgIds.System}.EnterCompany", methods);
            Assert.Contains($"{SysProgIds.System}.LeaveCompany", methods);
            Assert.Contains($"{SysProgIds.System}.ExecFunc", methods);
            Assert.Contains($"{SysProgIds.AuditRule}.Save", methods);
            Assert.DoesNotContain($"{SysProgIds.System}.Login", methods);
            Assert.DoesNotContain($"{SysProgIds.System}.Ping", methods);
        }

        [Fact]
        [DisplayName("StartAsync logs one warning naming the replay-protected methods while the wire frame is off")]
        public async Task StartAsync_WireFrameOff_LogsOneWarning()
        {
            Assert.False(ApiServiceOptions.RequireWireFrame);
            var logger = new ListLogger();

            await CreateService(logger).StartAsync(CancellationToken.None);

            var warning = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, warning.Level);
            Assert.Contains("RequireWireFrame", warning.Message, StringComparison.Ordinal);
            Assert.Contains($"{SysProgIds.System}.EnterCompany", warning.Message, StringComparison.Ordinal);
        }

        private ReplayProtectionWarningService CreateService(ILogger<ReplayProtectionWarningService> logger)
        {
            var storage = new FileDefineStorage(_paths);
            var cache = new CacheContainerService(storage, _paths, "replay_" + Guid.NewGuid().ToString("N"));
            var access = new CacheDefineAccess(storage, _paths, cache, Array.Empty<byte>());
            return new ReplayProtectionWarningService(access, new ProgramSettingsBoTypeResolver(access), logger);
        }

        private sealed class ListLogger : ILogger<ReplayProtectionWarningService>
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
