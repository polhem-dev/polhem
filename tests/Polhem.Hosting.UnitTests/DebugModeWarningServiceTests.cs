using System.ComponentModel;
using Polhem.Hosting.Registry;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// <see cref="DebugModeWarningService"/> logs an error when debug mode is on outside the Development environment.
    /// </summary>
    public class DebugModeWarningServiceTests
    {
        [Theory]
        [InlineData("Production", true, true)]
        [InlineData("Staging", true, true)]
        [InlineData("Development", true, false)]
        [InlineData("Production", false, false)]
        [DisplayName("StartAsync logs one error when debug mode is on outside Development, and nothing otherwise")]
        public async Task StartAsync_ByEnvironmentAndFlag_LogsWhenDebugModeIsOnOutsideDevelopment(
            string environment, bool isDebugMode, bool expectError)
        {
            var logger = new ListLogger();
            var service = new DebugModeWarningService(new FakeHostEnvironment(environment), logger, () => isDebugMode);

            await service.StartAsync(CancellationToken.None);

            if (expectError)
            {
                var entry = Assert.Single(logger.Entries);
                Assert.Equal(LogLevel.Error, entry.Level);
                Assert.Contains("IsDebugMode", entry.Message, StringComparison.Ordinal);
            }
            else
            {
                Assert.Empty(logger.Entries);
            }
        }

        private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = environmentName;
            public string ApplicationName { get; set; } = "Polhem.Hosting.UnitTests";
            public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }

        private sealed class ListLogger : ILogger<DebugModeWarningService>
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
