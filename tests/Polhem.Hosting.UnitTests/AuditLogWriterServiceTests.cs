using System.ComponentModel;
using Polhem.Definition.Logging;
using Polhem.Definition.Settings;
using Polhem.Hosting.Audit;
using Polhem.Repository.Abstractions.AuditLog;
using Microsoft.Extensions.Logging.Abstractions;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// Unit tests of <see cref="AuditLogWriterService"/>: a full queue falls back to a synchronous write (the entry is
    /// not dropped), and the background service writes enqueued entries to the sink in batches once started.
    /// </summary>
    public class AuditLogWriterServiceTests
    {
        private sealed class FakeAuditLogSink : IAuditLogSink
        {
            private readonly object _lock = new();
            private readonly List<AuditEntry> _entries = [];

            public int Count
            {
                get { lock (_lock) { return _entries.Count; } }
            }

            public List<AuditEntry> Snapshot()
            {
                lock (_lock) { return [.. _entries]; }
            }

            public void WriteBatch(IReadOnlyList<AuditEntry> entries)
            {
                lock (_lock)
                {
                    _entries.AddRange(entries);
                }
            }
        }

        /// <summary>
        /// A sink that throws on every write, simulating a failing deployment-specific <see cref="IAuditLogSink"/>.
        /// </summary>
        private sealed class ThrowingAuditLogSink : IAuditLogSink
        {
            public int Attempts { get; private set; }

            public void WriteBatch(IReadOnlyList<AuditEntry> entries)
            {
                Attempts++;
                // Deliberately a type the framework does not list: the sink is a public DI seam,
                // so a narrowed catch list cannot guard it.
                throw new NotImplementedException("sink is broken");
            }
        }

        private sealed class TestAuditEntry : AuditEntry
        {
            public override string TableName => "st_log_test";

            protected override void AddColumns(IList<AuditColumn> columns)
            {
                // No axis-specific columns needed for these tests.
            }
        }

        [Fact]
        [DisplayName("Write falls back to a synchronous write when the queue is full and does not drop the entry")]
        public void Write_QueueFull_FallsBackToSynchronous()
        {
            var sink = new FakeAuditLogSink();
            var options = new AuditLogOptions { QueueCapacity = 1 };
            using var service = new AuditLogWriterService(sink, options, NullLogger<AuditLogWriterService>.Instance);

            var first = new TestAuditEntry();
            var second = new TestAuditEntry();

            // The service is not started, so nothing drains the queue: the first entry fills the
            // bounded queue and the second overflows into the synchronous fallback.
            service.Write(first);
            service.Write(second);

            var written = sink.Snapshot();
            Assert.Single(written);
            Assert.Same(second, written[0]);
        }

        [Fact]
        [DisplayName("The background service writes enqueued entries to the sink after it starts")]
        public async Task BackgroundDrain_WritesEnqueuedEntries()
        {
            var sink = new FakeAuditLogSink();
            var options = new AuditLogOptions { QueueCapacity = 100, BatchSize = 10 };
            using var service = new AuditLogWriterService(sink, options, NullLogger<AuditLogWriterService>.Instance);

            await service.StartAsync(CancellationToken.None);
            try
            {
                var first = new TestAuditEntry();
                var second = new TestAuditEntry();
                service.Write(first);
                service.Write(second);

                // Background drain is asynchronous; poll (up to ~5s) until both entries land.
                for (int i = 0; i < 250 && sink.Count < 2; i++)
                {
                    await Task.Delay(20);
                }

                var written = sink.Snapshot();
                Assert.Contains(first, written);
                Assert.Contains(second, written);
            }
            finally
            {
                await service.StopAsync(CancellationToken.None);
            }
        }
    
        [Fact]
        [DisplayName("The background service does not fault when the sink throws (an escaping exception stops the whole host by .NET default)")]
        public async Task ExecuteAsync_SinkThrows_ServiceKeepsRunning()
        {
            var sink = new ThrowingAuditLogSink();
            using var service = new AuditLogWriterService(
                sink, new AuditLogOptions(), NullLogger<AuditLogWriterService>.Instance);

            await service.StartAsync(CancellationToken.None);
            service.Write(new TestAuditEntry());

            // Wait until the sink has really been called, then confirm the service is still alive.
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (sink.Attempts == 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }
            Assert.True(sink.Attempts > 0, "The sink was never called, so this test verified nothing.");

            // A faulted `ExecuteTask` is the signal on which `BackgroundService` stops the host.
            Assert.NotNull(service.ExecuteTask);
            Assert.NotEqual(TaskStatus.Faulted, service.ExecuteTask!.Status);

            // Later entries are still attempted, so the loop is not dead.
            int before = sink.Attempts;
            service.Write(new TestAuditEntry());
            deadline = DateTime.UtcNow.AddSeconds(5);
            while (sink.Attempts == before && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }
            Assert.True(sink.Attempts > before, "The loop stopped after the first failure.");

            await service.StopAsync(CancellationToken.None);
        }

        [Fact]
        [DisplayName("Concurrent writers to the file fallback do not lose any batch")]
        public async Task SpillToFile_ConcurrentWriters_LoseNothing()
        {
            // This path is reached only after the log database has failed, which is exactly when the queue is full
            // and every request thread piles in. Peak concurrency and the only time this code runs coincide.
            string path = Path.Combine(Path.GetTempPath(), $"polhem_spill_{Guid.NewGuid():N}.log");
            var sink = new AuditLogDbSink(
                new AlwaysFailingWriteRepository(),
                new AuditLogOptions { FileFallbackPath = path },
                NullLogger<AuditLogDbSink>.Instance);

            const int Writers = 8;
            const int PerWriter = 25;
            try
            {
                await Task.WhenAll(Enumerable.Range(0, Writers).Select(_ => Task.Run(() =>
                {
                    for (int i = 0; i < PerWriter; i++)
                    {
                        sink.WriteBatch([new TestAuditEntry()]);
                    }
                })));

                var lines = File.ReadAllLines(path);
                Assert.Equal(Writers * PerWriter, lines.Length);
            }
            finally
            {
                if (File.Exists(path)) { File.Delete(path); }
            }
        }

        /// <summary>Every write fails, forcing <c>AuditLogDbSink</c> onto the file fallback.</summary>
        private sealed class AlwaysFailingWriteRepository : IAuditLogWriteRepository
        {
            public void WriteBatch(IReadOnlyList<AuditEntry> entries)
                => throw new InvalidOperationException("log database is down");
        }
}
}
