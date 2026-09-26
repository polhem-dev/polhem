using System.ComponentModel;
using System.Reflection;
using Polhem.Db.CacheNotify;
using Polhem.Definition.Settings;
using Polhem.Hosting.CacheNotify;
using Microsoft.Extensions.Logging;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// Unit tests of <see cref="CacheNotifyPoller.ExecuteAsync"/>: the timer loop ends normally once the
    /// CancellationToken is cancelled, including when IntervalSeconds is 0 (which falls back to 5 seconds).
    /// </summary>
    public class CacheNotifyPollerExecuteAsyncTests
    {
        private sealed class ThrowingReader : ICacheNotifyReader
        {
            private readonly Exception _exception;
            public ThrowingReader(Exception exception) { _exception = exception; }

            public DateTime ReadBaseline(string databaseId) => throw _exception;

            public IReadOnlyList<CacheNotifyChange> ReadChangesSince(string databaseId, DateTime threshold)
                => throw _exception;
        }

        private sealed class StubLogger : ILogger<CacheNotifyPoller>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => false;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter) { }
        }

        private static readonly ILogger<CacheNotifyPoller> s_logger = new StubLogger();

        private static CacheNotifyPoller MakePoller(CacheNotifyOptions options)
        {
            var reader = new ThrowingReader(new InvalidOperationException("sim poll error"));
            return new CacheNotifyPoller(reader, options, s_logger);
        }

        private static async Task InvokeExecuteAsync(CacheNotifyPoller poller, CancellationToken token)
        {
            var method = typeof(CacheNotifyPoller).GetMethod(
                "ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var task = (Task)method!.Invoke(poller, new object[] { token })!;
            await task;
        }

        [Fact]
        [DisplayName("ExecuteAsync completes normally without throwing when given an already cancelled CancellationToken")]
        public async Task ExecuteAsync_PreCancelledToken_CompletesNormally()
        {
            var poller = MakePoller(new CacheNotifyOptions());
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var exception = await Record.ExceptionAsync(() => InvokeExecuteAsync(poller, cts.Token));

            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("ExecuteAsync with IntervalSeconds 0 completes normally (the interval falls back to 5 seconds)")]
        public async Task ExecuteAsync_ZeroIntervalSeconds_UsesDefaultFiveSeconds_CompletesNormally()
        {
            var options = new CacheNotifyOptions { IntervalSeconds = 0 };
            var poller = MakePoller(options);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var exception = await Record.ExceptionAsync(() => InvokeExecuteAsync(poller, cts.Token));

            Assert.Null(exception);
        }
    }
}
