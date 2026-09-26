using System.ComponentModel;
using System.Data.Common;
using System.Reflection;
using Polhem.Db.CacheNotify;
using Polhem.Definition.Settings;
using Polhem.Hosting.CacheNotify;
using Microsoft.Extensions.Logging;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// Unit tests of the <see cref="CacheNotifyPoller"/> constructor guards and the exception swallowing in
    /// <c>SafePoll</c>.
    /// </summary>
    public class CacheNotifyPollerUnitTests
    {
        private sealed class StubReader : ICacheNotifyReader
        {
            public DateTime ReadBaseline(string databaseId) => throw new NotImplementedException();

            public IReadOnlyList<CacheNotifyChange> ReadChangesSince(string databaseId, DateTime threshold)
                => throw new NotImplementedException();
        }

        private sealed class ThrowingReader : ICacheNotifyReader
        {
            private readonly Exception _exception;
            public ThrowingReader(Exception exception) { _exception = exception; }

            public DateTime ReadBaseline(string databaseId) => throw _exception;

            public IReadOnlyList<CacheNotifyChange> ReadChangesSince(string databaseId, DateTime threshold)
                => throw _exception;
        }

        private sealed class FakeDbException : DbException
        {
            public FakeDbException(string message) : base(message) { }
        }

        private sealed class StubLogger : ILogger<CacheNotifyPoller>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => false;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) { }
        }

        private static readonly ICacheNotifyReader s_reader = new StubReader();
        private static readonly CacheNotifyOptions s_options = new();
        private static readonly ILogger<CacheNotifyPoller> s_logger = new StubLogger();

        [Fact]
        [DisplayName("CacheNotifyPoller constructor creates an instance without throwing when every argument is valid")]
        public void Constructor_ValidArguments_CreatesInstance()
        {
            var exception = Record.Exception(() =>
                new CacheNotifyPoller(s_reader, s_options, s_logger));
            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("CacheNotifyPoller constructor throws ArgumentNullException for a null reader")]
        public void Constructor_NullReader_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CacheNotifyPoller(null!, s_options, s_logger));
        }

        [Fact]
        [DisplayName("CacheNotifyPoller constructor throws ArgumentNullException for null options")]
        public void Constructor_NullOptions_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CacheNotifyPoller(s_reader, null!, s_logger));
        }

        [Fact]
        [DisplayName("CacheNotifyPoller constructor throws ArgumentNullException for a null logger")]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CacheNotifyPoller(s_reader, s_options, null!));
        }

        [Fact]
        [DisplayName("SafePoll swallows an InvalidOperationException from session.Poll() instead of propagating it")]
        public void SafePoll_SessionThrowsInvalidOperationException_DoesNotPropagate()
        {
            var throwingReader = new ThrowingReader(new InvalidOperationException("simulated db error"));
            var session = new CacheNotifyPollSession("test_db", throwingReader, marginSeconds: 0);
            var poller = new CacheNotifyPoller(s_reader, s_options, s_logger);

            var safePollMethod = typeof(CacheNotifyPoller).GetMethod(
                "SafePoll", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(safePollMethod);

            var exception = Record.Exception(() => safePollMethod!.Invoke(poller, new object[] { session }));
            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("SafePoll swallows a DbException from session.Poll() instead of propagating it")]
        public void SafePoll_SessionThrowsDbException_DoesNotPropagate()
        {
            var throwingReader = new ThrowingReader(new FakeDbException("simulated db provider exception"));
            var session = new CacheNotifyPollSession("test_db", throwingReader, marginSeconds: 0);
            var poller = new CacheNotifyPoller(s_reader, s_options, s_logger);

            var safePollMethod = typeof(CacheNotifyPoller).GetMethod(
                "SafePoll", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(safePollMethod);

            var exception = Record.Exception(() => safePollMethod!.Invoke(poller, new object[] { session }));
            Assert.Null(exception);
        }
    }
}
