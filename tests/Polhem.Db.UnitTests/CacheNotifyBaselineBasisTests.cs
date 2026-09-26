using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.CacheNotify;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// The basis of the cache-notify poll cursor: the reader's "now" must use the same basis that the writer uses
    /// to stamp <c>sys_update_time</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two sides once had a dialect table each: the writer (the column DEFAULT and the upsert in
    /// <c>CacheNotifyService</c>) used UTC, while the reader's empty-table baseline used **the server's local time**
    /// (<c>getdate()</c> / <c>LOCALTIMESTAMP</c> / <c>CURRENT_TIMESTAMP(6)</c>).
    /// </para>
    /// <para>
    /// The harm: on a database server east of UTC, the first baseline of a fresh deployment (empty
    /// <c>st_cache_notify</c>) lies in the future, and every later poll window finds no rows. **Cache invalidation
    /// silently stops** until the wall clock catches up, which is eight hours at UTC+8. Measured (2026-09-04): under
    /// a UTC+8 session, PostgreSQL and MySQL differ by exactly 8 hours between the two expressions.
    /// </para>
    /// <para>
    /// <b>Why neither local runs nor CI see it</b>: the local containers and the GitHub runners all run in UTC, where
    /// the two expressions happen to be equal. So the first test below **checks the expression, not the value**.
    /// That is the only check that also holds in a UTC environment.
    /// </para>
    /// </remarks>
    public class CacheNotifyBaselineBasisTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public CacheNotifyBaselineBasisTests(SharedDbFixture fx) { _fx = fx; }

        public static TheoryData<DatabaseType> SupportedDialects() =>
            [DatabaseType.SQLServer, DatabaseType.PostgreSQL, DatabaseType.MySQL,
             DatabaseType.Oracle, DatabaseType.SQLite];

        [Theory]
        [MemberData(nameof(SupportedDialects))]
        [DisplayName("The empty-table baseline time expression is identical to the one the writer uses to stamp sys_update_time")]
        public void BaselineNowExpression_MatchesTheWriteSideExpression(DatabaseType databaseType)
        {
            // The writer side: the column DEFAULT and the upsert in `CacheNotifyService` both read this.
            string writeSide = DbDialectRegistry.Get(databaseType).GetDefaultValueExpression(FieldDbType.DateTime);
            Assert.NotEqual(string.Empty, writeSide);   // Without a value the comparison below means nothing.

            string readSide = CacheNotifyReader.BaselineNowCommandText(databaseType);

            Assert.Contains(writeSide, readSide, StringComparison.Ordinal);
            Assert.StartsWith("SELECT ", readSide, StringComparison.Ordinal);
        }

        [DbTheory(DatabaseType.SQLServer)]
        [InlineData(DatabaseType.SQLServer)]
        [DisplayName("SQL Server baseline statement returns a value close to UTC, not the server's local time")]
        public void BaselineNow_SqlServer_ReturnsUtc(DatabaseType databaseType) => AssertBaselineIsUtc(databaseType);

        [DbTheory(DatabaseType.PostgreSQL)]
        [InlineData(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL baseline statement returns a value close to UTC, not the server's local time")]
        public void BaselineNow_PostgreSql_ReturnsUtc(DatabaseType databaseType) => AssertBaselineIsUtc(databaseType);

        [DbTheory(DatabaseType.MySQL)]
        [InlineData(DatabaseType.MySQL)]
        [DisplayName("MySQL baseline statement returns a value close to UTC, not the server's local time")]
        public void BaselineNow_MySql_ReturnsUtc(DatabaseType databaseType) => AssertBaselineIsUtc(databaseType);

        [DbTheory(DatabaseType.Oracle)]
        [InlineData(DatabaseType.Oracle)]
        [DisplayName("Oracle baseline statement returns a value close to UTC, not the server's local time")]
        public void BaselineNow_Oracle_ReturnsUtc(DatabaseType databaseType) => AssertBaselineIsUtc(databaseType);

        /// <summary>
        /// Runs the baseline statement and confirms that it returns UTC.
        /// </summary>
        /// <param name="databaseType">The target database.</param>
        /// <remarks>
        /// While the containers run in UTC this test cannot tell UTC from local time. What it catches is a broken
        /// statement or one the dialect rejects; the basis of the expression is covered by the Theory above.
        /// The two complement each other.
        /// </remarks>
        private void AssertBaselineIsUtc(DatabaseType databaseType)
        {
            var dbAccess = _fx.NewDbAccess(TestDbConventions.GetDatabaseId(databaseType));
            string sql = CacheNotifyReader.BaselineNowCommandText(databaseType);

            var value = Convert.ToDateTime(dbAccess.ExecuteScalar(sql),
                System.Globalization.CultureInfo.InvariantCulture);

            // Loose to the minute: this catches a whole time zone of difference, not small clock drift.
            Assert.InRange(value, DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(10));
        }
    }
}
