using System.ComponentModel;
using Polhem.Definition.Logging;
using Polhem.Definition.Settings;
using Polhem.Hosting.Audit;
using Polhem.Repository.Abstractions.AuditLog;
using Microsoft.Extensions.Logging.Abstractions;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// What <see cref="AuditLogDbSink"/> does when the log database refuses a batch: one entry it refuses does not
    /// take the rest of the batch with it, an outage does not cost one attempt per entry, and the fallback file
    /// cannot be given a line the writer did not write.
    /// </summary>
    public sealed class AuditLogDbSinkTests : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"polhem_sink_{Guid.NewGuid():N}.log");

        public void Dispose()
        {
            if (File.Exists(_path)) { File.Delete(_path); }
        }

        private AuditLogDbSink Sink(IAuditLogWriteRepository repository)
            => new(repository, new AuditLogOptions { FileFallbackPath = _path }, NullLogger<AuditLogDbSink>.Instance);

        private static LoginAuditEntry Entry(string userId) => new() { UserId = userId, Event = LoginEvent.LoginFailed };

        [Fact]
        [DisplayName("A batch refused because of one entry is written one entry at a time, and only that entry is spilled")]
        public void WriteBatch_OneRefusedEntry_WritesTheRestAndSpillsOnlyIt()
        {
            var repository = new RefusingRepository(entry => entry.UserId == "bad");
            var batch = new[] { Entry("a"), Entry("bad"), Entry("b"), Entry("c") };

            Sink(repository).WriteBatch(batch);

            Assert.Equal(["a", "b", "c"], repository.Written.Select(e => e.UserId));
            var spilled = File.ReadAllLines(_path);
            Assert.Contains("user_id=bad", Assert.Single(spilled), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("When the database is down, two failed single writes stop the retry and the rest of the batch is spilled untried")]
        public void WriteBatch_Outage_StopsAfterTwoSingleFailures()
        {
            var repository = new RefusingRepository(_ => true);
            var batch = Enumerable.Range(0, 10).Select(i => Entry($"u{i}")).ToArray();

            Sink(repository).WriteBatch(batch);

            // One batch attempt, then two single attempts before the retry gives up.
            Assert.Equal(3, repository.Attempts);
            Assert.Equal(batch.Length, File.ReadAllLines(_path).Length);
        }

        [Fact]
        [DisplayName("A line feed or tab in a value is escaped, so a spilled entry stays on one line")]
        public void WriteBatch_ValueWithLineBreak_StaysOnOneLine()
        {
            var repository = new RefusingRepository(_ => true);

            Sink(repository).WriteBatch([Entry("x\nst_log_login\tuser_id=forged")]);

            var line = Assert.Single(File.ReadAllLines(_path));
            Assert.Contains(@"user_id=x\nst_log_login\tuser_id=forged", line, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("plain", "plain")]
        [InlineData("a\\b", @"a\\b")]
        [InlineData("a\tb", @"a\tb")]
        [InlineData("a\r\nb", @"a\r\nb")]
        [InlineData("", "")]
        [DisplayName("Escape writes a backslash, a tab, a carriage return and a line feed as two-character escapes")]
        public void Escape_ControlCharacters_AreEscaped(string value, string expected)
            => Assert.Equal(expected, AuditLogDbSink.Escape(value));

        /// <summary>Refuses any batch that holds an entry <c>refuse</c> picks, as a database refuses an over-long value.</summary>
        private sealed class RefusingRepository(Func<LoginAuditEntry, bool> refuse) : IAuditLogWriteRepository
        {
            public List<LoginAuditEntry> Written { get; } = [];

            public int Attempts { get; private set; }

            public void WriteBatch(IReadOnlyList<AuditEntry> entries)
            {
                Attempts++;
                var logins = entries.Cast<LoginAuditEntry>().ToList();
                if (logins.Any(refuse))
                    throw new InvalidOperationException("refused");
                Written.AddRange(logins);
            }
        }
    }
}
