using System.ComponentModel;
using System.Data.Common;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;
using Polhem.Hosting.Session;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.Form;
using Polhem.Repository.Abstractions.System;
using Microsoft.Extensions.Logging;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// Behavior tests of <see cref="ExpiredSessionCleanupService"/>.
    /// </summary>
    /// <remarks>
    /// This is the framework background service that **deletes data**, and it had no coverage before. Three things
    /// are worth pinning down: it sweeps once at startup (not at the first tick), a transient DB error must not end
    /// the loop (otherwise the table grows until the process exits), and <c>IntervalSeconds &lt;= 0</c> falls back to
    /// 3600 seconds instead of becoming a busy loop.
    /// </remarks>
    public class ExpiredSessionCleanupServiceTests
    {
        /// <summary>Counts calls, and can decide from the call number whether to throw.</summary>
        private sealed class RecordingSessionRepository : ISessionRepository
        {
            private readonly Func<int, int> _behavior;
            private int _calls;

            public RecordingSessionRepository(Func<int, int> behavior) { _behavior = behavior; }

            public int Calls => Volatile.Read(ref _calls);

            public int DeleteExpiredSessions()
            {
                var n = Interlocked.Increment(ref _calls);
                return _behavior(n);
            }

            public SessionUser? GetSession(Guid accessToken) => throw new NotSupportedException();
            public void InsertSession(SessionUser sessionUser) => throw new NotSupportedException();
            public void UpdateSession(SessionUser sessionUser) => throw new NotSupportedException();
            public void DeleteSession(Guid accessToken) => throw new NotSupportedException();
        }

        private sealed class StubRepositoryFactory : IRepositoryFactory
        {
            private readonly ISessionRepository _repository;
            public StubRepositoryFactory(ISessionRepository repository) { _repository = repository; }

            public T Create<T>(Guid accessToken = default) where T : class => (T)_repository;

            public T CreateFormRepository<T>(Guid accessToken, string progId)
                where T : class, IDataFormRepository => throw new NotSupportedException();
        }

        /// <summary><see cref="DbException"/> is abstract, so the tests need a concrete type to throw.</summary>
        private sealed class FakeDbException : DbException
        {
            public FakeDbException() : base("simulated transient database failure") { }
        }

        private sealed class StubLogger : ILogger<ExpiredSessionCleanupService>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => false;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter) { }
        }

        private static ExpiredSessionCleanupService Create(
            RecordingSessionRepository repository, int intervalSeconds) =>
            new(new StubRepositoryFactory(repository),
                new SessionCleanupOptions { Enabled = true, IntervalSeconds = intervalSeconds },
                new StubLogger());

        /// <summary>Polls instead of trading a fixed sleep for stability.</summary>
        private static async Task<bool> WaitForCallsAsync(
            RecordingSessionRepository repository, int expected, int timeoutMs = 5000)
        {
            var deadline = Environment.TickCount64 + timeoutMs;
            while (Environment.TickCount64 < deadline)
            {
                if (repository.Calls >= expected) { return true; }
                await Task.Delay(20).ConfigureAwait(false);
            }
            return repository.Calls >= expected;
        }

        [Fact]
        [DisplayName("StartAsync sweeps immediately instead of waiting for the first tick")]
        public async Task StartAsync_SweepsImmediately()
        {
            // The interval is deliberately very long: without the startup sweep this test could
            // only pass after an hour.
            var repository = new RecordingSessionRepository(_ => 3);
            var service = Create(repository, intervalSeconds: 3600);

            await service.StartAsync(CancellationToken.None);
            try
            {
                Assert.True(await WaitForCallsAsync(repository, 1), "No cleanup was observed after startup.");
            }
            finally
            {
                await service.StopAsync(CancellationToken.None);
                service.Dispose();
            }
        }

        [Fact]
        [DisplayName("A transient DbException does not end the loop and the next tick still sweeps")]
        public async Task DbException_DoesNotEndTheLoop()
        {
            // The first two calls throw `DbException`: the first is the startup sweep
            // and the second is the first tick.
            // If the exception ended the loop, the third call would never happen.
            var repository = new RecordingSessionRepository(n =>
                n <= 2 ? throw new FakeDbException() : 0);
            var service = Create(repository, intervalSeconds: 1);

            await service.StartAsync(CancellationToken.None);
            try
            {
                Assert.True(await WaitForCallsAsync(repository, 3),
                    $"The loop stopped after a DbException; only {repository.Calls} calls were observed.");
            }
            finally
            {
                await service.StopAsync(CancellationToken.None);
                service.Dispose();
            }
        }

        [Fact]
        [DisplayName("An IntervalSeconds of 0 falls back to the default 3600 seconds instead of becoming a busy loop")]
        public async Task ZeroInterval_FallsBackToDefault_AndDoesNotSpin()
        {
            var repository = new RecordingSessionRepository(_ => 0);
            var service = Create(repository, intervalSeconds: 0);

            await service.StartAsync(CancellationToken.None);
            try
            {
                Assert.True(await WaitForCallsAsync(repository, 1), "No cleanup was observed after startup.");

                // The fallback is 3600 seconds, so there must be no second call in this observation window.
                // If the fallback broke (`PeriodicTimer` throws for `TimeSpan.Zero`, or the interval became 0),
                // calls would spike here.
                await Task.Delay(600);
                Assert.Equal(1, repository.Calls);
            }
            finally
            {
                await service.StopAsync(CancellationToken.None);
                service.Dispose();
            }
        }

        [Fact]
        [DisplayName("StopAsync does not throw (cancellation is the normal shutdown path)")]
        public async Task StopAsync_CompletesWithoutThrowing()
        {
            var repository = new RecordingSessionRepository(_ => 0);
            var service = Create(repository, intervalSeconds: 1);
            await service.StartAsync(CancellationToken.None);

            var exception = await Record.ExceptionAsync(() => service.StopAsync(CancellationToken.None));

            service.Dispose();
            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("An exception other than DbException is not swallowed (it is not the failure this catch handles)")]
        public async Task NonDbException_IsNotSwallowed()
        {
            var repository = new RecordingSessionRepository(_ => throw new InvalidOperationException("bug"));
            var service = Create(repository, intervalSeconds: 3600);

            await service.StartAsync(CancellationToken.None);

            // The assertion is on `ExecuteTask`, not `StartAsync`. Whether `BackgroundService` hands a completed
            // execute task back to `StartAsync` is its internal policy, and measured here it did not propagate. What
            // must be shown is that this exception was not swallowed by the `catch (DbException)` in `SafeCleanup`,
            // and the state of `ExecuteTask` is the direct evidence of that.
            var executeTask = service.ExecuteTask;
            Assert.NotNull(executeTask);
            var exception = await Record.ExceptionAsync(() => executeTask!);

            service.Dispose();
            Assert.IsType<InvalidOperationException>(exception);
        }
    }
}
