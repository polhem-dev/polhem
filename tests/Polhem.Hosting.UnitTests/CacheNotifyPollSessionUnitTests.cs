using System.ComponentModel;
using Polhem.Db.CacheNotify;
using Polhem.Hosting.CacheNotify;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// Constructor guards of <see cref="CacheNotifyPollSession"/>.
    /// The per-dialect SQL branches have moved down to <c>CacheNotifyReader</c> in <c>Polhem.Db</c>;
    /// their tests are in <c>Polhem.Db.UnitTests.CacheNotifyReaderUnitTests</c>.
    /// </summary>
    public class CacheNotifyPollSessionUnitTests
    {
        private sealed class StubReader : ICacheNotifyReader
        {
            public DateTime ReadBaseline(string databaseId) => throw new NotImplementedException();

            public IReadOnlyList<CacheNotifyChange> ReadChangesSince(string databaseId, DateTime threshold)
                => throw new NotImplementedException();
        }

        private static readonly ICacheNotifyReader s_reader = new StubReader();

        [Fact]
        [DisplayName("CacheNotifyPollSession constructor throws ArgumentNullException for a null databaseId")]
        public void Constructor_NullDatabaseId_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CacheNotifyPollSession(null!, s_reader, marginSeconds: 0));
        }

        [Fact]
        [DisplayName("CacheNotifyPollSession constructor throws ArgumentException for a whitespace databaseId")]
        public void Constructor_WhitespaceDatabaseId_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new CacheNotifyPollSession("   ", s_reader, marginSeconds: 0));
        }

        [Fact]
        [DisplayName("CacheNotifyPollSession constructor throws ArgumentNullException for a null reader")]
        public void Constructor_NullReader_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CacheNotifyPollSession("test_db", null!, marginSeconds: 0));
        }

        [Fact]
        [DisplayName("CacheNotifyPollSession constructor accepts a negative marginSeconds (normalized to 0)")]
        public void Constructor_NegativeMarginSeconds_CreatesInstanceWithoutThrowing()
        {
            var exception = Record.Exception(() =>
                new CacheNotifyPollSession("test_db", s_reader, marginSeconds: -1));
            Assert.Null(exception);
        }
    }
}
