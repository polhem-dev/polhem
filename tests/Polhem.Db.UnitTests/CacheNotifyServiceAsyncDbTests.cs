using System.ComponentModel;
using System.Globalization;
using Polhem.Db.CacheNotify;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Database integration tests for <see cref="CacheNotifyService.TouchAsync"/>.
    /// The existing <c>CacheNotifyServiceTests</c> only test the synchronous <c>Touch</c>; this class covers the
    /// asynchronous path, so that the UPSERT runs asynchronously on every dialect.
    /// </summary>
    public class CacheNotifyServiceAsyncDbTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public CacheNotifyServiceAsyncDbTests(SharedDbFixture fx) { _fx = fx; }

        private static string NewKey() => $"CacheNotifyAsyncTest:{Guid.NewGuid():N}";

        private async Task ExecuteTouchAsync(DatabaseType databaseType, string cacheKey)
        {
            var databaseId = TestDbConventions.GetDatabaseId(databaseType);
            var connectionManager = _fx.GetRequiredService<IDbConnectionManager>();
            var service = _fx.GetRequiredService<ICacheNotifyService>();

            using var connection = connectionManager.CreateConnection(databaseId);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            await service.TouchAsync(cacheKey, transaction, databaseType).ConfigureAwait(false);
            transaction.Commit();
        }

        private long ReadVersion(DatabaseType databaseType, string cacheKey)
        {
            var databaseId = TestDbConventions.GetDatabaseId(databaseType);
            var dbAccess = _fx.NewDbAccess(databaseId);

            string tbl = databaseType.QuoteIdentifier("st_cache_notify");
            string key = databaseType.QuoteIdentifier("cache_key");
            string ver = databaseType.QuoteIdentifier("cache_version");

            var table = dbAccess.ExecuteDataTable(
                $"SELECT {ver} FROM {tbl} WHERE {key} = {{0}}", cacheKey)
                ?? throw new InvalidOperationException("Query returned no table.");

            Assert.Single(table.Rows);
            return Convert.ToInt64(table.Rows[0][0], CultureInfo.InvariantCulture);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server TouchAsync increments the version and a second Touch reaches 2")]
        public async Task TouchAsync_SqlServer_VersionIncrements()
        {
            var key = NewKey();
            await ExecuteTouchAsync(DatabaseType.SQLServer, key).ConfigureAwait(false);
            Assert.Equal(1L, ReadVersion(DatabaseType.SQLServer, key));
            await ExecuteTouchAsync(DatabaseType.SQLServer, key).ConfigureAwait(false);
            Assert.Equal(2L, ReadVersion(DatabaseType.SQLServer, key));
        }

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL TouchAsync increments the version and a second Touch reaches 2")]
        public async Task TouchAsync_PostgreSQL_VersionIncrements()
        {
            var key = NewKey();
            await ExecuteTouchAsync(DatabaseType.PostgreSQL, key).ConfigureAwait(false);
            Assert.Equal(1L, ReadVersion(DatabaseType.PostgreSQL, key));
            await ExecuteTouchAsync(DatabaseType.PostgreSQL, key).ConfigureAwait(false);
            Assert.Equal(2L, ReadVersion(DatabaseType.PostgreSQL, key));
        }

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL TouchAsync increments the version and a second Touch reaches 2")]
        public async Task TouchAsync_MySQL_VersionIncrements()
        {
            var key = NewKey();
            await ExecuteTouchAsync(DatabaseType.MySQL, key).ConfigureAwait(false);
            Assert.Equal(1L, ReadVersion(DatabaseType.MySQL, key));
            await ExecuteTouchAsync(DatabaseType.MySQL, key).ConfigureAwait(false);
            Assert.Equal(2L, ReadVersion(DatabaseType.MySQL, key));
        }

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle TouchAsync increments the version and a second Touch reaches 2")]
        public async Task TouchAsync_Oracle_VersionIncrements()
        {
            var key = NewKey();
            await ExecuteTouchAsync(DatabaseType.Oracle, key).ConfigureAwait(false);
            Assert.Equal(1L, ReadVersion(DatabaseType.Oracle, key));
            await ExecuteTouchAsync(DatabaseType.Oracle, key).ConfigureAwait(false);
            Assert.Equal(2L, ReadVersion(DatabaseType.Oracle, key));
        }
    }
}
